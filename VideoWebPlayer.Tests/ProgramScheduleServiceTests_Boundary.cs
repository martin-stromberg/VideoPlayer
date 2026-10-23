using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the occurrence-window boundary (N16 residual + N22): a begun item always airs to
/// the end — it may start while at least the minimum remainder is left before the boundary and
/// legitimately overshoots into the next window, shifting the following airing behind its real
/// end. Against a higher-priority next occurrence (hard bound) only items that can complete
/// inside the window start; everything else defers — videos are never cut.
/// </summary>
public sealed class ProgramScheduleServiceTests_Boundary
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    // Live scenario of rule 4: predecessor rule (10:00, 6 h) overshoots into the 16:00 window
    // of the equal-priority successor — the last episode runs to its end (no cut) and the
    // successor's airing starts behind it, every day.
    [Fact]
    public async Task EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-adjacent", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (_, _, episodes) = await fixture.CreateShowAsync("Mittag", 40, duration: TimeSpan.FromMinutes(47));
        var (_, _, episodes2) = await fixture.CreateShowAsync("Nachmittag", 40, duration: TimeSpan.FromMinutes(50));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0));
        // 47-min episodes cannot fill 6 h exactly — the eighth episode (15:29) legitimately
        // overshoots the 16:00 boundary and completes at 16:16.
        await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(10, 0), ProgramRuleMaterialType.TVShow, episodes[0].TVShowSeason.TVShowId, slotDuration: TimeSpan.FromHours(6));
        var rule4 = await fixture.CreateRuleAsync(channel.Id, "Vorbend", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, episodes2[0].TVShowSeason.TVShowId, slotDuration: TimeSpan.FromHours(4));

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var rule4Slots = scheduled.Where(s => s.ProgramRuleId == rule4.Id).ToList();
        Assert.NotEmpty(rule4Slots);
        // The overshooting episode keeps its full duration — never truncated at the boundary.
        var overshoot = scheduled.Single(s => s.Start < LocalUtc(2026, 10, 24, 16, 0) && s.End > LocalUtc(2026, 10, 24, 16, 0));
        Assert.Equal(TimeSpan.FromMinutes(47), overshoot.End - overshoot.Start);
        // The successor airing shifts behind the overshooting tail: 16:16 local.
        Assert.Equal(overshoot.End, rule4Slots[0].Start);
        Assert.Equal(LocalUtc(2026, 10, 24, 16, 16), rule4Slots[0].Start);
        Assert.Equal(0, summary.DroppedConflicts);
    }

    // N22 reproduction: occurrence "today 16:00, now 16:30" with a predecessor airing's tail
    // legitimately overshooting into the window — Reconcile must NOT cut the begun video; the
    // re-materialized tail starts behind its real end.
    [Fact]
    public async Task Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-invader", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 40, duration: TimeSpan.FromMinutes(45));
        var movie = await fixture.CreateMovieAsync("Vormittagsfilm", duration: TimeSpan.FromMinutes(90));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var buildTime = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // Predecessor at 15:30 with a 90-min movie: legitimately overshoots the 16:00 window,
        // completes at 17:00 (the "video must never be cut" case).
        var pred = await fixture.CreateRuleAsync(channel.Id, "Vormittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(15, 30), ProgramRuleMaterialType.Movie, movie.Id);
        var rule4 = await fixture.CreateRuleAsync(channel.Id, "Vorbendserien", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, show.Id, slotDuration: TimeSpan.FromHours(4));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(buildTime, ct);

        var invader = (await ScheduledAsync(fixture, ct))
            .First(s => s.ProgramRuleId == pred.Id && s.Start == LocalUtc(2026, 10, 24, 15, 30));
        Assert.Equal(LocalUtc(2026, 10, 24, 17, 0), invader.End); // full 90 min — not cut

        // Disaster state: rule 4's materialization is lost and must be rebuilt mid-occurrence.
        await fixture.Db.ProgramSlots.Where(s => s.ProgramRuleId == rule4.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, ProgramSlotStatus.Displaced), ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 16, 30));
        await service.ReconcileAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var rule4Scheduled = scheduled.Where(s => s.ProgramRuleId == rule4.Id).ToList();
        Assert.NotEmpty(rule4Scheduled);
        // The begun movie airs to the end — the tail is re-materialized behind it.
        Assert.Equal(ProgramSlotStatus.Scheduled, invader.Status);
        Assert.Equal(LocalUtc(2026, 10, 24, 17, 0), rule4Scheduled[0].Start);
    }

    // Overshooting and shifted airings must stay valid — otherwise the next reconcile
    // displaces them again (oscillation).
    [Fact]
    public async Task Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-stable", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 60, duration: TimeSpan.FromMinutes(47));
        var (show2, _, _) = await fixture.CreateShowAsync("Serie2", 60, duration: TimeSpan.FromMinutes(50));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0));
        await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(10, 0), ProgramRuleMaterialType.TVShow, show.Id, slotDuration: TimeSpan.FromHours(6));
        await fixture.CreateRuleAsync(channel.Id, "Vorbend", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, show2.Id, slotDuration: TimeSpan.FromHours(4));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        for (var i = 1; i <= 3; i++)
        {
            var t = now.AddMinutes(15 * i);
            var summary = await service.ReconcileAsync(t, ct);
            Assert.True(summary.SlotsDisplaced == 0,
                $"Lauf {i}: {summary.SlotsDisplaced} verdrängt — " + string.Join(" | ",
                    (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced)
                        .Select(s => $"{s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId}")));
            await service.EnsureCoverageAsync(t, ct);
        }
    }

    // A head item larger than the remaining window is no longer consumed or skipped — it airs
    // completely and overshoots; the successor's airing starts behind its end.
    [Fact]
    public async Task EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-oversized", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        var big = await fixture.CreateMovieAsync("Riesenfilm", collectionId: collection.Id, duration: TimeSpan.FromMinutes(300), releaseDate: new DateTime(2000, 1, 1));
        var small = await fixture.CreateMovieAsync("Kurzfilm", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2001, 1, 1));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0));
        // Two adjacent daily windows: 10:00 (4 h) and 14:00 (4 h) — the 300-min head item
        // cannot fit [10:00, 14:00) and still airs in full: 10:00–15:00.
        await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(10, 0), ProgramRuleMaterialType.MovieCollection, collection.Id, slotDuration: TimeSpan.FromHours(4));
        var next = await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.Movie, small.Id, slotDuration: TimeSpan.FromHours(4));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        // The big movie airs — complete, never cut or silently skipped. It crosses the 14:00
        // boundary (the lookback occurrence already consumed the ring head, so today the small
        // one leads; the big one follows and overshoots to 16:00).
        var bigSlot = Assert.Single(scheduled, s => s.MovieId == big.Id
            && s.Start < LocalUtc(2026, 10, 24, 14, 0) && s.End > LocalUtc(2026, 10, 24, 14, 0));
        Assert.Equal(LocalUtc(2026, 10, 24, 16, 0), bigSlot.End);
        // The successor is not displaced or dropped — it simply starts behind the overshoot.
        var nextSlots = scheduled.Where(s => s.ProgramRuleId == next.Id).ToList();
        Assert.NotEmpty(nextSlots);
        Assert.Equal(bigSlot.End, nextSlots[0].Start);
    }

    // N22 headline case: 3 minutes remainder before the next window + a 45-minute item —
    // the item starts and airs to the end (45 min, ending past the boundary), the successor
    // shifts; no slot is ever truncated.
    [Fact]
    public async Task EnsureCoverage_ShortRemainder_OvershootingItem_Completes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-short-remainder", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Langfilm", duration: TimeSpan.FromMinutes(45));
        var nextMovie = await fixture.CreateMovieAsync("Danach", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(19, 57), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);
        var next = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, nextMovie.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var slot = Assert.Single(scheduled, s => s.ProgramRuleId == rule.Id && s.Start == LocalUtc(2026, 10, 24, 19, 57));
        Assert.Equal(TimeSpan.FromMinutes(45), slot.End - slot.Start); // complete — never 3 min
        Assert.Equal(LocalUtc(2026, 10, 24, 20, 42), slot.End);
        var nextSlots = scheduled.Where(s => s.ProgramRuleId == next.Id).ToList();
        Assert.NotEmpty(nextSlots);
        Assert.Equal(slot.End, nextSlots[0].Start); // shifted behind the overshoot
    }

    // Below the configured minimum start remainder (default 30 s) nothing is scheduled —
    // fillers take the gap instead.
    [Fact]
    public async Task EnsureCoverage_RemainderBelowMinRemain_PlansFiller()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-min-remainder", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Langfilm", duration: TimeSpan.FromMinutes(45));
        var nextMovie = await fixture.CreateMovieAsync("Danach", duration: TimeSpan.FromMinutes(30));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(10));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // 15 seconds before the next window — below the 30 s minimum start remainder.
        var rule = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(19, 59, 45), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1);
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, nextMovie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == rule.Id);
        // The 15 s remainder plus the rest of the day is covered — by filler where the rule
        // produced nothing.
        Assert.Contains(scheduled, s => s.RuleType == ProgramRuleType.Filler && s.End > LocalUtc(2026, 10, 24, 19, 59) + TimeSpan.FromSeconds(45) && s.Start < LocalUtc(2026, 10, 24, 20, 0));
    }

    // A higher-priority next occurrence is a hard bound: an item that could not complete
    // before it defers instead of being scheduled and cut — on the bound day the occurrence
    // yields nothing; on unbound days the same ring airs its items in order (nothing skipped).
    // (2026-10-24 is a Saturday, 2026-10-25 a Sunday.)
    [Fact]
    public async Task EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-hard", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        var longMovie = await fixture.CreateMovieAsync("Lang", collectionId: collection.Id, duration: TimeSpan.FromMinutes(45), releaseDate: new DateTime(2000, 1, 1));
        await fixture.CreateMovieAsync("Kurz", collectionId: collection.Id, duration: TimeSpan.FromMinutes(2), releaseDate: new DateTime(2001, 1, 1));
        var prime = await fixture.CreateMovieAsync("Prime", duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(19, 0), ProgramRuleMaterialType.MovieCollection, collection.Id, priority: 1, episodeCount: 2);
        // Only Sundays at 19:30 a higher-priority rule turns the window into a hard bound.
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Weekly,
            new TimeOnly(19, 30), ProgramRuleMaterialType.Movie, prime.Id, priority: 10, dayOfWeek: DayOfWeek.Sunday);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        // Saturday is unbound — both ring items air in order (19:00–19:45, 19:45–19:47).
        Assert.Contains(scheduled, s => s.MovieId == longMovie.Id && s.Start == LocalUtc(2026, 10, 24, 19, 0));
        // Sunday defers: the 45-min head cannot complete before the 19:30 hard bound —
        // nothing of this rule is scheduled, nothing is cut.
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == rule.Id && s.Start.Day == 25);
        Assert.Contains(scheduled, s => s.MovieId == prime.Id && s.Start == LocalUtc(2026, 10, 25, 19, 30));
    }

    [Fact]
    public void ProgramScheduleOptions_MinStartRemain_Normalizes()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), new Configuration.ProgramScheduleOptions { MinStartRemainSeconds = 0 }.EffectiveMinStartRemain);
        Assert.Equal(TimeSpan.FromSeconds(30), new Configuration.ProgramScheduleOptions { MinStartRemainSeconds = -5 }.EffectiveMinStartRemain);
        Assert.Equal(TimeSpan.FromSeconds(45), new Configuration.ProgramScheduleOptions { MinStartRemainSeconds = 45 }.EffectiveMinStartRemain);
        Assert.Equal(TimeSpan.FromSeconds(30), new Configuration.ProgramScheduleOptions().EffectiveMinStartRemain);
    }

    [Fact]
    public async Task DeleteDisplacedSlots_RemovesOnlyDisplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-displaced", ct);
        var admin = await fixture.AddUserAsync("admin@x.de", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(60));
        await fixture.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await service.EnsureCoverageAsync(now, ct);

        // Seed displaced leftovers directly (as a re-evaluation would leave them).
        await fixture.CreateSlotAsync(channel.Id, LocalUtc(2026, 10, 24, 14, 0), LocalUtc(2026, 10, 24, 15, 0),
            movie.Id, movieId: movie.Id, status: ProgramSlotStatus.Displaced);
        await fixture.CreateSlotAsync(channel.Id, LocalUtc(2026, 10, 24, 15, 0), LocalUtc(2026, 10, 24, 16, 0),
            movie.Id, movieId: movie.Id, status: ProgramSlotStatus.Displaced);

        var all = await fixture.SlotsAsync(ct);
        Assert.Contains(all, s => s.Status == ProgramSlotStatus.Displaced);
        Assert.Contains(all, s => s.Status == ProgramSlotStatus.Scheduled);
        var displacedCount = all.Count(s => s.Status == ProgramSlotStatus.Displaced);

        var deleted = await service.DeleteDisplacedSlotsAsync(admin.Id, channel.Id, ct);

        Assert.Equal(displacedCount, deleted);
        var remaining = await fixture.SlotsAsync(ct);
        Assert.DoesNotContain(remaining, s => s.Status == ProgramSlotStatus.Displaced);
        Assert.All(remaining, s => Assert.Equal(ProgramSlotStatus.Scheduled, s.Status));
    }

    [Fact]
    public async Task DeleteDisplacedSlots_NonAdmin_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-auth", ct);
        var user = await fixture.AddUserAsync("user@x.de", isAdmin: false);
        var channel = await fixture.GetChannelAsync(ct);
        var service = fixture.CreateService();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.DeleteDisplacedSlotsAsync(user.Id, channel.Id, ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.ResetScheduleAsync(user.Id, channel.Id, ct));
    }

    [Fact]
    public async Task ResetSchedule_RemovesAllSlotsAndCursors()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-reset", ct);
        var admin = await fixture.AddUserAsync("admin@x.de", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(60));
        await fixture.CreateRuleAsync(channel.Id, "R", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);
        Assert.NotEmpty(await fixture.SlotsAsync(ct));
        Assert.NotEmpty(await fixture.Db.ProgramRuleCursors.ToListAsync(ct));

        var result = await service.ResetScheduleAsync(admin.Id, channel.Id, ct);

        Assert.True(result.SlotsDeleted > 0);
        Assert.True(result.CursorsDeleted > 0);
        Assert.Empty(await fixture.SlotsAsync(ct));
        Assert.Empty(await fixture.Db.ProgramRuleCursors.ToListAsync(ct));
    }
}
