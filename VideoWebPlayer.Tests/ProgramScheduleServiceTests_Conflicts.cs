using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleServiceTests_Conflicts
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    [Fact]
    public async Task EnsureCoverage_HigherPriorityWins_Overlap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-priority", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var winner = await fixture.CreateMovieAsync("Gewinner", duration: TimeSpan.FromMinutes(90));
        var (loserShow, _, loserEpisodes) = await fixture.CreateShowAsync("Verlierer", 3, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Serie20", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, loserShow.Id, priority: 1, episodeCount: 2);
        await fixture.CreateRuleAsync(channel.Id, "Film20", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 30), ProgramRuleMaterialType.Movie, winner.Id, priority: 5);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        // The 20:30 movie window is a hard bound for the lower-priority series: its 45-min
        // episodes can never complete before 20:30, so they defer instead of colliding —
        // no airing is materialized, nothing is dropped mid-air.
        Assert.Equal(0, summary.DroppedConflicts);
        var slots = await fixture.SlotsAsync(ct);
        Assert.DoesNotContain(slots, s => s.TVShowEpisodeId is not null);
        Assert.Contains(slots, s => s.MovieId == winner.Id);
        // Loser cursor: no episode ever aired (every occurrence defers on the hard bound) —
        // the rotation simply waits.
        var loserRule = await fixture.Db.ProgramRules.FirstAsync(r => r.Name == "Serie20", ct);
        Assert.Empty(await fixture.Db.ProgramRuleCursors.Where(c => c.ProgramRuleId == loserRule.Id).ToListAsync(ct));
    }

    [Fact]
    public async Task EnsureCoverage_EqualPriority_LowerRuleIdWins()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-tiebreak", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var first = await fixture.CreateMovieAsync("Erste-Regel-Film", duration: TimeSpan.FromMinutes(90));
        var second = await fixture.CreateMovieAsync("Zweite-Regel-Film", duration: TimeSpan.FromMinutes(90));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var ruleA = await fixture.CreateRuleAsync(channel.Id, "A-Regel", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, first.Id, priority: 3);
        var ruleB = await fixture.CreateRuleAsync(channel.Id, "B-Regel", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, second.Id, priority: 3);
        Assert.True(ruleA.Id < ruleB.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var ruleSlots = slots.Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.All(ruleSlots, s => Assert.Equal(ruleA.Id, s.ProgramRuleId));
        Assert.All(ruleSlots, s => Assert.Equal(first.Id, s.MovieId));
    }
}
