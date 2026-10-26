using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the TV channel administration page <c>/admin/tv-sender</c>: the
/// consolidated rule lifecycle (create via search dialog, edit, deactivate, delete) and the
/// admin-only access gate.
/// </summary>
public sealed class TVSenderAdminE2ETests : TVSenderAdminE2ETestBase
{
    // Attempt count that marks a permanently failed item — taken from the options so the
    // seeds stay "failed" even when the MediaProbeOptions default changes.
    private static readonly int FailedProbeAttempts = new MediaProbeOptions().EffectiveMaxAttempts;

    // Duration-probe seeds shared by the progress and reset tests: one successfully probed
    // item, two permanently failed ones (the second for the bulk reset; its older timestamp
    // keeps it last in the rolling recent list, which orders by DurationProbedAt descending)
    // and one pending retry item whose fresh attempt timestamp keeps it pending for the
    // whole test run — so the background probe worker cannot change the expected numbers.
    private async Task SeedProbeItemsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var sourceId = await EnsureMediaSourceIdAsync(db);

        var collection = new VideoWebPlayer.Data.MediaCollection
        {
            Name = "probe",
            Path = "/test/probe",
            MediaSourceId = sourceId,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        db.MediaItems.Add(new VideoWebPlayer.Data.MediaItem
        {
            Name = "e2e-fertig.mkv",
            Path = "/test/probe/e2e-fertig.mkv",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(90),
            DurationProbedAt = DateTime.UtcNow
        });
        db.MediaItems.Add(new VideoWebPlayer.Data.MediaItem
        {
            Name = "e2e-defekt.mkv",
            Path = "/test/probe/e2e-defekt.mkv",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            DurationProbedAt = DateTime.UtcNow.AddMinutes(-30),
            DurationProbeAttempts = FailedProbeAttempts
        });
        db.MediaItems.Add(new VideoWebPlayer.Data.MediaItem
        {
            Name = "e2e-defekt-2.mkv",
            Path = "/test/probe/e2e-defekt-2.mkv",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            DurationProbedAt = DateTime.UtcNow.AddMinutes(-60),
            DurationProbeAttempts = FailedProbeAttempts
        });
        db.MediaItems.Add(new VideoWebPlayer.Data.MediaItem
        {
            Name = "e2e-wartet.mkv",
            Path = "/test/probe/e2e-wartet.mkv",
            MediaCollectionId = collection.Id,
            CreatedAt = DateTime.UtcNow,
            DurationProbedAt = DateTime.UtcNow.AddMinutes(-1),
            DurationProbeAttempts = 1
        });
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Admin_CreateEditDeactivateDeleteRule()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();

        var showId = await SeedTvShowWithSeasonsAsync("E2E Serie", ("Staffel 1", 2));
        await SeedProbeItemsAsync();

        await OpenAdminPageAsAdminAsync();

        // Tab structure: five tabs in the mockup order, "Regeln" initially active.
        var tabs = Page.Locator(".admin-tab");
        await Expect(tabs).ToHaveCountAsync(5);
        await Expect(tabs.Nth(0)).ToContainTextAsync("Regeln");
        await Expect(tabs.Nth(1)).ToContainTextAsync("Sendeplan");
        await Expect(tabs.Nth(2)).ToContainTextAsync("Einstellungen");
        await Expect(tabs.Nth(3)).ToContainTextAsync("Laufzeitermittlung");
        await Expect(tabs.Nth(4)).ToContainTextAsync("Clip-Gruppen");
        await Expect(Page.Locator("#tab-regeln.active")).ToBeVisibleAsync();
        await Expect(Page.Locator("#tvsender-new-rule")).ToBeVisibleAsync();

        // Laufzeitermittlung tab: counters, rolling list and the expandable detail tables.
        await SwitchTabAsync("#tvsender-tab-laufzeit", "#tab-laufzeit");
        await Expect(Page.Locator("#tvsender-probe-card")).ToContainTextAsync("Laufzeitermittlung");
        await Expect(Page.Locator("#tvsender-probe-probed")).ToContainTextAsync("1 ermittelt");
        await Expect(Page.Locator("#tvsender-probe-pending")).ToContainTextAsync("1 ausstehend");
        await Expect(Page.Locator("#tvsender-probe-failed")).ToContainTextAsync("2 fehlgeschlagen");
        var probeRows = Page.Locator(".tvsender-probe-row");
        await Expect(probeRows).ToHaveCountAsync(4);
        await Expect(probeRows.First).ToContainTextAsync("e2e-fertig.mkv");
        await Expect(probeRows.Nth(2)).ToContainTextAsync("e2e-defekt.mkv");
        await Expect(probeRows.Nth(2)).ToContainTextAsync("keine Laufzeit");
        await Expect(probeRows.Nth(3)).ToContainTextAsync("e2e-defekt-2.mkv");

        // Detail lists: the chips carry "▾ Details" toggles revealing the file tables.
        await Page.ClickAsync("#tvsender-probe-pending-details");
        var pendingDetail = Page.Locator("#tvsender-probe-pending-detail[open]");
        await Expect(pendingDetail).ToBeVisibleAsync();
        var pendingRows = pendingDetail.Locator(".tvsender-probe-detail-row");
        await Expect(pendingRows).ToHaveCountAsync(1);
        await Expect(pendingRows.First).ToContainTextAsync("e2e-wartet.mkv");
        // Versuche-Zelle gezielt mit exaktem Vergleich — ein Substring-Match auf die Zeile
        // könnte die Zahl auch aus dem Zeitstempel der Spalte "Letzter Versuch" beziehen.
        await Expect(pendingRows.First.Locator("td").Nth(2)).ToHaveTextAsync("1");

        await Page.ClickAsync("#tvsender-probe-failed-details");
        var failedDetail = Page.Locator("#tvsender-probe-failed-detail[open]");
        await Expect(failedDetail).ToBeVisibleAsync();
        var failedRows = failedDetail.Locator(".tvsender-probe-detail-row");
        await Expect(failedRows).ToHaveCountAsync(2);
        await Expect(failedRows.First).ToContainTextAsync("e2e-defekt.mkv");
        await Expect(failedRows.First.Locator("td").Nth(2)).ToHaveTextAsync(FailedProbeAttempts.ToString());
        await Expect(failedRows.Nth(1)).ToContainTextAsync("e2e-defekt-2.mkv");

        // Channel settings (Einstellungen tab): set schedule horizon + generation interval +
        // live stream settings (segment length, segment directory, retention), save.
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Page.FillAsync("#tvsender-horizon-hours", "72");
        await Page.FillAsync("#tvsender-interval-minutes", "30");
        await Page.FillAsync("#tvsender-segment-length-seconds", "10");
        await Page.FillAsync("#tvsender-segment-directory", "");
        await Page.FillAsync("#tvsender-segment-retention-minutes", "45");
        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gespeichert");

        // The stream settings round-tripped into the persisted setup row.
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            var setup = await db.Setups.FirstAsync(TestContext.Current.CancellationToken);
            Assert.Equal(10, setup.LiveStreamSegmentLengthSeconds);
            Assert.Null(setup.LiveStreamSegmentDirectory);
            Assert.Equal(45, setup.LiveStreamSegmentRetentionMinutes);
        }

        // Create a rule (Regeln tab): open the collapsible editor card, fill fields,
        // pick material via the search selector.
        await SwitchTabAsync("#tvsender-tab-regeln", "#tab-regeln");
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        // The editor is an overlay dialog floating above the rule list (Schritt 23, N21).
        await Expect(Page.Locator("#rule-editor")).ToBeVisibleAsync();
        await Expect(Page.Locator(".admin-dialog-overlay:has(#rule-editor)")).ToBeVisibleAsync();

        await Page.FillAsync("#rule-name", "Abendserie");
        await Page.FillAsync("#rule-episodes", "1");
        await Page.FillAsync("#rule-priority", "3");

        await DriveUntilStateAsync(
            async () =>
            {
                var sel = Page.Locator(".media-search-input");
                if (await sel.CountAsync() > 0) await sel.FillAsync("E2E Serie", new LocatorFillOptions { Timeout = 10_000 });
            },
            () => Page.Locator($".media-search-result[data-media-type='TVShow'][data-media-id='{showId}'], .media-search-error, .media-search-empty")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        Assert.True(
            await Page.Locator($".media-search-result[data-media-id='{showId}']").CountAsync() > 0,
            "Suchergebnis fehlt. Dialogzustand: " + await Page.Locator("#rule-material-picker").InnerHTMLAsync());

        await Page.Locator($".media-search-result[data-media-type='TVShow'][data-media-id='{showId}']").ClickAsync();
        await Page.Locator("#rule-material-selected").WaitForAsync();
        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Abendserie" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row).ToContainTextAsync("Serien-Sendezeit");
        await Expect(row).ToContainTextAsync("Serie: E2E Serie");

        // Edit: rename via the edit dialog.
        await row.Locator(".tvsender-rule-edit").ClickAsync();
        await Page.Locator("#rule-name").WaitForAsync();
        await Page.FillAsync("#rule-name", "Abendserie II");
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator(".tvsender-rule-row")).ToContainTextAsync("Abendserie II");

        // Planning diagnosis: the row explains why nothing is schedulable yet — the seeded
        // episodes have no media items at all.
        await Expect(row.Locator(".tvsender-rule-diagnosis")).ToContainTextAsync("ohne Mediendatei");

        // "Jetzt generieren" (Sendeplan tab) wakes the schedule worker immediately and reports back.
        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");
        await Page.ClickAsync("#tvsender-generate-now");
        await Expect(Page.Locator("#tvsender-schedule-status")).ToContainTextAsync("aktualisiert",
            new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });

        // Deactivate then reactivate (the rule actions live in the Regeln tab).
        await SwitchTabAsync("#tvsender-tab-regeln", "#tab-regeln");
        await Page.Locator(".tvsender-rule-toggle").ClickAsync();
        await Expect(Page.Locator(".tvsender-rule-row")).ToContainTextAsync("Inaktiv");

        // Delete with confirmation.
        await Page.Locator(".tvsender-rule-delete").ClickAsync();
        await Page.Locator("#rule-delete-confirm").WaitForAsync();
        await Page.ClickAsync("#rule-delete-confirm");
        await Expect(Page.Locator(".tvsender-rule-row")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#tvsender-rules-status")).ToContainTextAsync("gelöscht");

        // Second rule on the same app instance: a Filler rule whose material is a clip source —
        // picked through the media-source dropdown that only appears for the Filler rule type.
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));
        await Page.FillAsync("#rule-name", "Funki-Füller");
        await Page.FillAsync("#rule-episodes", "");
        await Page.FillAsync("#rule-duration", "30");
        await Page.SelectOptionAsync("#rule-type", "Filler");
        await Page.Locator("#rule-material-clipsource").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        // Pick whichever media source the dropdown offers first — names differ per test seed.
        var firstClipOption = Page.Locator("#rule-material-clipsource option").Nth(1);
        var clipOptionText = (await firstClipOption.TextContentAsync())!.Trim();
        var clipOptionValue = await firstClipOption.GetAttributeAsync("value");
        await Page.Locator("#rule-material-clipsource").SelectOptionAsync(
            new SelectOptionValue { Label = clipOptionText });
        await Expect(Page.Locator("#rule-material-selected")).ToContainTextAsync(clipOptionText);
        await Page.ClickAsync("#rule-save");

        var clipRow = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Funki-Füller" });
        await clipRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(clipRow).ToContainTextAsync("Platzhalter/Füller");
        await Expect(clipRow).ToContainTextAsync(clipOptionText);

        // Persisted server-side as MediaSourceClips material on the chosen source.
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            var clipRule = await db.ProgramRules.SingleAsync(r => r.Name == "Funki-Füller", TestContext.Current.CancellationToken);
            Assert.Equal(VideoWebPlayer.Data.ProgramRuleMaterialType.MediaSourceClips, clipRule.MaterialType);
            Assert.Equal(long.Parse(clipOptionValue!), clipRule.MaterialId);
        }

        // Clean up so the rest of the test sees an empty rule list.
        await clipRow.Locator(".tvsender-rule-delete").ClickAsync();
        await Page.Locator("#rule-delete-confirm").WaitForAsync();
        await Page.ClickAsync("#rule-delete-confirm");
        await Expect(Page.Locator(".tvsender-rule-row")).ToHaveCountAsync(0);

        // Sendeplan-Karte: geplanter und verdrängter Sendeplatz werden mit Status-Chip angezeigt.
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            var channel = await db.TVChannels.FirstAsync(TestContext.Current.CancellationToken);
            db.ProgramSlots.Add(new VideoWebPlayer.Data.ProgramSlot
            {
                TVChannelId = channel.Id,
                Start = DateTime.UtcNow.AddHours(2),
                End = DateTime.UtcNow.AddHours(3),
                MediaItemId = 1,
                MovieId = 1,
                RuleType = VideoWebPlayer.Data.ProgramRuleType.Filler
            });
            db.ProgramSlots.Add(new VideoWebPlayer.Data.ProgramSlot
            {
                TVChannelId = channel.Id,
                Start = DateTime.UtcNow.AddHours(3),
                End = DateTime.UtcNow.AddHours(4),
                MediaItemId = 2,
                MovieId = 2,
                RuleType = VideoWebPlayer.Data.ProgramRuleType.SeriesFixedTime,
                Status = VideoWebPlayer.Data.ProgramSlotStatus.Displaced
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-tab-sendeplan");
        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");
        var displacedRow = Page.Locator(".tvsender-slot-row.tvsender-slot-displaced");
        await displacedRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(displacedRow).ToContainTextAsync("Verdrängt");
        await Expect(Page.Locator(".tvsender-slot-row").First).ToContainTextAsync("Geplant");
        // Panel-Karten statt Tabelle: Poster, Zeit, Titel und Beschreibung je Sendeplatz.
        Assert.True(await Page.Locator("#tvsender-schedule-cards .program-card").CountAsync() >= 2);
        await Expect(Page.Locator("#tvsender-schedule-cards .program-card .program-poster img").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_ProbeReset_SingleItemAndAllFailed()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        await SeedProbeItemsAsync();

        await OpenAdminPageAsAdminAsync();
        await SwitchTabAsync("#tvsender-tab-laufzeit", "#tab-laufzeit");

        // Der Massen-Reset ist auch ohne Aufklappen der Detail-Liste erreichbar.
        await Expect(Page.Locator("#tvsender-probe-reset-all")).ToBeVisibleAsync();

        await Page.ClickAsync("#tvsender-probe-failed-details");
        var failedDetail = Page.Locator("#tvsender-probe-failed-detail[open]");
        await Expect(failedDetail).ToBeVisibleAsync();
        var failedRows = failedDetail.Locator(".tvsender-probe-detail-row");
        await Expect(failedRows).ToHaveCountAsync(2);

        // Einzel-Reset: der Zeilen-Button nimmt genau dieses Item wieder in die Ermittlung auf.
        // Assertions zielen auf den Failed-Chip — ein Probe-Worker-Tick kann das zurückgesetzte
        // Item zwischendurch erneut proben (transient bleibt es ausstehend).
        var defektRow = failedRows.Filter(new LocatorFilterOptions { HasText = "e2e-defekt.mkv" });
        await defektRow.Locator(".tvsender-probe-reset").ClickAsync();
        await Expect(Page.Locator("#tvsender-probe-action-status")).ToContainTextAsync("zurückgesetzt",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        await Expect(Page.Locator("#tvsender-probe-failed")).ToContainTextAsync("1 fehlgeschlagen");
        await Expect(Page.Locator("#tvsender-probe-pending")).ToContainTextAsync("2 ausstehend");
        await Expect(failedRows).ToHaveCountAsync(1);
        await Expect(failedRows.First).ToContainTextAsync("e2e-defekt-2.mkv");

        // Massen-Reset über den Bestätigungsdialog: alle fehlgeschlagenen Items zurücksetzen.
        await Page.ClickAsync("#tvsender-probe-reset-all");
        await Expect(Page.Locator(".admin-dialog-overlay .admin-dialog")).ToBeVisibleAsync();
        await Page.ClickAsync("#probe-reset-all-confirm");
        await Expect(Page.Locator("#tvsender-probe-failed")).ToContainTextAsync("0 fehlgeschlagen",
            new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        await Expect(Page.Locator("#tvsender-probe-action-status")).ToContainTextAsync("1 Datei(en)");
        Assert.Equal(0, await Page.Locator("#tvsender-probe-failed-detail").CountAsync());
    }

    [Fact]
    public async Task Admin_GenreRule_MultiSelectAndSourceFilter()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();

        // Two visible genres plus one hidden genre (must not be selectable) on the shared source.
        long genreAId, genreBId, hiddenId, sourceId;
        string sourceName;
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            sourceId = await EnsureMediaSourceIdAsync(db);
            sourceName = await db.MediaSources.Where(s => s.Id == sourceId)
                .Select(s => s.Name).SingleAsync(TestContext.Current.CancellationToken);
            var a = new VideoWebPlayer.Data.Genre { Name = "E2E Action", MediaSourceId = sourceId };
            var b = new VideoWebPlayer.Data.Genre { Name = "E2E Komödie", MediaSourceId = sourceId };
            var hidden = new VideoWebPlayer.Data.Genre { Name = "E2E Versteckt", MediaSourceId = sourceId, IsHidden = true };
            db.Genres.AddRange(a, b, hidden);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            (genreAId, genreBId, hiddenId) = (a.Id, b.Id, hidden.Id);
        }

        await OpenAdminPageAsAdminAsync();

        // Create a rule: switch to Genre-/Formatrotation, pick two genres + restrict the source.
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

        await Page.FillAsync("#rule-name", "Genreabend");
        await Page.FillAsync("#rule-episodes", "2");
        await Page.SelectOptionAsync("#rule-type", "GenreRotation");
        await Page.Locator("#rule-material-genre").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });

        // Hidden genre is not offered; both visible genres can be selected together.
        Assert.Equal(0, await Page.Locator($"#rule-genre-{hiddenId}").CountAsync());
        await Page.CheckAsync($"#rule-genre-{genreAId}");
        await Page.CheckAsync($"#rule-genre-{genreBId}");
        await Page.CheckAsync($"#rule-source-{sourceId}");

        await Page.ClickAsync("#rule-save");

        var row = Page.Locator(".tvsender-rule-row", new PageLocatorOptions { HasText = "Genreabend" });
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(row).ToContainTextAsync("Genre-/Formatrotation");
        await Expect(row).ToContainTextAsync("Genre: E2E Action, E2E Komödie");
        await Expect(row).ToContainTextAsync($"Quellen: {sourceName}");

        // Join rows persisted server-side.
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            var rule = await db.ProgramRules.SingleAsync(r => r.Name == "Genreabend", TestContext.Current.CancellationToken);
            var links = await db.ProgramRuleGenres.Where(g => g.ProgramRuleId == rule.Id).Select(g => g.GenreId).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new HashSet<long>([genreAId, genreBId]), links.ToHashSet());
            Assert.Single(await db.ProgramRuleMediaSources.Where(s => s.ProgramRuleId == rule.Id).ToListAsync(TestContext.Current.CancellationToken));
        }

        // Edit: the stored multi-selection is shown checked again.
        await row.Locator(".tvsender-rule-edit").ClickAsync();
        await Page.Locator("#rule-name").WaitForAsync();
        Assert.True(await Page.Locator($"#rule-genre-{genreAId}").IsCheckedAsync());
        Assert.True(await Page.Locator($"#rule-genre-{genreBId}").IsCheckedAsync());
        Assert.True(await Page.Locator($"#rule-source-{sourceId}").IsCheckedAsync());
        await Page.ClickAsync("#rule-save");
        await Expect(Page.Locator("#tvsender-rules-status")).ToContainTextAsync("gespeichert");
    }

    [Fact]
    public async Task NonAdmin_SeesNotAuthorized()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        await Expect(Page.Locator("text=Nicht autorisiert")).ToBeVisibleAsync();
        Assert.Equal(0, await Page.Locator("#tvsender-new-rule").CountAsync());
    }

    /// <summary>
    /// Drives the settings tab's standby card: picks a seeded unclassified clip from the
    /// candidate table and verifies the persisted LiveStreamStandbyMediaItemId plus the
    /// chip label. One browser run because Playwright tests are expensive.
    /// </summary>
    [Fact]
    public async Task Admin_StandbyClip_PickAndPersist()
    {
        await EnableTvSenderAsync();
        var clipIds = await SeedClipItemsAsync(("standby-wahl.mkv", TimeSpan.FromSeconds(30)));
        await OpenAdminPageAsAdminAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");

        await Expect(Page.Locator("#tvsender-standby-current")).ToContainTextAsync("Standard-Clip");
        await Page.ClickAsync("#tvsender-standby-change");
        var row = Page.Locator($".standby-candidate-row[data-media-item-id='{clipIds[0]}']");
        await Expect(row).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
        await row.Locator(".standby-candidate-pick").ClickAsync();
        await Expect(Page.Locator("#tvsender-standby-current")).ToContainTextAsync("standby-wahl");

        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gespeichert", new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setup = await db.Setups.FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal(clipIds[0], setup.LiveStreamStandbyMediaItemId);
    }
}
