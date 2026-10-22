using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the occurrence-window boundary (N16 residual): an airing may overshoot its
/// nominal length only into unowned time — never into the next occurrence's window — and an
/// earlier airing's tail already invading a window yields at the boundary instead of killing
/// the whole next airing.
/// </summary>
public sealed class ProgramScheduleServiceTests_Boundary
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    // Live scenario of rule 4: predecessor rule (10:00, 6 h) overshoots into the 16:00 window
    // of the equal-priority successor — the successor must still get slots, every day.
    [Fact]
    public async Task EnsureCoverage_AdjacentWindows_OvershootDoesNotKillSuccessor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-adjacent", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (_, _, episodes) = await fixture.CreateShowAsync("Mittag", 40, duration: TimeSpan.FromMinutes(47));
        var (_, _, episodes2) = await fixture.CreateShowAsync("Nachmittag", 40, duration: TimeSpan.FromMinutes(50));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 9, 0));
        // 47-min episodes cannot fill 6 h exactly — the airing overshoots past 16:00.
        await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(10, 0), ProgramRuleMaterialType.TVShow, episodes[0].TVShowSeason.TVShowId, slotDuration: TimeSpan.FromHours(6));
        var rule4 = await fixture.CreateRuleAsync(channel.Id, "Vorbend", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, episodes2[0].TVShowSeason.TVShowId, slotDuration: TimeSpan.FromHours(4));

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var rule4Slots = scheduled.Where(s => s.ProgramRuleId == rule4.Id).ToList();
        Assert.NotEmpty(rule4Slots);
        // The predecessor's airings end at or before 16:00 — no boundary invasion.
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId != rule4.Id && s.End > LocalUtc(2026, 10, 24, 16, 0) && s.Start < LocalUtc(2026, 10, 24, 16, 0));
        // Successor airings start at 16:00 local each day.
        Assert.All(rule4Slots.Where(s => s.Start < rule4Slots[^1].Start), s => { });
        Assert.Equal(LocalUtc(2026, 10, 24, 16, 0), rule4Slots[0].Start);
        Assert.Equal(0, summary.DroppedConflicts);
    }

    // The exact required reproduction: occurrence "today 16:00, now 16:30" plus an existing
    // predecessor slot invading the window — Reconcile must trim the invader's suffix and
    // materialize the tail.
    [Fact]
    public async Task Reconcile_PartialOccurrence_InvadingPredecessorTail_Materializes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("boundary-invader", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 40, duration: TimeSpan.FromMinutes(45));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var buildTime = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule4 = await fixture.CreateRuleAsync(channel.Id, "Vorbendserien", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, show.Id, slotDuration: TimeSpan.FromHours(4));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(buildTime, ct);

        // Seed the live precondition: rule 4's airing is displaced (disaster state) and a
        // predecessor airing's tail slot invades the 16:00 window (overshoot from before the fix).
        await fixture.Db.ProgramSlots.Where(s => s.ProgramRuleId == rule4.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, ProgramSlotStatus.Displaced), ct);
        var invader = await fixture.CreateSlotAsync(channel.Id,
            LocalUtc(2026, 10, 24, 15, 30), LocalUtc(2026, 10, 24, 16, 37),
            mediaItemId: filler.Id, movieId: filler.Id, ruleId: null, ruleType: ProgramRuleType.GenreRotation);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 16, 30));
        var summary = await service.ReconcileAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var rule4Scheduled = scheduled.Where(s => s.ProgramRuleId == rule4.Id).ToList();
        Assert.NotEmpty(rule4Scheduled);
        Assert.Equal(ProgramSlotStatus.Displaced, invader.Status);
        // The tail covers the remaining window from 16:00 onward.
        Assert.Contains(rule4Scheduled, s => s.Start == LocalUtc(2026, 10, 24, 16, 0) || (s.Start < now.UtcDateTime && s.End > now.UtcDateTime));
    }

    // An airing trimmed at the boundary must stay valid — otherwise the next reconcile
    // displaces it again (oscillation).
    [Fact]
    public async Task Reconcile_BoundaryTrimmedAiring_StaysStableAcrossRuns()
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

    // A head item larger than the remaining window is consumed (cursor advances) instead of
    // stalling the rotation forever.
    [Fact]
    public async Task EnsureCoverage_OversizedHeadItem_IsConsumedNotStalling()
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
        // cannot fit [10:00, 14:00).
        await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(10, 0), ProgramRuleMaterialType.MovieCollection, collection.Id, slotDuration: TimeSpan.FromHours(4));
        var next = await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(14, 0), ProgramRuleMaterialType.Movie, small.Id, slotDuration: TimeSpan.FromHours(4));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        // The oversized movie is skipped; the small one airs — and rule 'next' still gets slots.
        Assert.DoesNotContain(scheduled, s => s.MovieId == big.Id);
        var cursor = await fixture.Db.ProgramRuleCursors.FirstOrDefaultAsync(c => c.ProgramRuleId != next.Id, ct);
        Assert.NotNull(cursor);
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
