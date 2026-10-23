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

        var dumped = (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
            .Select(s => $"{s.Start:HH:mm}-{s.End:HH:mm} {s.RuleType} rule={s.ProgramRuleId} item={s.MediaItemId}").ToList();
        Assert.True(summary.SlotsDisplaced == 0, "Displaced: " + string.Join(" | ", dumped));
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
        var dumped = (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
            .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId} ep={s.TVShowEpisodeId}").ToList();
        Assert.True(summary.SlotsDisplaced == 0, "Displaced: " + string.Join(" | ", dumped));

        // And a second run must stay equally quiet — no oscillation.
        var summary2 = await service.ReconcileAsync(now.AddMinutes(25), ct);
        Assert.True(summary2.SlotsDisplaced == 0, "Displaced (2. Lauf): " + string.Join(" | ",
            (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
                .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId}")));
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
            Assert.True(summary.SlotsDisplaced == 0, $"Lauf {i}: {summary.SlotsDisplaced} verdrängt — " + string.Join(" | ",
                (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
                    .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId}")));
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
}
