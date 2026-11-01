using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression tests for issue #320 (follow-up of #315): the interleaved tail check
/// (<c>InterleavedAiringTailReproducible</c>) invalidates faithfully materialized airings
/// in two more cases — both only conservative (displacement churn, never false retention):
/// 1. Deferral gap: a strand that deferred mid-occurrence (age-blocked ordered head or a
///    bound that forbids the take) produces no pick at its rotation turn, so the real
///    unwritten prefix sequence matches no dense-rotation hypothesis.
/// 2. Cursor bias: a strand with only unwritten prefix picks in the coverage-spanning
///    occurrence and takes in a later non-contiguous group of the same rule gets its
///    reconstruction base from the persisted cursor — which marks the last take of the
///    whole plan, not of this occurrence — landing the hypotheses on wrong ring positions.
/// </summary>
public sealed class ProgramScheduleServiceTests_Issue320
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    // Two-strand genre rotation (two shows à 45 min), daily 20:00, 4 h slot. The FSK-18
    // show can never air before 23:00 local — its ordered head is blocked at every turn
    // of the 20:00 occurrence, so the strand defers once and produces nothing for the
    // rest of the occurrence (the materialization's own done-rule).
    private static async Task<(ProgramScheduleTestDb Fixture, ProgramRule Rule)> SeedBlockedStrandRuleAsync(
        string dbName, CancellationToken ct)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Mix", 1);
        var (day, _, _) = await fixture.CreateShowAsync("Tagesserie", 10, 1, TimeSpan.FromMinutes(45));
        var (night, _, _) = await fixture.CreateShowAsync("Nachtserie", 10, 1, TimeSpan.FromMinutes(45), ageRatingMinAge: 18);
        await fixture.LinkShowGenreAsync(day.Id, genre.Id);
        await fixture.LinkShowGenreAsync(night.Id, genre.Id);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Genre", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            slotDuration: TimeSpan.FromHours(4), genreIds: [genre.Id]);
        return (fixture, rule);
    }

    // Deferral gap: coverage began mid-occurrence, so the written tail holds only the
    // free strand's picks; the blocked strand's unwritten prefix contains a deferral
    // event that no dense-rotation hypothesis can represent — the check displaced the
    // unchanged airing (HeadPickInconsistent) and re-materialized the identical tail.
    [Fact]
    public async Task Reconcile_UnchangedRules_DeferredStrandInPrefix_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var messages = new ConcurrentQueue<string>();
        var (fixture, _) = await SeedBlockedStrandRuleAsync("i320-deferral", ct);
        using var _ = fixture;
        var service = fixture.CreateService(logger: new ListLogger<ProgramScheduleService>(messages));

        // 21:00 lies inside the 20:00 occurrence: the first take (20:00-20:45) is taken
        // but never written — together with the blocked strand's deferral at its first
        // turn it forms the unwritten prefix.
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 21, 0)), ct);
        var before = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.NotEmpty(before);

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 21, 15)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct, "Lauf 1");

        var summary2 = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 21, 30)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "Lauf 2");

        var after = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.All(before, b => Assert.Contains(after, a => a == b));
        Assert.DoesNotContain(messages, m => m.Contains("HeadPickInconsistent"));
    }

    // Two-strand genre rotation with unequal episode runtimes: each show's first episode
    // deviates from the rest, so the ring position of the unwritten prefix take decides
    // the reconstructed real start. Daily 20:00, 2:15 h slot → three picks per
    // occurrence, the last one (21:30/21:45 → 22:15) is the only written slot once
    // coverage begins at 22:00.
    private static async Task<(ProgramScheduleTestDb Fixture, ProgramRule Rule)> SeedUnequalLengthRuleAsync(
        string dbName, CancellationToken ct)
    {
        var fixture = await ProgramScheduleTestDb.CreateAsync(dbName, ct);
        var channel = await fixture.GetChannelAsync(ct);
        var genre = await fixture.CreateGenreAsync("Mix", 1);
        var (alpha, _, alphaEps) = await fixture.CreateShowAsync("Alpha", 10, 1, TimeSpan.FromMinutes(45));
        var (beta, _, betaEps) = await fixture.CreateShowAsync("Beta", 10, 1, TimeSpan.FromMinutes(45));
        await fixture.LinkShowGenreAsync(alpha.Id, genre.Id);
        await fixture.LinkShowGenreAsync(beta.Id, genre.Id);

        // Intra-strand runtime variance is what makes the biased cursor position fatal:
        // the reconstruction walking back from the plan-end cursor takes a 45-min item
        // where the real prefix take was 30/60 min and lands on a wrong real start.
        await SetEpisodeDurationAsync(fixture, alphaEps[0].Id, TimeSpan.FromMinutes(30), ct);
        await SetEpisodeDurationAsync(fixture, betaEps[0].Id, TimeSpan.FromMinutes(60), ct);

        var rule = await fixture.CreateRuleAsync(channel.Id, "Genre", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Genre, genre.Id,
            slotDuration: TimeSpan.FromMinutes(135), genreIds: [genre.Id]);
        return (fixture, rule);
    }

    private static async Task SetEpisodeDurationAsync(ProgramScheduleTestDb fixture, long episodeId, TimeSpan duration, CancellationToken ct)
    {
        var item = await fixture.Db.TVShowEpisodeMediaItems
            .Where(l => l.TVShowEpisodeId == episodeId)
            .Select(l => l.MediaItem)
            .SingleAsync(ct);
        item.Duration = duration;
        item.DurationProbedAt = DateTime.UtcNow;
        await fixture.SaveDiagAsync();
    }

    // Cursor bias: the strand whose day-1 takes all ended before the coverage start has
    // no written contribution to the tail group — but it took again in the later
    // (non-contiguous) day-2/day-3 groups, so the persisted cursor sits past those later
    // takes. The reconstruction used to start from that plan-end position, took the
    // wrong (differently long) item for the unwritten prefix take and failed the
    // occurrence anchor — displacing the unchanged airing every run.
    [Fact]
    public async Task Reconcile_UnchangedRules_UnwrittenPrefixTakeWithLaterGroupTake_StaysScheduled()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, _) = await SeedUnequalLengthRuleAsync("i320-cursorbias", ct);
        using var _ = fixture;
        var service = fixture.CreateService();

        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 22, 0)), ct);
        var before = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.NotEmpty(before);

        var summary = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 22, 0)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary, ct, "Lauf 1");

        var summary2 = await service.ReconcileAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 22, 5)), ct);
        await ProgramScheduleTestDb.AssertNoDisplacedAsync(fixture, summary2, ct, "Lauf 2");

        var after = (await fixture.SlotsAsync(ct))
            .Where(s => s.Status == ProgramSlotStatus.Scheduled)
            .Select(s => (s.Start, s.End, s.MediaItemId, s.TVShowEpisodeId, s.MovieId))
            .ToList();
        Assert.All(before, b => Assert.Contains(after, a => a == b));
    }
}
