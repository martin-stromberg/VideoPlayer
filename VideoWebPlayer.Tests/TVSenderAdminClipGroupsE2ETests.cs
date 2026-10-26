using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the "Clip-Gruppen" tab of the TV channel administration: group CRUD,
/// the protected reserved "Trailer" system group, clip assignment via the candidate picker and
/// the rule editor fields.
/// </summary>
public sealed class TVSenderAdminClipGroupsE2ETests : TVSenderAdminE2ETestBase
{
    private Task OpenAdminPageAsAdminAsync()
        => OpenAdminPageAsAdminAsync("#tvsender-tab-clipgruppen");

    /// <summary>Seeds the reserved "Trailer" group when the fixture database lacks the
    /// migration InsertData seed (e.g. EnsureCreated schemas).</summary>
    private async Task SeedTrailerClipGroupAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        if (!await db.ProgramClipGroups.AnyAsync(g => g.Id == ProgramClipGroup.TrailerGroupId, ct))
        {
            db.ProgramClipGroups.Add(new ProgramClipGroup { Id = ProgramClipGroup.TrailerGroupId, Name = ProgramClipGroup.TrailerGroupName, IsSystem = true });
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Seeds a named (non-system) clip group directly in the database and returns its id.</summary>
    private async Task<long> SeedClipGroupAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var group = new ProgramClipGroup { Name = name };
        db.ProgramClipGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return group.Id;
    }

    /// <summary>Links a media item to a clip group directly in the database.</summary>
    private async Task SeedClipGroupItemAsync(long groupId, long mediaItemId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProgramClipGroupItems.Add(new ProgramClipGroupItem { ProgramClipGroupId = groupId, MediaItemId = mediaItemId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Assigns a clip group to a program rule directly in the database.</summary>
    private async Task SeedRuleClipGroupLinkAsync(long ruleId, long groupId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProgramRuleClipGroups.Add(new ProgramRuleClipGroup { ProgramRuleId = ruleId, ProgramClipGroupId = groupId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Seeds a movie plus its media item (the media search only lists classified
    /// movies) on the shared E2E source and returns the movie id.</summary>
    private async Task<long> SeedMovieWithItemAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var sourceId = await EnsureMediaSourceIdAsync(db);
        var collection = new MediaCollection
        {
            Name = $"movies-{Guid.NewGuid():N}",
            MediaSourceId = sourceId,
            Path = $"/test/m-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        var movie = new Movie { Name = name, MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        var item = new MediaItem
        {
            Name = $"{name}.mkv",
            Path = $"{collection.Path}/{name}.mkv",
            MediaCollectionId = collection.Id,
            Duration = TimeSpan.FromMinutes(76),
            DurationProbedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);
        db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = item.Id });
        await db.SaveChangesAsync(ct);
        return movie.Id;
    }

    private Task OpenClipGroupsTabAsync() => SwitchTabAsync("#tvsender-tab-clipgruppen", "#tab-clipgruppen");

    private Task OpenSendeplanTabAsync() => SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");

    [Fact]
    public async Task ClipGroupsTab_CreateRenameDeleteGroup()
    {
        await EnableTvSenderAsync();
        await SeedTrailerClipGroupAsync();
        await OpenAdminPageAsAdminAsync();
        await OpenClipGroupsTabAsync();

        await Page.Locator("#clipgroup-new").ClickAsync();
        await Page.Locator("#clipgroup-name").FillAsync("Maskottchen");
        await Page.Locator("#clipgroup-save").ClickAsync();

        var row = Page.Locator(".clipgroup-row", new PageLocatorOptions { HasText = "Maskottchen" });
        await Expect(row).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await row.Locator(".clipgroup-rename").ClickAsync();
        await Page.Locator("#clipgroup-name").FillAsync("Funki");
        await Page.Locator("#clipgroup-save").ClickAsync();
        await Expect(Page.Locator(".clipgroup-row", new PageLocatorOptions { HasText = "Funki" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        await Page.Locator(".clipgroup-row", new PageLocatorOptions { HasText = "Funki" }).Locator(".clipgroup-delete").ClickAsync();
        await Page.Locator("#clipgroup-delete-confirm").ClickAsync();
        await Expect(Page.Locator(".clipgroup-row", new PageLocatorOptions { HasText = "Funki" }))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
    }

    [Fact]
    public async Task ClipGroupsTab_TrailerGroupVisible_ButProtected()
    {
        await EnableTvSenderAsync();
        await SeedTrailerClipGroupAsync();
        await OpenAdminPageAsAdminAsync();
        await OpenClipGroupsTabAsync();

        var trailerRow = Page.Locator(".clipgroup-row", new PageLocatorOptions { HasText = "Trailer" });
        await Expect(trailerRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
        await Expect(trailerRow.Locator(".clipgroup-delete")).ToHaveCountAsync(0);
        await Expect(trailerRow.Locator(".clipgroup-rename")).ToHaveCountAsync(0);

        // „Ansehen" erklärt die dynamische Befüllung statt eine leere Tabelle zu zeigen.
        await trailerRow.Locator(".clipgroup-manage").ClickAsync();
        await Expect(Page.Locator("#clipgroup-system-hint")).ToContainTextAsync("Planungszeit",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        await Expect(Page.Locator("#clipgroup-members-card .clipgroup-member-row")).ToHaveCountAsync(0);

        // A user group named "Trailer" is rejected by the service.
        await Page.Locator("#clipgroup-new").ClickAsync();
        await Page.Locator("#clipgroup-name").FillAsync("trailer");
        await Page.Locator("#clipgroup-save").ClickAsync();
        await Expect(Page.Locator("#clipgroup-edit-error")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
    }

    [Fact]
    public async Task ClipGroupsTab_AssignAndRemoveClipViaCandidates()
    {
        await EnableTvSenderAsync();
        await SeedTrailerClipGroupAsync();
        var groupId = await SeedClipGroupAsync("E2E-Clips");
        var clipIds = await SeedClipItemsAsync(
            ("e2e-clip-eins.mkv", TimeSpan.FromMinutes(1)),
            ("e2e-clip-ohne-laufzeit.mkv", null));
        await OpenAdminPageAsAdminAsync();
        await OpenClipGroupsTabAsync();

        await Page.Locator($".clipgroup-row[data-group-id='{groupId}'] .clipgroup-manage").ClickAsync();
        await Expect(Page.Locator("#clipgroup-members-card")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        var candidate = Page.Locator($".clip-candidate-row[data-media-item-id='{clipIds[0]}']");
        await Expect(candidate).ToContainTextAsync("e2e-clip-eins.mkv", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        var noDurCandidate = Page.Locator($".clip-candidate-row[data-media-item-id='{clipIds[1]}']");
        await Expect(noDurCandidate).ToContainTextAsync("keine Laufzeit");

        // Hinzufügen → Mitgliedszeile erscheint, der Kandidat wird als Mitglied markiert.
        await candidate.Locator(".clip-candidate-add").ClickAsync();
        var member = Page.Locator($".clipgroup-member-row[data-media-item-id='{clipIds[0]}']");
        await Expect(member).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
        await Expect(candidate.Locator(".clip-candidate-member")).ToContainTextAsync("Bereits zugeordnet");
        await Expect(candidate.Locator(".clip-candidate-add")).ToHaveCountAsync(0);

        // Auch ein Clip ohne Laufzeit lässt sich zuordnen und ist entsprechend markiert.
        await noDurCandidate.Locator(".clip-candidate-add").ClickAsync();
        var noDurMember = Page.Locator($".clipgroup-member-row[data-media-item-id='{clipIds[1]}']");
        await Expect(noDurMember).ToContainTextAsync("keine Laufzeit", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        // Entfernen → Mitgliedszeile verschwindet, der Kandidat ist wieder hinzufügbar.
        await member.Locator(".clipgroup-member-remove").ClickAsync();
        await Expect(Page.Locator($".clipgroup-member-row[data-media-item-id='{clipIds[0]}']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
        await Expect(candidate.Locator(".clip-candidate-add")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RuleEditor_ShowsClipGroupSelectionAndGraceField()
    {
        await EnableTvSenderAsync();
        await SeedTrailerClipGroupAsync();
        var groupId = await SeedClipGroupAsync("Maskottchen");
        // Die Mediensuche liefert nur klassifizierte Filme → Film mit MediaItem säen.
        var movieId = await SeedMovieWithItemAsync("E2E-Film");
        await OpenAdminPageAsAdminAsync();
        await DriveUntilStateAsync(
            async () =>
            {
                var editor = Page.Locator("#rule-editor");
                if (await editor.CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.WaitForSelectorAsync("#rule-editor", new PageWaitForSelectorOptions { Timeout = 10_000 }));

        await Expect(Page.Locator("#rule-clip-groups")).ToBeVisibleAsync();
        await Expect(Page.Locator(".rule-clip-group-option")).ToHaveCountAsync(2);
        // Ohne Sendeplatz-Länge gibt es kein Karenzzeit-Feld — der Hinweis erklärt die Kopplung.
        await Expect(Page.Locator("#rule-grace-minutes")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#rule-clip-groups-hint")).ToBeVisibleAsync();

        // Regel mit Gruppen-Mehrfachauswahl + Karenzzeit speichern und in der Liste sehen.
        await Page.FillAsync("#rule-name", "Primetime-Clips");
        await Page.SelectOptionAsync("#rule-type", "MoviePrimeTime");
        await Page.FillAsync("#rule-duration", "240");
        await Page.Locator("#rule-duration").BlurAsync();
        // Ohne gewählte Clip-Gruppe bleibt das Karenzzeit-Feld wirkungslos und verborgen;
        // der Hinweis erklärt die Kopplung an die Gruppen-Auswahl.
        await Expect(Page.Locator("#rule-grace-minutes")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#rule-clip-groups-hint")).ToBeVisibleAsync();
        await Page.Locator($"#rule-clip-group-{groupId}").CheckAsync();
        await Expect(Page.Locator("#rule-grace-minutes")).ToBeVisibleAsync();
        await Expect(Page.Locator("#rule-grace-minutes-hint")).ToBeVisibleAsync();
        var graceInput = await Page.Locator("#rule-grace-minutes").InputValueAsync();
        Assert.Equal("15", graceInput);

        await DriveUntilStateAsync(
            async () =>
            {
                var sel = Page.Locator(".media-search-input");
                if (await sel.CountAsync() > 0)
                    await sel.FillAsync("E2E-Film", new LocatorFillOptions { Timeout = 10_000 });
            },
            () => Page.Locator($".media-search-result[data-media-type='Movie'][data-media-id='{movieId}']")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
        await Page.Locator($".media-search-result[data-media-type='Movie'][data-media-id='{movieId}']").ClickAsync();
        await Page.Locator("#rule-material-selected").WaitForAsync();

        await Page.Locator($"#rule-clip-group-{ProgramClipGroup.TrailerGroupId}").CheckAsync();
        await Page.FillAsync("#rule-grace-minutes", "10");
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Primetime-Clips" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row).ToContainTextAsync("Clips: Maskottchen");
        await Expect(row).ToContainTextAsync("Trailer");

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ct = TestContext.Current.CancellationToken;
            var rule = await db.ProgramRules.Include(r => r.ClipGroups).SingleAsync(r => r.Name == "Primetime-Clips", ct);
            Assert.Equal(new[] { ProgramClipGroup.TrailerGroupId, groupId },
                rule.ClipGroups.Select(c => c.ProgramClipGroupId).OrderBy(x => x).ToArray());
            Assert.Equal(TimeSpan.FromMinutes(10), rule.PlaceholderGrace);
        }
    }

    [Fact]
    public async Task GeneratedSchedule_ContainsVisibleClipSlots()
    {
        await EnableTvSenderAsync();
        await SeedTrailerClipGroupAsync();
        long ruleId;
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ct = TestContext.Current.CancellationToken;
            var sourceId = await EnsureMediaSourceIdAsync(db);
            var collection = new MediaCollection { Name = "C", MediaSourceId = sourceId, Path = "/c", CreatedAt = DateTime.UtcNow };
            db.MediaCollections.Add(collection);
            var movie = new Movie { Name = "E2E-Film", MediaSourceId = sourceId, CreatedAt = DateTime.UtcNow };
            db.Movies.Add(movie);
            await db.SaveChangesAsync(ct);
            var movieItem = new MediaItem { Name = "m.mkv", Path = "/c/m.mkv", MediaCollectionId = collection.Id, Duration = TimeSpan.FromMinutes(76), DurationProbedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.MediaItems.Add(movieItem);
            await db.SaveChangesAsync(ct);
            db.MovieMediaItems.Add(new MovieMediaItem { MovieId = movie.Id, MediaItemId = movieItem.Id });
            var channel = await new ProgramRuleService(db).GetOrCreateChannelAsync(ct);
            var rule = new ProgramRule
            {
                TVChannelId = channel.Id,
                Name = "Primetime",
                RuleType = ProgramRuleType.MoviePrimeTime,
                Recurrence = ProgramRuleRecurrence.Daily,
                TimeOfDay = new TimeOnly(20, 0),
                SlotDuration = TimeSpan.FromHours(4),
                MaterialType = ProgramRuleMaterialType.Movie,
                MaterialId = movie.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ProgramRules.Add(rule);
            await db.SaveChangesAsync(ct);
            ruleId = rule.Id;
        }
        var clipIds = await SeedClipItemsAsync(("funki-clip.mkv", TimeSpan.FromSeconds(60)));
        var groupId = await SeedClipGroupAsync("E2E-Clips");
        await SeedClipGroupItemAsync(groupId, clipIds[0]);
        await SeedRuleClipGroupLinkAsync(ruleId, groupId);

        await OpenAdminPageAsAdminAsync();
        await OpenSendeplanTabAsync();

        // „Jetzt generieren" weckt den Hintergrund-Worker über das echte Signal — die
        // Slot-Karte erscheint, sobald der Lauf durch ist (erneut klicken, falls nötig).
        var clipCard = Page.Locator(".tvsender-slot-row", new PageLocatorOptions { HasText = "funki-clip.mkv" });
        await DriveUntilStateAsync(
            async () =>
            {
                if (await clipCard.CountAsync() > 0)
                    return;
                var btn = Page.Locator("#tvsender-generate-now");
                if (await btn.CountAsync() > 0 && await btn.IsEnabledAsync())
                    await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => clipCard.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        // Sichtbare Slot-Karten: der Clip nutzt den MediaItem.Name-Fallback (kein Film/Episode).
        await Expect(clipCard.First.Locator(".program-title")).ToHaveTextAsync("funki-clip.mkv");
        await Expect(Page.Locator(".tvsender-slot-row .program-title", new PageLocatorOptions { HasText = "E2E-Film" }).First)
            .ToBeVisibleAsync();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ct = TestContext.Current.CancellationToken;
            Assert.True(await db.ProgramSlots.AnyAsync(s => s.MediaItemId == clipIds[0] && s.ProgramRuleId == ruleId, ct));
        }
    }
}
