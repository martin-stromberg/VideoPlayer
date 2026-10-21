using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the TV channel administration page <c>/admin/tv-sender</c>: the
/// consolidated rule lifecycle (create via search dialog, edit, deactivate, delete) and the
/// admin-only access gate.
/// </summary>
public sealed class TVSenderAdminE2ETests : PlaylistsE2ETestBase
{
    private async Task OpenAdminPageAsAdminAsync()
    {
        await MakeUserAdminAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-name", new PageWaitForSelectorOptions { Timeout = 30_000 });
    }

    [Fact]
    public async Task Admin_CreateEditDeactivateDeleteRule()
    {
        if (SkipBrowser) return;

        var showId = await SeedTvShowWithSeasonsAsync("E2E Serie", ("Staffel 1", 2));
        await OpenAdminPageAsAdminAsync();

        // Channel settings: set schedule horizon + generation interval + live stream
        // settings (segment length, segment directory, retention), save.
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

        // Create a rule: open the dialog, fill fields, pick material via the search selector.
        await DriveUntilStateAsync(
            async () =>
            {
                if (await Page.Locator("#rule-name").CountAsync() > 0) return;
                var btn = Page.Locator("#tvsender-new-rule");
                if (await btn.CountAsync() > 0) await btn.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            },
            () => Page.Locator("#rule-name").WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 }));

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

        // "Jetzt generieren" wakes the schedule worker immediately and reports back.
        await Page.ClickAsync("#tvsender-generate-now");
        await Expect(Page.Locator("#tvsender-schedule-status")).ToContainTextAsync("aktualisiert",
            new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });

        // Deactivate then reactivate.
        await Page.Locator(".tvsender-rule-toggle").ClickAsync();
        await Expect(Page.Locator(".tvsender-rule-row")).ToContainTextAsync("Inaktiv");

        // Delete with confirmation.
        await Page.Locator(".tvsender-rule-delete").ClickAsync();
        await Page.Locator("#rule-delete-confirm").WaitForAsync();
        await Page.ClickAsync("#rule-delete-confirm");
        await Expect(Page.Locator(".tvsender-rule-row")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#tvsender-rules-status")).ToContainTextAsync("gelöscht");

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
        var displacedRow = Page.Locator(".tvsender-slot-row.tvsender-slot-displaced");
        await displacedRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Expect(displacedRow).ToContainTextAsync("Verdrängt");
        await Expect(Page.Locator(".tvsender-slot-row").First).ToContainTextAsync("Geplant");
    }

    [Fact]
    public async Task Admin_GenreRule_MultiSelectAndSourceFilter()
    {
        if (SkipBrowser) return;

        // Two visible genres plus one hidden genre (must not be selectable) on the shared source.
        long genreAId, genreBId, hiddenId, sourceId;
        string sourceName;
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VideoWebPlayer.Data.ApplicationDbContext>();
            var source = await db.MediaSources.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            if (source is null)
            {
                source = new VideoWebPlayer.Data.MediaSource { Name = "E2E Test Source", Host = "127.0.0.1", Port = 22, Path = "/test" };
                db.MediaSources.Add(source);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            sourceId = source.Id;
            sourceName = source.Name;
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

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        await Expect(Page.Locator("text=Nicht autorisiert")).ToBeVisibleAsync();
        Assert.Equal(0, await Page.Locator("#tvsender-new-rule").CountAsync());
    }
}
