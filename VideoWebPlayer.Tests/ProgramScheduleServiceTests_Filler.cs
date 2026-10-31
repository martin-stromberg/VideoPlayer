using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleServiceTests_Filler
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    [Fact]
    public async Task EnsureCoverage_OnlyFillerRule_FillsEntireHorizon()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-only-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(ProgramRuleType.Filler, s.RuleType));
        Assert.Equal(slots.Count, summary.FillerSlotsCreated);
        ProgramScheduleTestDb.AssertGapless(slots, now.UtcDateTime, now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task EnsureCoverage_FillerItemTooLong_IsSkipped_NoFittingFiller_LeavesGap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-filler-fit", ct);
        var channel = await fixture.GetChannelAsync(ct);
        // Prime-time movie 20:00–21:00; fillers of 45 min can't tile the remaining gaps exactly
        // (gaps: 12:00–20:00 = 480 min → 10×45 fits +30 rest; 21:00–24:00 = 180 → 4×45 fits exactly…)
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(60));
        var longFiller = await fixture.CreateMovieAsync("Langer Füller", duration: TimeSpan.FromMinutes(45));
        var shortFiller = await fixture.CreateMovieAsync("Kurzer Füller", duration: TimeSpan.FromMinutes(15));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Lange Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, longFiller.Id, episodeCount: 1);

        var summary1 = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        // 45-min filler cannot tile the 30-min remainder → a gap remains.
        Assert.True(summary1.RemainingGapSeconds > 0);

        // Now add a second filler rule with a short item that fits the remainder.
        var fixture2Name = "schedule-filler-fit2";
        using var fixture2 = await ProgramScheduleTestDb.CreateAsync(fixture2Name, ct);
        var channel2 = await fixture2.GetChannelAsync(ct);
        var movie2 = await fixture2.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(60));
        var f1 = await fixture2.CreateMovieAsync("Langer Füller", duration: TimeSpan.FromMinutes(45));
        var f2 = await fixture2.CreateMovieAsync("Kurzer Füller", duration: TimeSpan.FromMinutes(15));
        await fixture2.CreateRuleAsync(channel2.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie2.Id);
        await fixture2.CreateRuleAsync(channel2.Id, "Lang", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, f1.Id, episodeCount: 1);
        await fixture2.CreateRuleAsync(channel2.Id, "Kurz", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, f2.Id, episodeCount: 1);

        var summary2 = await fixture2.CreateService().EnsureCoverageAsync(now, ct);
        var slots2 = await fixture2.SlotsAsync(ct);
        Assert.Equal(0, summary2.RemainingGapSeconds);
        ProgramScheduleTestDb.AssertGapless(slots2, now.UtcDateTime, now.UtcDateTime.AddHours(48));
        Assert.Contains(slots2, s => s.MovieId == f2.Id);
    }
}
