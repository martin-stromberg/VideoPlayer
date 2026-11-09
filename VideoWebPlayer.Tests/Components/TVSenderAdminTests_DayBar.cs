using Bunit;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the day selection bar of the "Sendeplan" tab in <see cref="TVSenderAdmin"/>
/// (Issue #306): the bar offers every day present in the schedule, preselects the current or
/// next relevant day and renders exactly that day's cards. Rendered against a real SQLite
/// database like <see cref="TVSenderAdminTests"/>.
/// </summary>
public sealed class TVSenderAdminTests_DayBar
{
    /// <summary>Seeds one slot on each given local day (local noon, converted to UTC, so the
    /// rendered <c>data-day</c> matches the local calendar date).</summary>
    private static async Task SeedSlotOnLocalDayAsync(
        ProgramScheduleTestDb fixture, long channelId, DateTime localDay, CancellationToken ct)
    {
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localDay.AddHours(12), TimeZoneInfo.Local);
        await fixture.CreateSlotAsync(channelId, startUtc, startUtc.AddHours(1), item.Id);
    }

    /// <summary>Seeds one slot on the local <em>today</em>. The page only loads slots with
    /// <c>Start &gt;= UtcNow-2h</c>, so the seed lands on the side of the calendar day that is
    /// guaranteed inside that window: 01:00 local before 2 a.m., 23:00 local afterwards —
    /// at every runtime one of the two stays visible.</summary>
    private static async Task SeedSlotOnTodayAsync(
        ProgramScheduleTestDb fixture, long channelId, CancellationToken ct)
    {
        var localStart = DateTime.Now.Hour < 2
            ? DateTime.Today.AddHours(1)
            : DateTime.Today.AddHours(23);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(60));
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local);
        await fixture.CreateSlotAsync(channelId, startUtc, startUtc.AddHours(1), item.Id);
    }

    private static async Task<(BunitContext Ctx, IRenderedComponent<TVSenderAdmin> Cut)> RenderSendeplanAsync(
        ProgramScheduleTestDb fixture, string adminId)
    {
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, adminId);
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-tab-sendeplan"), TimeSpan.FromSeconds(10));
        cut.Find("#tvsender-tab-sendeplan").Click();
        return (ctx, cut);
    }

    [Fact]
    public async Task DayBar_ListsEveryScheduleDay_Once()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-daybar-days", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);

        var tomorrow = DateTime.Today.AddDays(1);
        // Zwei Sendeplaetze am selben Tag + einer zwei Tage spaeter → zwei Leisten-Eintraege.
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, tomorrow, ct);
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, tomorrow, ct);
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, tomorrow.AddDays(2), ct);

        var (ctx, cut) = await RenderSendeplanAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var buttons = cut.FindAll("#tvsender-day-bar .tvsender-day-btn");
        Assert.Equal(
            new[] { tomorrow.ToString("yyyy-MM-dd"), tomorrow.AddDays(2).ToString("yyyy-MM-dd") },
            buttons.Select(b => b.GetAttribute("data-day")).ToArray());
    }

    [Fact]
    public async Task DayBar_Default_PreselectsToday_WhenPresent()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-daybar-today", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);

        var today = DateTime.Today;
        await SeedSlotOnTodayAsync(fixture, channel.Id, ct);
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, today.AddDays(1), ct);

        var (ctx, cut) = await RenderSendeplanAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var active = Assert.Single(
            cut.FindAll("#tvsender-day-bar .tvsender-day-btn.active"));
        Assert.Equal(today.ToString("yyyy-MM-dd"), active.GetAttribute("data-day"));
        var header = Assert.Single(
            cut.FindAll("#tvsender-schedule-cards .tvsender-day-header"));
        Assert.Equal(today.ToString("yyyy-MM-dd"), header.GetAttribute("data-day"));
        Assert.Single(cut.FindAll("#tvsender-schedule-cards .tvsender-slot-row"));
    }

    [Fact]
    public void ResolveScheduleDay_OnlyPastDays_SelectsLatestPastDay()
    {
        var today = new DateTime(2026, 10, 29);
        var days = new[] { today.AddDays(-3), today.AddDays(-1) };

        var resolved = TVSenderAdmin.ResolveScheduleDay(days, selected: null, today);

        Assert.Equal(today.AddDays(-1), resolved);
    }

    [Fact]
    public void ResolveScheduleDay_SelectedDayMissing_FallsBackToNext()
    {
        var today = new DateTime(2026, 10, 29);
        var days = new[] { today.AddDays(2), today.AddDays(3) };

        // Eine fruehere Auswahl, die nicht mehr im Plan steht, greift nicht —
        // es gilt wieder der naechste belegte Tag ab heute.
        var resolved = TVSenderAdmin.ResolveScheduleDay(days, selected: today.AddDays(-5), today);

        Assert.Equal(today.AddDays(2), resolved);
    }

    [Fact]
    public void ResolveScheduleDay_SelectedDayPresent_KeepsSelection()
    {
        var today = new DateTime(2026, 10, 29);
        var days = new[] { today, today.AddDays(1), today.AddDays(2) };

        var resolved = TVSenderAdmin.ResolveScheduleDay(days, selected: today.AddDays(2), today);

        Assert.Equal(today.AddDays(2), resolved);
    }

    [Fact]
    public async Task DayBar_SwitchingDay_ExchangesVisibleSlots()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-daybar-switch", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);

        var tomorrow = DateTime.Today.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, tomorrow, ct);
        await SeedSlotOnLocalDayAsync(fixture, channel.Id, dayAfter, ct);

        var (ctx, cut) = await RenderSendeplanAsync(fixture, admin.Id);
        using var _ctx = ctx;

        // Vorweg: der erste Tag (naechster belegter) ist aktiv und zeigt genau eine Karte.
        var dayOne = Assert.Single(
            cut.FindAll("#tvsender-day-bar .tvsender-day-btn.active"));
        Assert.Equal(tomorrow.ToString("yyyy-MM-dd"), dayOne.GetAttribute("data-day"));
        Assert.Single(cut.FindAll("#tvsender-schedule-cards .tvsender-slot-row"));

        // Wechsel auf den zweiten Tag: nur dessen Karte bleibt, Header und Aktiv-Status
        // folgen der Auswahl.
        cut.Find(
                $"#tvsender-day-bar .tvsender-day-btn[data-day='{dayAfter:yyyy-MM-dd}']")!
            .Click();
        var dayTwo = Assert.Single(
            cut.FindAll("#tvsender-day-bar .tvsender-day-btn.active"));
        Assert.Equal(dayAfter.ToString("yyyy-MM-dd"), dayTwo.GetAttribute("data-day"));
        Assert.Equal("true", dayTwo.GetAttribute("aria-selected"));
        var header = Assert.Single(
            cut.FindAll("#tvsender-schedule-cards .tvsender-day-header"));
        Assert.Equal(dayAfter.ToString("yyyy-MM-dd"), header.GetAttribute("data-day"));
        Assert.Single(cut.FindAll("#tvsender-schedule-cards .tvsender-slot-row"));

        // Zurueck auf den ersten Tag — die Auswahl laesst sich wiederholen.
        cut.Find(
                $"#tvsender-day-bar .tvsender-day-btn[data-day='{tomorrow:yyyy-MM-dd}']")!
            .Click();
        var back = Assert.Single(
            cut.FindAll("#tvsender-day-bar .tvsender-day-btn.active"));
        Assert.Equal(tomorrow.ToString("yyyy-MM-dd"), back.GetAttribute("data-day"));
        Assert.Single(cut.FindAll("#tvsender-schedule-cards .tvsender-slot-row"));
    }
}
