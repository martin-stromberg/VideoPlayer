using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario group 1.b/1.c/1.j: the 15-minute
/// placeholder grace at the period end (20:15 + 225 min → 00:00 local). Below the
/// grace the remainder goes to clip fill — no further title may start; above it the
/// next title starts and overshoots the period end to its real end.
/// </summary>
public sealed class ProgramScheduleServiceTests_PrimetimeGrace
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    // Scenario 1.b: two movies fill the period down to a remainder below the 15-minute
    // grace (215 of 225 min) → exactly two titles air, the rest is clip fill, and no
    // third (wrapped) movie ever starts.
    [Fact]
    public async Task EnsureCoverage_RemainderBelowGrace_ClipsFillNoThirdMovie()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-grace-clips", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        // Two movies: 110 + 105 = 215 min → 10 min remainder, below the 15-min grace.
        var genre = await fixture.CreateGenreAsync("Action");
        var m1 = await FixtureMovieAsync(fixture, "A", TimeSpan.FromMinutes(110), genre.Id);
        var m2 = await FixtureMovieAsync(fixture, "B", TimeSpan.FromMinutes(105), genre.Id);
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 6, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var tonight = PrimetimeSeed.AiringOf(
            (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id), Day);
        var titles = tonight.Where(s => !s.IsClipFill).ToList();

        // Exactly the two movies — each exactly once, never a wrap-around repeat.
        Assert.Equal(2, titles.Count);
        Assert.Equal(new[] { m1.Id, m2.Id }.Select(id => (long?)id).OrderBy(id => id),
            titles.Select(t => t.MovieId).OrderBy(id => id));

        // The <15-minute remainder is filled with clips of the assigned groups.
        var clipSlots = tonight.Where(s => s.IsClipFill).ToList();
        Assert.NotEmpty(clipSlots);
        Assert.All(clipSlots, s => Assert.Contains(s.MediaItemId, clips.Items.Select(i => i.Id)));

        // Negative space: after the last title ends, no further title starts.
        var lastTitleEnd = titles.Max(s => s.End);
        Assert.DoesNotContain(tonight, s => !s.IsClipFill && s.Start >= lastTitleEnd);
        PrimetimeSeed.AssertStartsInsidePeriod(tonight);
    }

    // Scenario 1.c: two movies leave more than the 15-minute grace → a third movie
    // starts inside the period and runs past midnight to its real end; nothing of the
    // expired period (no clips) follows it, and the next evening starts clean.
    [Fact]
    public async Task EnsureCoverage_RemainderAboveGrace_ThirdMovieOvershootsMidnight()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-grace-overshoot", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        // Equal runtimes: two picks always leave 25 min above the grace, regardless of the
        // hash-mixed ring position the first airing starts at.
        var m1 = await FixtureMovieAsync(fixture, "A", TimeSpan.FromMinutes(100), genre.Id);
        var m2 = await FixtureMovieAsync(fixture, "B", TimeSpan.FromMinutes(100), genre.Id);
        var m3 = await FixtureMovieAsync(fixture, "C", TimeSpan.FromMinutes(100), genre.Id);
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 3, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        var tonight = PrimetimeSeed.AiringOf(ruleSlots, Day);
        var titles = tonight.Where(s => !s.IsClipFill).ToList();

        // Three titles: 100 + 100 = 200 min → 25 min remainder above the grace → the
        // third movie starts at 23:35 local and runs past 00:00 to 01:15.
        Assert.Equal(3, titles.Count);
        var last = titles[^1];
        Assert.Equal(23 * 60 + 35, PrimetimeSeed.LocalMinutesOfDay(last.Start), 0);
        Assert.Equal(PrimetimeSeed.LocalUtc(2026, 11, 3, 1, 15), last.End);

        // No clip of the expired period follows the overshooting movie — the remainder
        // stays an open gap until the next day's 20:15 airing.
        Assert.DoesNotContain(tonight, s => s.IsClipFill && s.Start >= last.End);
        Assert.DoesNotContain(ruleSlots, s =>
            PrimetimeSeed.EveningDateOf(s.Start) == Day && s.Start >= last.End);

        // The next evening's airing starts cleanly at 20:15 local.
        var tomorrow = PrimetimeSeed.AiringOf(ruleSlots, Day.AddDays(1));
        Assert.NotEmpty(tomorrow);
        Assert.Equal(PrimetimeSeed.Start.ToTimeSpan().TotalMinutes,
            tomorrow.Min(s => PrimetimeSeed.LocalMinutesOfDay(s.Start)));
        PrimetimeSeed.AssertStartsInsidePeriod(ruleSlots);
    }

    // Scenario 1.j: the grace remainder is drawn round-robin across BOTH clip groups —
    // and no clip may smuggle a title start back into the remainder.
    [Fact]
    public async Task EnsureCoverage_ClipRest_DistributedAcrossBothGroupsNoTitleAfter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-grace-groups", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Action");
        await FixtureMovieAsync(fixture, "A", TimeSpan.FromMinutes(110), genre.Id);
        await FixtureMovieAsync(fixture, "B", TimeSpan.FromMinutes(105), genre.Id);
        var groupA = await PrimetimeSeed.AddClipGroupAsync(fixture, "GruppeA", 4, TimeSpan.FromMinutes(2), ct);
        var groupB = await PrimetimeSeed.AddClipGroupAsync(fixture, "GruppeB", 4, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [groupA.Group.Id, groupB.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var tonight = PrimetimeSeed.AiringOf(
            (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id), Day);
        var clipSlots = tonight.Where(s => s.IsClipFill).ToList();
        Assert.True(clipSlots.Count >= 2, "Restlücke sollte über beide Gruppen gefüllt sein.");

        var idsA = groupA.Items.Select(i => i.Id).ToHashSet();
        var idsB = groupB.Items.Select(i => i.Id).ToHashSet();
        Assert.Contains(clipSlots, s => idsA.Contains(s.MediaItemId));
        Assert.Contains(clipSlots, s => idsB.Contains(s.MediaItemId));

        // Round-robin: groups strictly alternate over the placed clip slots.
        for (var i = 1; i < clipSlots.Count; i++)
        {
            var prevInA = idsA.Contains(clipSlots[i - 1].MediaItemId);
            var curInA = idsA.Contains(clipSlots[i].MediaItemId);
            Assert.NotEqual(prevInA, curInA);
        }

        // After the last title ends only clips remain — no title hides behind the fill.
        var lastTitleEnd = tonight.Where(s => !s.IsClipFill).Max(s => s.End);
        Assert.All(tonight.Where(s => s.Start >= lastTitleEnd), s => Assert.True(s.IsClipFill));
    }

    private static async Task<Movie> FixtureMovieAsync(
        ProgramScheduleTestDb fixture, string name, TimeSpan duration, long genreId)
    {
        var movie = await fixture.CreateMovieAsync(name, duration: duration);
        await fixture.LinkMovieGenreAsync(movie.Id, genreId);
        return movie;
    }
}
