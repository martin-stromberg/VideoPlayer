using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression tests for issue #315: the rolling re-evaluation invalidated an unchanged,
/// still-running interleaved airing with <c>HeadPickInconsistent</c> whenever a strand had
/// only supplied unwritten head picks (taken but never written because the coverage run
/// began mid-occurrence) and no written contribution in the remaining group — displacing
/// and re-materializing the identical tail in an endless loop that killed the running title.
/// Also covers: fully aired slots are never re-evaluated, and the slot carrying the
/// playback position is never cut mid-air unless the current age-window settings already
/// forbid it at its own start.
/// </summary>
public sealed class ProgramScheduleServiceTests_Issue315
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    // Three-strand genre rotation (three shows à 45 min) with a clip group, daily 20:00,
    // 4 h slot: six picks per occurrence rotating s0,s1,s2,s0,s1,s2.
    private static async Task<(ProgramScheduleTestDb Fixture, ProgramRule Rule)> SeedInterleavedClipRuleAsync(
        string dbName, CancellationToken ct)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Mix", 1);
        foreach (var name in new[] { "S1", "S2", "S3" })
        {
            var (show, _, _) = await fixture.CreateShowAsync(name, 10, 1, TimeSpan.FromMinutes(45));
            await fixture.LinkShowGenreAsync(show.Id, genre.Id);
        }
        var group = await fixture.CreateClipGroupAsync("G", ct);
        for (var i = 0; i < 20; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
            await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        }
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genre", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            slotDuration: TimeSpan.FromHours(4), genreIds: [genre.Id], clipGroupIds: [group.Id]);
        return (fixture, rule);
    }

    // Core failure of the issue: coverage began inside pick 4 of the occurrence, so the
    // written tail {s1, s2} leaves prefix strand s0 without any written contribution —
    // the check used to reject every hypothesis (HeadPickInconsistent) although the
    // airing is a faithful materialization. Must stay stable, repeatedly.
    [Fact]
    public async Task Reconcile_UnchangedRules_RunningAiringWithUnwrittenStrandHead_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var messages = new ConcurrentQueue<string>();
        var (fixture, _) = await SeedInterleavedClipRuleAsync("i315-unwritten-head", ct);
        using var _ = fixture;
        var service = fixture.CreateService(logger: new ListLogger<ProgramScheduleService>(messages));

        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 20)), ct);
        var before = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 35)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct, "Lauf 1");

        // Second run stays quiet — no oscillation.
        var summary2 = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 23, 50)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "Lauf 2");

        var after = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.All(before, b => Assert.Contains(after, a => a == b));
        Assert.DoesNotContain(messages, m => m.Contains("HeadPickInconsistent"));
    }

    // Already aired slots are never re-evaluated or re-planned: a running airing whose
    // tail is displaced for a real reason (rule deactivated) leaves the fully aired
    // history bit-identical, and the merged survivor+new-tail layout stays stable over
    // repeated reconciles (no churn).
    [Fact]
    public async Task Reconcile_AiredHistoryUntouched_MergedTailStaysStable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, rule) = await SeedInterleavedClipRuleAsync("i315-merge", ct);
        using var _ = fixture;
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);

        var t1 = LocalUtc(2026, 10, 24, 21, 0);
        var aired = (await fixture.SlotsAsync(ct))
            .Where(s => s.End <= t1 && s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId))
            .ToList();

        // Real change mid-airing: deactivate → tail displaced → reactivate → tail rebuilt.
        rule.IsActive = false;
        await fixture.SaveDiagAsync();
        var displaced = await service.ReconcileAsync(new DateTimeOffset(t1), ct);
        Assert.True(displaced.SlotsDisplaced > 0);
        rule.IsActive = true;
        await fixture.SaveDiagAsync();

        var failures = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            var t = t1.AddMinutes(15 * (i + 1));
            var summary = await service.ReconcileAsync(new DateTimeOffset(t), ct);
            await service.EnsureCoverageAsync(new DateTimeOffset(t), ct);
            if (summary.SlotsDisplaced > 0)
                failures.Add($"t={t:dd.HH:mm} displaced={summary.SlotsDisplaced}");
        }

        var airedAfter = (await fixture.SlotsAsync(ct))
            .Where(s => s.End <= t1 && s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId))
            .ToList();
        Assert.Equal(aired, airedAfter); // history untouched — never re-evaluated
        Assert.Empty(failures); // no oscillation of the merged survivor+tail layout
    }

    // The slot carrying the playback position is never cut mid-air: a real invalidation
    // (rule deactivated) displaces the airing's future, but the running slot plays to
    // its end — the only legitimate kill is a tightened age restriction.
    [Fact]
    public async Task Reconcile_InvalidAiring_RunningSlotRunsToItsEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, rule) = await SeedInterleavedClipRuleAsync("i315-running", ct);
        using var _ = fixture;
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);

        var t1 = LocalUtc(2026, 10, 24, 21, 0);
        var running = (await fixture.SlotsAsync(ct))
            .Single(s => s.Start < t1 && s.End > t1);

        rule.IsActive = false;
        await fixture.SaveDiagAsync();
        var summary = await service.ReconcileAsync(new DateTimeOffset(t1), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var runningAfter = await fixture.Db.ProgramSlots.AsNoTracking().SingleAsync(s => s.Id == running.Id, ct);
        Assert.Equal(ProgramSlotStatus.Scheduled, runningAfter.Status); // never cut mid-air
    }

    // The only legitimate kill mid-air: the current age-window settings already forbid
    // the running item at its own slot start (restrictions tightened).
    [Fact]
    public async Task Reconcile_RunningSlot_AgeWindowBlocksAtOwnStart_IsDisplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("i315-agekill", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Unrated-Film", duration: TimeSpan.FromMinutes(90));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Film", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 8, 0)), ct);

        var t1 = LocalUtc(2026, 10, 24, 13, 0);
        var running = (await fixture.SlotsAsync(ct))
            .Single(s => s.ProgramRuleId == rule.Id && s.Start < t1 && s.End > t1);

        // Restrictions tightened: unrated content may only air inside the night window —
        // at 12:00 the running item is forbidden at its own start and is legitimately
        // killed mid-air.
        await new ProgramSettingsService(fixture.Db, NullLogger<ProgramSettingsService>.Instance)
            .UpdateProgramScheduleUnknownRatingModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var summary = await service.ReconcileAsync(new DateTimeOffset(t1), ct);

        var runningAfter = await fixture.Db.ProgramSlots.AsNoTracking().SingleAsync(s => s.Id == running.Id, ct);
        Assert.Equal(ProgramSlotStatus.Displaced, runningAfter.Status);
        Assert.True(summary.SlotsDisplaced > 0);
    }
}
