using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramScheduleService.ReconcileAsync"/>: the schedule re-evaluation that
/// keeps the plan consistent after rule changes and media-library changes — invalid scheduled
/// slots are marked <see cref="ProgramSlotStatus.Displaced"/> (never deleted) and the freed
/// windows are re-materialized.
/// </summary>
public sealed class ProgramScheduleServiceTests_Reconcile
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    private static async Task<List<ProgramSlot>> DisplacedAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced).ToList();

    [Fact]
    public async Task Reconcile_NoChanges_LeavesEverythingScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-nochange", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(10));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var countBefore = (await fixture.SlotsAsync(ct)).Count;

        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        var slots = await fixture.SlotsAsync(ct);
        // The reconcile window reaches 30 minutes past the previous coverage edge — the tail may
        // be filled (no displacement, all existing slots stay Scheduled).
        Assert.Equal(countBefore + summary.SlotsCreated, slots.Count);
        Assert.All(slots, s => Assert.Equal(ProgramSlotStatus.Scheduled, s.Status));
        ProgramScheduleTestDb.AssertGapless(slots, now.UtcDateTime, now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task Reconcile_DeactivatedRule_DisplacesFutureAndRefills()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-deactivate", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(15));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var futureRuleSlots = (await fixture.SlotsAsync(ct))
            .Where(s => s.ProgramRuleId == rule.Id && s.RuleType == ProgramRuleType.MoviePrimeTime).Count();
        Assert.True(futureRuleSlots > 0);

        rule.IsActive = false;
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.Equal(futureRuleSlots, summary.SlotsDisplaced);
        var displaced = await DisplacedAsync(fixture, ct);
        Assert.All(displaced, s => Assert.Equal(rule.Id, s.ProgramRuleId));
        // Displaced rows are kept (no hard delete) and the freed window is refilled.
        Assert.True((await fixture.SlotsAsync(ct)).Count >= futureRuleSlots);
        var scheduled = await ScheduledAsync(fixture, ct);
        ProgramScheduleTestDb.AssertGapless(scheduled, now.UtcDateTime.AddMinutes(30), now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-delete", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        fixture.Db.ProgramRules.Remove(rule);
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var displaced = await DisplacedAsync(fixture, ct);
        // Soft reference: the displaced slots still carry the deleted rule's id.
        Assert.All(displaced, s => Assert.Equal(rule.Id, s.ProgramRuleId));
        // Every displaced window is covered again (a sub-filler remainder may stay — same as
        // generation semantics).
        var scheduled = await ScheduledAsync(fixture, ct);
        Assert.All(displaced, d => Assert.Contains(scheduled, s => s.Start < d.End && s.End > d.Start));
    }

    [Fact]
    public async Task Reconcile_ChangedTimeOfDay_ReplansAtNewTime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-timeofday", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(60));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        rule.TimeOfDay = new TimeOnly(22, 0);
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var ruleSlots = (await ScheduledAsync(fixture, ct)).Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(ruleSlots);
        Assert.All(ruleSlots, s =>
            Assert.Equal(22, TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz).Hour));
        var scheduled = await ScheduledAsync(fixture, ct);
        ProgramScheduleTestDb.AssertGapless(scheduled, now.UtcDateTime.AddMinutes(30), now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-series-reschedule", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 10, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var planned = (await fixture.SlotsAsync(ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => s.TVShowEpisodeId).ToList();
        // Lookback airing consumed E1+E2 → today E3+E4, tomorrow E5+E6 (48h horizon).
        Assert.Equal(episodes.Skip(2).Take(4).Select(e => (long?)e.Id), planned);

        // Move the series slot: all future airings get displaced and must re-air the same
        // episodes at the new time — in air order, without a skip.
        rule.TimeOfDay = new TimeOnly(22, 0);
        await fixture.SaveDiagAsync();
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        var displaced = (await DisplacedAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime).ToList();
        Assert.Equal(4, displaced.Count);

        var rescheduled = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => s.TVShowEpisodeId).ToList();
        var dumpSeries = string.Join(" | ", (await fixture.SlotsAsync(ct)).Select(s =>
            $"{s.Start:dd.HH:mm}-{s.End:HH:mm} ep={s.TVShowEpisodeId} {(s.Status == ProgramSlotStatus.Displaced ? "X" : "S")}"));
        Assert.True(rescheduled.SequenceEqual(episodes.Skip(2).Take(rescheduled.Count).Select(e => (long?)e.Id)), dumpSeries);
    }

    [Fact]
    public async Task Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-media-deleted", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 10, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 2);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // Delete episode 5's media item (bulk delete — the path real media deletion takes).
        var e5Item = await fixture.Db.TVShowEpisodeMediaItems
            .Where(l => l.TVShowEpisodeId == episodes[4].Id)
            .Select(l => l.MediaItemId)
            .SingleAsync(ct);
        await fixture.Db.TVShowEpisodeMediaItems.Where(l => l.MediaItemId == e5Item).ExecuteDeleteAsync(ct);
        await fixture.Db.MediaItems.Where(m => m.Id == e5Item).ExecuteDeleteAsync(ct);

        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced >= 2);
        var displaced = await DisplacedAsync(fixture, ct);
        Assert.Contains(displaced, s => s.MediaItemId == e5Item);
        // The deleted slot row stays — no delete cascade.
        Assert.Contains(await fixture.SlotsAsync(ct), s => s.MediaItemId == e5Item);

        // Tomorrow's airing re-airs the next episodes in order — E5 skipped (deleted), E6+E7 air.
        var tomorrowAiring = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .GroupBy(s => s.Start.Day)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(s => s.TVShowEpisodeId).ToList())
            .ToList();
        var dumpSeries = string.Join(" | ", (await fixture.SlotsAsync(ct)).Select(s =>
            $"{s.Start:dd.HH:mm}-{s.End:HH:mm} ep={s.TVShowEpisodeId} {(s.Status == ProgramSlotStatus.Displaced ? "X" : "S")}"));
        Assert.True(tomorrowAiring.Any(a => a.SequenceEqual(new long?[] { episodes[5].Id, episodes[6].Id })), dumpSeries);
        var scheduled = await ScheduledAsync(fixture, ct);
    }

    [Fact]
    public async Task Reconcile_PriorityRaised_EvictsLowerPriorityAiring()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-priority", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movieA = await fixture.CreateMovieAsync("FilmA", duration: TimeSpan.FromMinutes(60));
        var movieB = await fixture.CreateMovieAsync("FilmB", duration: TimeSpan.FromMinutes(60));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var ruleA = await fixture.CreateRuleAsync(channel.Id, "A", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movieA.Id, priority: 5);
        var ruleB = await fixture.CreateRuleAsync(channel.Id, "B", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movieB.Id, priority: 3);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        Assert.DoesNotContain(await fixture.SlotsAsync(ct), s => s.ProgramRuleId == ruleB.Id && s.RuleType == ProgramRuleType.MoviePrimeTime);

        ruleB.Priority = 9;
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var displaced = await DisplacedAsync(fixture, ct);
        Assert.All(displaced, s => Assert.Equal(ruleA.Id, s.ProgramRuleId));
        var bSlots = (await ScheduledAsync(fixture, ct)).Where(s => s.ProgramRuleId == ruleB.Id).ToList();
        Assert.NotEmpty(bSlots);
        Assert.All(bSlots, s => Assert.Equal(movieB.Id, s.MovieId));
    }

    [Fact]
    public async Task Reconcile_NewEpisodeInserted_ReplansInAirOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-new-episode", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, season, episodes) = await fixture.CreateShowAsync("Serie", 6, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // A new episode arrives mid-sequence: existing episodes 4-6 are renumbered 5-7 and the new
        // one takes number 4 — the expansion order changes and planned airings become invalid.
        foreach (var ep in episodes.Skip(3))
            ep.Number += 1;
        var newEpisode = new TVShowEpisode { Name = "Neue Folge", TVShowSeasonId = season.Id, Number = 4, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShowEpisodes.Add(newEpisode);
        await fixture.SaveDiagAsync();
        var newItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(45));
        fixture.Db.TVShowEpisodeMediaItems.Add(new TVShowEpisodeMediaItem { TVShowEpisodeId = newEpisode.Id, MediaItemId = newItem.Id });
        await fixture.SaveDiagAsync();

        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var scheduledEpisodes = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => s.TVShowEpisodeId).ToList();
        // Expansion order is now E1,E2,E3,newE,E4,E5,E6 → airings E3+newE, E4+E5 (48h horizon).
        Assert.Equal(new long?[] { episodes[2].Id, newEpisode.Id, episodes[3].Id, episodes[4].Id },
            scheduledEpisodes.Take(4).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_IgnoresDisplacedSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-ensure-coverage", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        rule.IsActive = false;
        await fixture.SaveDiagAsync();
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        // A following extend-only run must not treat the displaced slots as coverage.
        var summary = await service.EnsureCoverageAsync(now.AddMinutes(30), ct);
        var scheduled = await ScheduledAsync(fixture, ct);
        var displaced = await DisplacedAsync(fixture, ct);
        Assert.All(displaced, d => Assert.Contains(scheduled, s => s.Start < d.End && s.End > d.Start));
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == rule.Id);
    }

    [Fact]
    public async Task Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-partial-occurrence", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 10, duration: TimeSpan.FromMinutes(45));

        // The first coverage run happens while today's 14:00 airing (4×45 min) is already in
        // progress: only the tail slots are materialized — the airing legitimately starts at
        // 15:30, not at the occurrence start.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 15, 40));
        await fixture.CreateRuleAsync(channel.Id, "Nachmittagsserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 4);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var airing = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime && s.Start < now.UtcDateTime.AddHours(2))
            .ToList();
        Assert.Equal(2, airing.Count);
        Assert.Equal(LocalUtc(2026, 10, 24, 15, 30), airing[0].Start);
        Assert.Equal(episodes[2].Id, airing[0].TVShowEpisodeId);

        // The re-evaluation must recognize the tail as the still-running remainder of the 14:00
        // occurrence — nothing may be displaced.
        var summary = await service.ReconcileAsync(now.AddMinutes(10), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);

        // And a second run must stay equally quiet — no oscillation.
        var summary2 = await service.ReconcileAsync(now.AddMinutes(25), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "2. Lauf");
    }

    [Fact]
    public async Task Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-partial-stable", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 20, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 15, 40));
        await fixture.CreateRuleAsync(channel.Id, "Nachmittagsserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 4);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // Snapshot of the fully materialized future airings (tomorrow onward starts at 14:00).
        var before = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => (s.Start, s.End, s.TVShowEpisodeId))
            .ToList();

        await service.ReconcileAsync(now.AddMinutes(10), ct);
        var after = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => (s.Start, s.End, s.TVShowEpisodeId))
            .ToList();

        // Every previously scheduled series slot survives at the same position — the plan is
        // idempotent instead of being rewritten each run.
        Assert.All(before, b => Assert.Contains(after, a => a.Start == b.Start && a.End == b.End && a.TVShowEpisodeId == b.TVShowEpisodeId));
    }

    [Fact]
    public async Task Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-worker-loop", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 12, duration: TimeSpan.FromMinutes(45));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        // Daily 14:00 series airing (4×45 min) — the last horizon-edge airing overshoots the
        // planning horizon, and "today" is a partially aired tail — the two historical
        // displacement triggers of the overlap-cluster bug.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 15, 40));
        await fixture.CreateRuleAsync(channel.Id, "Nachmittagsserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 4);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // The worker cadence: Reconcile + EnsureCoverage per interval — repeated runs must not
        // churn the plan.
        for (var i = 1; i <= 3; i++)
        {
            var t = now.AddMinutes(15 * i);
            var summary = await service.ReconcileAsync(t, ct);
            await service.EnsureCoverageAsync(t, ct);
            await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct, $"Lauf {i}");
        }

        var scheduled = await ScheduledAsync(fixture, ct);
        var ordered = scheduled.OrderBy(s => s.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].Start >= ordered[i - 1].End,
                $"Überlappende geplante Slots: {ordered[i - 1].Start:o}-{ordered[i - 1].End:o} und {ordered[i].Start:o}-{ordered[i].End:o}");
        ProgramScheduleTestDb.AssertGapless(scheduled, now.AddMinutes(45).UtcDateTime, now.UtcDateTime.AddHours(48));
    }

    [Fact]
    public async Task GetSchedule_ReturnsUpcomingSlotsWithStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-getschedule", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        rule.IsActive = false;
        await fixture.SaveDiagAsync();
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        var slots = await service.GetScheduleAsync(channel.Id, now.AddHours(-2), 200, ct);

        Assert.NotEmpty(slots);
        Assert.Contains(slots, s => s.Status == ProgramSlotStatus.Displaced);
        Assert.All(slots, s => Assert.True(s.Start >= now.UtcDateTime.AddHours(-2)));
    }

    // Issue #261: a broadcast whose real start lies more than 24 h before the coverage start
    // must stay reproducible — the reconcile lookback widens (up to MaxOccurrenceLookbackDays)
    // so the occurrence anchor stays provable. Under a daily rule the 30 h airings chain
    // seamlessly (each following occurrence is shifted behind the running tail), so
    // GroupAirings merges them into a single multi-occurrence chain.
    [Fact]
    public async Task Reconcile_LongAiring_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-airing", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Marathonfilm", duration: TimeSpan.FromHours(30));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 1, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Marathon", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var marathonSlots = (await ScheduledAsync(fixture, ct))
            .Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.True(marathonSlots.Count >= 2, "erwartete verschmolzene Kette fehlt");
        Assert.Equal(marathonSlots[0].End, marathonSlots[1].Start); // lückenlose Kette

        // 38 h after the first occurrence's nominal start — the chain head lies outside the
        // 24-h base window and must still be recognized as reproducible.
        var check = new DateTimeOffset(LocalUtc(2026, 11, 3, 10, 0));
        var summary = await service.ReconcileAsync(check, ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);

        // Second run stays equally quiet — no oscillation.
        var summary2 = await service.ReconcileAsync(check.AddMinutes(25), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "2. Lauf");
    }

    // Same long-airing scenario beyond two days: the occurrence day loop must reach back as far
    // as the lookback allows, not just to the coverage day minus one.
    [Fact]
    public async Task Reconcile_LongAiring_BeyondTwoDays_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-airing-2d", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Langfilm", duration: TimeSpan.FromHours(50));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 1, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Marathon", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        var check = new DateTimeOffset(LocalUtc(2026, 11, 4, 12, 0));
        var summary = await service.ReconcileAsync(check, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        Assert.All(await fixture.SlotsAsync(ct),
            s => Assert.Equal(ProgramSlotStatus.Scheduled, s.Status));
    }

    // Marathon block as a chain of many single slots: head slots end before the base load
    // window edge, the chain reaches over the coverage start, no single slot spans both
    // boundaries — the widening trigger (a slot with Start <= loadFrom) fires.
    [Fact]
    public async Task Reconcile_MultiPartLongAiring_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-multipart-long", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Marathon", 50, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 1, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Marathonblock", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Once,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id,
            slotDuration: TimeSpan.FromHours(30), singleDate: new DateOnly(2026, 11, 1));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // 25 h into the 30 h block: one interior slot covers the base window edge, the head
        // slots are already out of the load window.
        var check = new DateTimeOffset(LocalUtc(2026, 11, 2, 21, 7));
        var summary = await service.ReconcileAsync(check, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
    }

    // Aired with a shifted real start: the predecessor airing overshoots the occurrence start,
    // so the > 24 h airing begins late — the invader slot proving the shift ends outside the
    // base load window and must be loaded by the widened window.
    [Fact]
    public async Task Reconcile_LongAiring_Shifted_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-shifted", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var predecessor = await fixture.CreateMovieAsync("Vorprogramm", duration: TimeSpan.FromHours(8));
        var movie = await fixture.CreateMovieAsync("Langfilm", duration: TimeSpan.FromHours(30));

        // Coverage built before the predecessor's airing so it is persisted and can overshoot
        // the 20:00 occurrence — the long airing's real start shifts to 22:00.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 31, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.Movie, predecessor.Id, episodeCount: 1);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // The 20:00 airing was shifted behind the predecessor's overshoot — its real start is
        // the invader's end. More than 24 h later the invader slot already ended before the
        // base load window and its evidence must be loaded by the widened window.
        var check = new DateTimeOffset(LocalUtc(2026, 11, 2, 23, 0));
        var summary = await service.ReconcileAsync(check, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        Assert.Contains(await ScheduledAsync(fixture, ct),
            s => s.ProgramRuleId == rule.Id && s.Start < check.UtcDateTime.AddHours(-24));
    }

    // Rest gap k > 0 without a trigger hit: young plan history — the tail of a long airing was
    // materialized with its head items consumed without slot rows, so the first written slot
    // starts after loadFrom and nothing covers the window edge. The validation-driven widening
    // (base validation fails, reload on the max window, re-validate) must still rescue it.
    [Fact]
    public async Task Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-notrigger", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Marathon", 10, duration: TimeSpan.FromHours(9));

        // One-off 36 h block from Nov 1 11:00 (4 x 9 h): the ensure run at Nov 2 09:00 lands
        // inside the airing, so only the tail slots are written — the head items consumed the
        // cursor without producing rows (k > 0 reconstruction needed).
        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 9, 0));
        await fixture.CreateRuleAsync(channel.Id, "Marathon", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Once,
            new TimeOnly(11, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 4,
            singleDate: new DateOnly(2026, 11, 1));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var allSlots = (await fixture.SlotsAsync(ct))
            .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} ep={s.TVShowEpisodeId} st={s.Status}").ToList();

        // 26 h after the airing's nominal start, only 4 h after the plan was built: no loaded
        // slot starts at or before loadFrom — the trigger provably does not fire.
        var check = new DateTimeOffset(LocalUtc(2026, 11, 2, 13, 0));
        var summary = await service.ReconcileAsync(check, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct,
            "Ausgangsplan: " + string.Join(" | ", allSlots));
    }

    // Genuine staleness must still displace: after the rule's time of day changed, the > 24 h
    // chain finds neither an anchor nor invader evidence — widening must not rescue it.
    [Fact]
    public async Task Reconcile_LongAiring_ChangedRule_StillDisplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-changed", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Marathonfilm", duration: TimeSpan.FromHours(30));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 1, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Marathon", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        rule.TimeOfDay = new TimeOnly(22, 0);
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 11, 3, 10, 0)), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        Assert.Contains(await DisplacedAsync(fixture, ct), s => s.ProgramRuleId == rule.Id);
        // Re-planned at the new time.
        Assert.Contains(await ScheduledAsync(fixture, ct), s => s.ProgramRuleId == rule.Id);
    }

    // Documented bound: airings whose real start is older than MaxOccurrenceLookbackDays are
    // still displaced and re-planned (deliberate, configurable limit).
    [Fact]
    public async Task Reconcile_LongAiring_BeyondMaxLookback_Displaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-long-maxlookback", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Marathonfilm", duration: TimeSpan.FromHours(30));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 1, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Marathon", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);

        var service = fixture.CreateService(Microsoft.Extensions.Options.Options.Create(
            new Configuration.ProgramScheduleOptions { MaxOccurrenceLookbackDays = 1 }));
        await service.EnsureCoverageAsync(now, ct);

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 11, 3, 10, 0)), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        Assert.Contains(await DisplacedAsync(fixture, ct), s => s.ProgramRuleId == rule.Id);
        Assert.Contains(await ScheduledAsync(fixture, ct), s => s.ProgramRuleId == rule.Id);
    }

    // Issue #290 reproduction: the movie's lower-id file is unprobed, so the second (higher-id)
    // link is the representative file the plan is written with. Once the probe completes on the
    // first file the representative flips — the airing itself is unchanged (same content, same
    // positions) and must stay scheduled; today the strict MediaItemId check displaces it.
    [Fact]
    public async Task Reconcile_MovieSecondMediaItemProbed_SlotStaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-probe-flip-movie", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var seededItemId = (await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).ToListAsync(ct)).Single();
        // Second, higher-id link — probed; the seeded item loses its duration (probe pending).
        var secondItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieMediaItemAsync(movie.Id, secondItem.Id);
        await fixture.Db.MediaItems.Where(m => m.Id == seededItemId)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)null), ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var movieSlots = (await ScheduledAsync(fixture, ct)).Where(s => s.MovieId == movie.Id).ToList();
        Assert.NotEmpty(movieSlots);
        Assert.All(movieSlots, s => Assert.Equal(secondItem.Id, s.MediaItemId));

        // The probe completes on the seeded (lower-id) file — the representative flips while
        // the written airing stays the same content at the same positions.
        await fixture.Db.MediaItems.Where(m => m.Id == seededItemId)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)TimeSpan.FromMinutes(120)), ct);

        var summary = await service.ReconcileAsync(now, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        var after = (await ScheduledAsync(fixture, ct)).Where(s => s.MovieId == movie.Id).ToList();
        Assert.Equal(movieSlots.Select(s => (s.Start, s.End, s.MediaItemId)).ToList(),
            after.Select(s => (s.Start, s.End, s.MediaItemId)).ToList());
        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "2. Lauf");
    }

    // Episode counterpart of the representative flip: the seeded item of each episode is
    // unprobed until after the coverage run — the second link carries the slot's MediaItemId.
    [Fact]
    public async Task Reconcile_EpisodeSecondMediaItemProbed_SlotStaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-probe-flip-episode", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 4, duration: TimeSpan.FromMinutes(45));

        var seededItemIds = await fixture.Db.TVShowEpisodeMediaItems
            .Where(l => l.TVShowEpisode.TVShowSeason.TVShowId == show.Id)
            .Select(l => l.MediaItemId).ToListAsync(ct);
        foreach (var ep in episodes)
        {
            var secondItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(45));
            await fixture.LinkEpisodeMediaItemAsync(ep.Id, secondItem.Id);
        }
        await fixture.Db.MediaItems.Where(m => seededItemIds.Contains(m.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)null), ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 2);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var episodeSlots = (await ScheduledAsync(fixture, ct)).Where(s => s.TVShowEpisodeId is not null).ToList();
        Assert.NotEmpty(episodeSlots);
        Assert.All(episodeSlots, s => Assert.DoesNotContain(s.MediaItemId, seededItemIds));

        await fixture.Db.MediaItems.Where(m => seededItemIds.Contains(m.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Duration, (TimeSpan?)TimeSpan.FromMinutes(50)), ct);

        var summary = await service.ReconcileAsync(now, ct);

        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct);
        var after = (await ScheduledAsync(fixture, ct)).Where(s => s.TVShowEpisodeId is not null).ToList();
        Assert.Equal(episodeSlots.Select(s => (s.Start, s.End, s.MediaItemId)).ToList(),
            after.Select(s => (s.Start, s.End, s.MediaItemId)).ToList());
        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "2. Lauf");
    }

    // Guard for the tolerant slot check: unlinking the stored file is a real change — the
    // airing is displaced exactly once and re-materialized with the remaining link; the
    // follow-up run is stable.
    [Fact]
    public async Task Reconcile_UnlinkedMediaItem_StillDisplacesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-unlinked-item", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var seededItemId = (await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).ToListAsync(ct)).Single();
        var secondItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieMediaItemAsync(movie.Id, secondItem.Id);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var movieSlotCount = (await ScheduledAsync(fixture, ct)).Count(s => s.MovieId == movie.Id);
        Assert.True(movieSlotCount > 0);

        // Real change: the stored file leaves the movie — the slots must be replaced once.
        await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id && l.MediaItemId == seededItemId)
            .ExecuteDeleteAsync(ct);

        var summary = await service.ReconcileAsync(now, ct);

        var displaced = (await DisplacedAsync(fixture, ct)).Where(s => s.MovieId == movie.Id).ToList();
        Assert.Equal(movieSlotCount, displaced.Count);
        Assert.All(displaced, s => Assert.Equal(seededItemId, s.MediaItemId));
        var rescheduled = (await ScheduledAsync(fixture, ct)).Where(s => s.MovieId == movie.Id).ToList();
        Assert.Equal(movieSlotCount, rescheduled.Count);
        Assert.All(rescheduled, s => Assert.Equal(secondItem.Id, s.MediaItemId));

        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "2. Lauf");
    }

    // Issue #290 baseline: two reconcile runs with an identical time anchor and unchanged
    // inputs — the second run must be a complete no-op (no displaced copies, no re-created
    // slots). A failure here means the reconcile check is not idempotent even without any
    // input change.
    [Fact]
    public async Task Reconcile_IdenticalUtcNow_SecondRunIsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("reconcile-identical-now", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        await service.ReconcileAsync(now, ct);
        var second = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "2. Lauf");
    }
}
