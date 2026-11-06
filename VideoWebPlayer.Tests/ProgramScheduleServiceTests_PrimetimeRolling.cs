using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Issue „Senderplanung fehlerhaft", scenario group 1.a/1.f/1.k/1.l: the rolling
/// 7-day simulation of the primetime genre-rotation rule (daily 20:15 local,
/// 225-minute period, 15-minute grace). Asserts full inventory preservation across
/// simulated days plus the negative space — no primetime content may ever start
/// outside the 20:15–24:00 local window.
/// </summary>
public sealed class ProgramScheduleServiceTests_PrimetimeRolling
{
    /// <summary>Seeds the scenario material: two movie genres, one show genre and one
    /// user clip group; the reserved trailer group expands empty (no trailer links).</summary>
    /// <param name="Rule">The created primetime rule (return tuple element).</param>
    /// <param name="MovieIds">The ids of all seeded movies (return tuple element).</param>
    /// <param name="Episodes">The seeded show episodes in air order (return tuple element).</param>
    /// <param name="fixture">The seeded test database.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rule, the movie ids and the show episodes.</returns>
    private static async Task<(ProgramRule Rule, List<long> MovieIds, List<TVShowEpisode> Episodes)> SeedScenarioAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct)
    {
        var channel = await fixture.GetChannelAsync(ct);
        var (gMovies1, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 6, TimeSpan.FromMinutes(100));
        var (gMovies2, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Komödie", 6, TimeSpan.FromMinutes(95));
        var (gShow, _, episodes) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Krimi", "Tatort", 12, TimeSpan.FromMinutes(45));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 5, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [gMovies1.Id, gMovies2.Id, gShow.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);
        var movies = await fixture.Db.MovieGenres
            .Where(g => g.GenreId == gMovies1.Id || g.GenreId == gMovies2.Id)
            .Select(g => g.MovieId).ToListAsync(ct);
        return (rule, movies, episodes);
    }

    // Scenario 1.a: the week is simulated in daily steps (reconcile + extend, the
    // worker's own order). Planned titles stay untouched — later runs only append.
    [Fact]
    public async Task Simulation_WeekOfDailyRuns_PlanOnlyExtendsNoDisplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-week", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var (rule, _, _) = await SeedScenarioAsync(fixture, ct);

        var start = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        var previous = new List<ProgramSlot>();
        for (var day = 0; day < PrimetimeSeed.HorizonDays; day++)
        {
            var now = start.AddDays(day);
            var (reconcile, _) = await PrimetimeSeed.StepAsync(fixture, now, ct);
            await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, reconcile, ct, $"Lauf Tag {day}");
            var current = await fixture.SlotsAsync(ct);
            PrimetimeSeed.AssertPreserved(previous, current, $"Lauf Tag {day}");
            previous = current;
        }

        var ruleSlots = previous.Where(s => s.ProgramRuleId == rule.Id && s.Status == ProgramSlotStatus.Scheduled).ToList();
        PrimetimeSeed.AssertStartsInsidePeriod(ruleSlots);
        PrimetimeSeed.AssertEveryEveningHasTitles(ruleSlots, new DateOnly(2026, 11, 2), PrimetimeSeed.HorizonDays);
    }

    // Scenario 1.f: the rule is created mid-airing (20:45 — the 20:15 occurrence is
    // already in the past but still running) → the plan must reach back to 20:15.
    [Fact]
    public async Task EnsureCoverage_RuleCreatedMidPrimetime_SchedulesBackToPeriodStart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-midcreate", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (genre, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 4, TimeSpan.FromMinutes(100));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 3, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [genre.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        // Rule "created" at 20:45 local — the 20:15 occurrence is already running.
        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 20, 45));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(ruleSlots);

        var tonight = PrimetimeSeed.AiringOf(ruleSlots, new DateOnly(2026, 11, 2));
        Assert.NotEmpty(tonight);
        // The airing is written with its true start 20:15 — even though "now" is 20:45.
        Assert.Equal(20 * 60 + 15, tonight.Min(s => PrimetimeSeed.LocalMinutesOfDay(s.Start)));
        // Negative space: nothing of this rule ever starts before the period start.
        Assert.All(ruleSlots, s => Assert.True(
            PrimetimeSeed.ToLocal(s.Start) >= PrimetimeSeed.LocalUtc(2026, 11, 2, 20, 15),
            $"Slot startet {PrimetimeSeed.ToLocal(s.Start):dd.MM. HH:mm} — vor der heutigen Primetime."));
    }

    // Soll decision (issue.md): an occurrence whose airing already ENDED before the
    // planning instant was never part of a plan — it must not consume ring positions.
    // Today it does (cursor burnt for the "yesterday" that never aired).
    [Fact]
    public async Task EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-lookback", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        // Ordered strand → deterministic episode order; 225 min / 45 min = 5 per airing.
        var (genre, _, episodes) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Krimi", "Tatort", 8, TimeSpan.FromMinutes(45));
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id, [genre.Id], []);

        // First run Monday 12:00 — yesterday's 20:15 airing already ended (~00:00).
        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var tonight = PrimetimeSeed.AiringOf(
            (await fixture.SlotsAsync(ct)).Where(s => s.ProgramRuleId == rule.Id), new DateOnly(2026, 11, 2));
        var expected = episodes.Take(5).Select(e => (long?)e.Id).ToList();
        Assert.Equal(expected, tonight.Select(s => s.TVShowEpisodeId).ToList());
    }

    // Scenario 1.k: the rotation continues across the week — episodes follow air order
    // (with wrap-around, no jump) and every evening keeps the same strand structure.
    [Fact]
    public async Task EnsureCoverage_WeekOfAirings_StrandStructureStableAndOrderContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-continuity", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (gMovies, _) = await PrimetimeSeed.AddMovieGenreAsync(fixture, "Action", 6, TimeSpan.FromMinutes(100));
        var (gShow, _, episodes) = await PrimetimeSeed.AddShowGenreAsync(fixture, "Krimi", "Tatort", 8, TimeSpan.FromMinutes(45));
        var clips = await PrimetimeSeed.AddClipGroupAsync(fixture, "Maskottchen", 5, TimeSpan.FromMinutes(2), ct);
        var rule = await PrimetimeSeed.CreateRuleAsync(fixture, channel.Id,
            [gMovies.Id, gShow.Id], [clips.Group.Id, ProgramClipGroup.TrailerGroupId]);

        var now = new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 11, 2, 12, 0));
        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id).ToList();

        // Air order across the whole week: each placed episode follows its predecessor
        // (+1) or wraps from the last episode back to the first — never a jump.
        var episodeNumbers = ruleSlots
            .Where(s => s.TVShowEpisodeId is not null)
            .Select(s => episodes.Single(e => e.Id == s.TVShowEpisodeId).Number)
            .ToList();
        Assert.NotEmpty(episodeNumbers);
        for (var i = 1; i < episodeNumbers.Count; i++)
        {
            var expected = episodeNumbers[i - 1] == episodes.Count ? 1 : episodeNumbers[i - 1] + 1;
            Assert.True(episodeNumbers[i] == expected,
                $"Episodensprung: {episodeNumbers[i - 1]} → {episodeNumbers[i]} an Position {i}.");
        }

        // Same strand structure every evening: the title-type sequence of the first
        // evening is the reference — every other airing repeats it exactly (the strands
        // keep their slot position across days while their cursors advance).
        var reference = PrimetimeSeed.AiringOf(ruleSlots, new DateOnly(2026, 11, 2))
            .Where(s => !s.IsClipFill).Select(s => s.MovieId is not null).ToList();
        Assert.NotEmpty(reference);
        for (var day = 1; day < PrimetimeSeed.HorizonDays; day++)
        {
            var titles = PrimetimeSeed.AiringOf(ruleSlots, new DateOnly(2026, 11, 2).AddDays(day))
                .Where(s => !s.IsClipFill).Select(s => s.MovieId is not null).ToList();
            Assert.Equal(reference, titles);
        }
    }

    // Scenario 1.l: the airing starts 20:15 local every single day — including the
    // DST change weekend (25./26.10.2026). No shifted, doubled or missing start.
    [Fact]
    public async Task EnsureCoverage_AcrossDstChange_EveryAiringStartsAt2015Local()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("primetime-dst", ct);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, PrimetimeSeed.HorizonDays, ct);
        var (rule, _, _) = await SeedScenarioAsync(fixture, ct);

        // Two runs bracketing the DST change (25.10.2026): before and after.
        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 24, 12, 0)), ct);
        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(PrimetimeSeed.LocalUtc(2026, 10, 28, 12, 0)), ct);

        var ruleSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id).ToList();
        PrimetimeSeed.AssertStartsInsidePeriod(ruleSlots);

        for (var day = 0; day < PrimetimeSeed.HorizonDays; day++)
        {
            var evening = new DateOnly(2026, 10, 24).AddDays(day);
            var airing = PrimetimeSeed.AiringOf(ruleSlots, evening);
            Assert.NotEmpty(airing);
            Assert.Equal(PrimetimeSeed.Start.ToTimeSpan().TotalMinutes,
                airing.Min(s => PrimetimeSeed.LocalMinutesOfDay(s.Start)));
        }
    }
}
