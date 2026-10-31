using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Playwright E2E tests for the youth-protection settings of the TV channel administration
/// page: the four window times and the unknown-rating mode persist over save+reload, invalid
/// schemas show an error, the rule diagnosis names the window reason and window-rated content
/// only lands in the night window.
/// </summary>
public sealed class TVSenderAdminE2ETests_AgeWindow : TVSenderAdminE2ETestBase
{
    private Task SetUnknownModeAsync(ProgramScheduleUnknownRatingMode mode)
        => UpdateSetupAsync(s => s.ProgramScheduleUnknownRatingMode = mode);

    [Fact]
    public async Task Admin_AgeWindows_SetAndPersist()
    {
        await EnableTvSenderAsync();
        await OpenAdminPageAsAdminAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");

        await Page.FillAsync("#tvsender-agewindow-12", "19:30");
        await Page.FillAsync("#tvsender-agewindow-16", "21:00");
        await Page.FillAsync("#tvsender-agewindow-18", "22:30");
        await Page.FillAsync("#tvsender-agewindow-nightend", "05:30");
        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gespeichert");

        await Page.ReloadAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Expect(Page.Locator("#tvsender-agewindow-12")).ToHaveValueAsync("19:30");
        await Expect(Page.Locator("#tvsender-agewindow-16")).ToHaveValueAsync("21:00");
        await Expect(Page.Locator("#tvsender-agewindow-18")).ToHaveValueAsync("22:30");
        await Expect(Page.Locator("#tvsender-agewindow-nightend")).ToHaveValueAsync("05:30");
    }

    [Fact]
    public async Task Admin_UnknownRatingMode_SelectAndPersist()
    {
        await EnableTvSenderAsync();
        await OpenAdminPageAsAdminAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");

        // The delivered default is the restrictive night-window behavior.
        await Expect(Page.Locator("#tvsender-unknown-rating")).ToHaveValueAsync("RestrictToNightWindow");

        await Page.SelectOptionAsync("#tvsender-unknown-rating", "Unrestricted");
        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("gespeichert");

        await Page.ReloadAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");
        await Expect(Page.Locator("#tvsender-unknown-rating")).ToHaveValueAsync("Unrestricted");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(ProgramScheduleUnknownRatingMode.Unrestricted,
            (await db.Setups.FirstAsync(TestContext.Current.CancellationToken)).ProgramScheduleUnknownRatingMode);
    }

    [Fact]
    public async Task Admin_AgeWindows_InvalidInput_ShowsError()
    {
        await EnableTvSenderAsync();
        await OpenAdminPageAsAdminAsync();
        await SwitchTabAsync("#tvsender-tab-einstellungen", "#tab-einstellungen");

        // Fsk16 before Fsk12 violates the monotone ordering — the save is rejected and the
        // status line carries the validation message.
        await Page.FillAsync("#tvsender-agewindow-16", "18:00");
        await Page.ClickAsync("#tvsender-save-settings");
        await Expect(Page.Locator("#tvsender-channel-status")).ToContainTextAsync("aufsteigend");
    }

    [Fact]
    public async Task Admin_RuleDiagnosis_ShowsAgeWindowReason()
    {
        await EnableTvSenderAsync();
        await SetUnknownModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow);
        var movieId = await SeedMovieWithItemAsync("e2e-fsk18", TimeSpan.FromMinutes(90), minAge: 18);

        // The page creates the channel on first load — open once, seed the rule, reload.
        await OpenAdminPageAsAdminAsync();
        await SeedMovieRuleAsync("Mittagsregel", 12, 0, movieId);
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".tvsender-rule-diagnosis", new() { Timeout = 30_000 });
        await Expect(Page.Locator(".tvsender-rule-diagnosis")).ToContainTextAsync("altersgerecht");
    }

    [Fact]
    public async Task Admin_AgeWindowedContent_ScheduledAtNight()
    {
        await EnableTvSenderAsync();
        await SetUnknownModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow);
        var movieId = await SeedMovieWithItemAsync("e2e-nachtfilm", TimeSpan.FromMinutes(60), minAge: 18);

        // The page creates the channel on first load — open once, seed the rule, then run.
        await OpenAdminPageAsAdminAsync();
        await SeedMovieRuleAsync("Nachtslot", 23, 30, movieId);
        await Page.ReloadAsync();
        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");
        await Page.ClickAsync("#tvsender-generate-now");
        await Expect(Page.Locator("#tvsender-schedule-status")).ToContainTextAsync("aktualisiert",
            new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

        // The worker run is asynchronous — the page's 4-second wait is no guarantee that the
        // generation finished, so the slot is awaited in the database.
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        List<ProgramSlot> slots;
        do
        {
            using var checkScope = Services.CreateScope();
            var checkDb = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            slots = await checkDb.ProgramSlots
                .Where(s => s.MovieId == movieId && s.Status == ProgramSlotStatus.Scheduled)
                .ToListAsync(ct);
            if (slots.Count > 0)
                break;
            await Task.Delay(1000, ct);
        }
        while (DateTime.UtcNow < deadline);

        // Every generated slot of the rated movie starts inside the FSK-18 night window.
        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), TimeZoneInfo.Local));
            Assert.True(local >= new TimeOnly(23, 0) || local < new TimeOnly(6, 0), $"Slot-Start {local} liegt außerhalb des Nachtfensters.");
        });

        // Fresh page load renders the slot card (the tab pane does not refresh by itself).
        await Page.GotoAsync($"{ServerUrl}/admin/tv-sender");
        await Page.WaitForSelectorAsync("#tvsender-tab-sendeplan");
        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");
        await Expect(Page.Locator(".tvsender-slot-row").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
    }
}
