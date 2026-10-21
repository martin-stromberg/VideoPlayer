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

        Assert.True(summary.DroppedConflicts >= 1);
        var slots = await fixture.SlotsAsync(ct);
        // The 90-min movie occupies 20:30–22:00; the 2×45-min series airing at 20:00–21:30 overlaps → dropped.
        Assert.DoesNotContain(slots, s => s.TVShowEpisodeId is not null);
        Assert.Contains(slots, s => s.MovieId == winner.Id);
        // Loser cursor: yesterday's lookback airing consumed 2 episodes; today's dropped
        // conflict must not advance it further.
        var loserRule = await fixture.Db.ProgramRules.FirstAsync(r => r.Name == "Serie20", ct);
        var loserCursor = await fixture.Db.ProgramRuleCursors.SingleAsync(c => c.ProgramRuleId == loserRule.Id, ct);
        Assert.Equal(loserEpisodes[1].Id, loserCursor.LastContentId);
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
