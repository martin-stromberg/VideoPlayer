using System.Text.RegularExpressions;
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
/// Playwright E2E tests for the day selection bar of the "Sendeplan" tab (Issue #306): the
/// bar offers every day present in the schedule, preselects the next relevant day and a day
/// switch exchanges the visible slot cards without reloading the schedule.
/// </summary>
public sealed class TVSenderAdminDayBarE2ETests : TVSenderAdminE2ETestBase
{
    /// <summary>Seeds one movie slot on each of two consecutive local days (local noon → UTC,
    /// so the rendered <c>data-day</c> matches the local calendar date) — returns the two
    /// local dates.</summary>
    private async Task<(DateTime Day1, DateTime Day2)> SeedTwoDayScheduleAsync()
    {
        var movieDay1 = await SeedMovieWithItemAsync("E2E Tag-Eins-Film", TimeSpan.FromMinutes(60));
        var movieDay2 = await SeedMovieWithItemAsync("E2E Tag-Zwei-Film", TimeSpan.FromMinutes(60));

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var channel = await new ProgramRuleService(db).GetOrCreateChannelAsync(ct);
        var itemDay1 = await db.MovieMediaItems.Where(m => m.MovieId == movieDay1)
            .Select(m => m.MediaItemId).SingleAsync(ct);
        var itemDay2 = await db.MovieMediaItems.Where(m => m.MovieId == movieDay2)
            .Select(m => m.MediaItemId).SingleAsync(ct);

        var day1 = DateTime.Today.AddDays(1);
        var day2 = day1.AddDays(1);
        foreach (var (movieId, itemId, day) in new[]
        {
            (movieDay1, itemDay1, day1),
            (movieDay2, itemDay2, day2)
        })
        {
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(day.AddHours(12), TimeZoneInfo.Local);
            db.ProgramSlots.Add(new ProgramSlot
            {
                TVChannelId = channel.Id,
                Start = startUtc,
                End = startUtc.AddHours(1),
                MediaItemId = itemId,
                MovieId = movieId,
                RuleType = ProgramRuleType.MoviePrimeTime,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
        return (day1, day2);
    }

    [Fact]
    public async Task Sendeplan_DayBar_SelectsSingleDay()
    {
        if (SkipBrowser) return;
        await EnableTvSenderAsync();
        var (day1, day2) = await SeedTwoDayScheduleAsync();

        await OpenAdminPageAsAdminAsync("#tvsender-tab-sendeplan");
        await SwitchTabAsync("#tvsender-tab-sendeplan", "#tab-sendeplan");

        // Die Auswahlleiste steht ueber der Titelauflistung und bietet beide Plantage;
        // vorausgewaehlt ist der naechste belegte Tag (heute hat keine Sendeplaetze).
        var dayBar = Page.Locator("#tvsender-day-bar");
        await Expect(dayBar).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        var dayButtons = dayBar.Locator(".tvsender-day-btn");
        await Expect(dayButtons).ToHaveCountAsync(2);
        await Expect(dayButtons.Nth(0)).ToHaveAttributeAsync("data-day", day1.ToString("yyyy-MM-dd"));
        await Expect(dayButtons.Nth(1)).ToHaveAttributeAsync("data-day", day2.ToString("yyyy-MM-dd"));
        await Expect(dayButtons.Nth(0)).ToHaveClassAsync(new Regex("active"));
        await Expect(dayButtons.Nth(0)).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(dayButtons.Nth(1)).ToHaveAttributeAsync("aria-selected", "false");

        // Nur der gewaehlte Tag rendert Karten — der zweite Tag bleibt verborgen.
        await Expect(Page.Locator("#tvsender-schedule-cards .tvsender-day-header"))
            .ToHaveAttributeAsync("data-day", day1.ToString("yyyy-MM-dd"));
        var slotRows = Page.Locator(".tvsender-slot-row");
        await Expect(slotRows).ToHaveCountAsync(1);
        await Expect(slotRows.First).ToContainTextAsync("E2E Tag-Eins-Film");

        // Tageswechsel: die Liste zeigt ausschliesslich die Sendeplaetze des neuen Tages.
        await dayButtons.Nth(1).ClickAsync();
        await Expect(dayButtons.Nth(1)).ToHaveClassAsync(new Regex("active"));
        await Expect(dayButtons.Nth(0)).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Page.Locator("#tvsender-schedule-cards .tvsender-day-header"))
            .ToHaveAttributeAsync("data-day", day2.ToString("yyyy-MM-dd"));
        await Expect(slotRows).ToHaveCountAsync(1);
        await Expect(slotRows.First).ToContainTextAsync("E2E Tag-Zwei-Film");

        // Zurueck zum ersten Tag — die Auswahl laesst sich wiederholen.
        await dayButtons.Nth(0).ClickAsync();
        await Expect(slotRows.First).ToContainTextAsync("E2E Tag-Eins-Film");
    }
}
