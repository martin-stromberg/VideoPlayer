using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for issue #351: a freshly created or re-activated rule materializes inside the
/// already-planned horizon — it wins window conflicts against kept slots of equal priority
/// (freshness stages: no cursor rows at all, or <see cref="ProgramRule.UpdatedAt"/> younger
/// than the competing slot's <see cref="ProgramSlot.CreatedAt"/>); the displaced airing
/// shifts behind the new block instead of being dropped, at most its tail title defers to
/// the next occurrence, and a dropped <c>collection:{id}</c> film cascades over the strand
/// cursor into all later continuation blocks.
/// </summary>
public sealed class ProgramScheduleServiceTests_NewRules
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    private static async Task<List<ProgramSlot>> DisplacedAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced).ToList();

    private static string Dump(IEnumerable<ProgramSlot> slots)
        => string.Join(" | ", slots.Select(s =>
            $"{(s.Status == ProgramSlotStatus.Displaced ? "X" : "S")} {PrimetimeSeed.ToLocal(s.Start):dd.MM HH:mm}-{PrimetimeSeed.ToLocal(s.End):HH:mm} rule={s.ProgramRuleId} movie={s.MovieId} ep={s.TVShowEpisodeId}"));

    // Acceptance scenario 1 (happy path): a new early-evening rule (daily 17:00, 4×45 min —
    // its block ends 20:00, entirely before the 20:15 primetime) lands on every already
    // planned day, not just at the horizon edge; the primetime plan is untouched and —
    // free window — nothing is displaced.
    [Fact]
    public async Task Reconcile_NewRuleFreeWindow_MaterializesInAlreadyPlannedDays()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-freewindow", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, _) = await fixture.CreateShowAsync("Primetime-Serie", 20, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var primeBefore = (await ScheduledAsync(fixture, ct)).Where(s => s.ProgramRuleId == prime.Id).ToList();
        Assert.Equal(14, primeBefore.Count);

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 4);
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var earlySlots = scheduled.Where(s => s.ProgramRuleId == early.Id).OrderBy(s => s.Start).ToList();
        // Seven already-planned days carry the early block — 17:00 to 20:00, contiguous.
        Assert.Equal(28, earlySlots.Count);
        for (var d = 0; d < 7; d++)
        {
            var day = earlySlots.Skip(d * 4).Take(4).ToList();
            Assert.Equal(LocalUtc(2026, 11, 2 + d, 17, 0), day[0].Start);
            for (var i = 1; i < 4; i++)
                Assert.Equal(day[i - 1].End, day[i].Start);
            Assert.Equal(LocalUtc(2026, 11, 2 + d, 20, 0), day[^1].End);
        }
        // The existing primetime plan is byte-identical (same rows, times, content).
        var primeAfter = scheduled.Where(s => s.ProgramRuleId == prime.Id).ToList();
        PrimetimeSeed.AssertPreserved(primeBefore, primeAfter, "Primetime");
        Assert.Equal(0, summary.SlotsDisplaced);
    }

    // The last episode of the fresh early block legally overshoots the soft 20:15 boundary —
    // the kept primetime block loses the overlapping slot, is displaced completely and
    // re-materializes shifted behind the new block: same episodes, real start = end of the
    // fresh block; follow-up days shift the same way.
    [Fact]
    public async Task Reconcile_NewRuleTailOverlap_ShiftsPrimetimeBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-tailoverlap", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, primeEps) = await fixture.CreateShowAsync("Primetime-Serie", 20, duration: TimeSpan.FromMinutes(60));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // Fresh rule: daily 17:00, four 60-minute episodes — the fourth (20:00–21:00) cuts
        // into the primetime block's first slot (20:15–21:15).
        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 4);
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var scheduled = await ScheduledAsync(fixture, ct);
        var earlyDay1 = scheduled.Where(s => s.ProgramRuleId == early.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(4, earlyDay1.Count);
        Assert.Equal(LocalUtc(2026, 11, 2, 21, 0), earlyDay1[^1].End);

        // The primetime block kept its content and shifted behind the new block's real end.
        var primeDay1 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(2, primeDay1.Count);
        Assert.Equal(earlyDay1[^1].End, primeDay1[0].Start);
        Assert.Equal(LocalUtc(2026, 11, 2, 21, 0), primeDay1[0].Start);
        Assert.Equal([primeEps[0].Id, primeEps[1].Id], primeDay1.Select(s => s.TVShowEpisodeId).ToList());

        // Every later day carries the same shifted pairing — episodes continue in air order.
        var primeEpisodes = scheduled.Where(s => s.ProgramRuleId == prime.Id)
            .OrderBy(s => s.Start).Select(s => s.TVShowEpisodeId!.Value).ToList();
        var expected = Enumerable.Range(0, primeEpisodes.Count).Select(i => primeEps[i].Id).ToList();
        Assert.Equal(expected, primeEpisodes);
        for (var d = 1; d < 7; d++)
        {
            var day = scheduled.Where(s => s.ProgramRuleId == prime.Id
                && s.Start >= LocalUtc(2026, 11, 2 + d, 12, 0) && s.Start < LocalUtc(2026, 11, 3 + d, 12, 0)).ToList();
            Assert.Equal(LocalUtc(2026, 11, 2 + d, 21, 0), day[0].Start);
        }
    }

    // The shift is so large that the last title of the displaced block no longer fits before
    // its hard boundary (a higher-priority 23:30 occurrence): it defers and airs first on the
    // follow-up day — the air order stays gapless and duplicate-free.
    [Fact]
    public async Task Reconcile_NewRuleOverlap_ShiftedBlockTailDefers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-taildefers", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, primeEps) = await fixture.CreateShowAsync("Primetime-Serie", 30, duration: TimeSpan.FromMinutes(60));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));
        var late = await fixture.CreateMovieAsync("Spätfilm", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var lateRule = await fixture.CreateRuleAsync(channel.Id, "Spätprogramm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 30), ProgramRuleMaterialType.Movie, late.Id,
            priority: 10, episodeCount: 1);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 3);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        // The unshifted day block still fits three episodes before the hard 23:30 bound.
        var primeBefore = (await ScheduledAsync(fixture, ct)).Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).ToList();
        Assert.Equal(3, primeBefore.Count);

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 4);
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        // Shifted to 21:00, the shifted block keeps two episodes (21:00–23:00); the third
        // cannot complete before the hard 23:30 bound and defers.
        var primeDay1 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(2, primeDay1.Count);
        Assert.Equal([primeEps[0].Id, primeEps[1].Id], primeDay1.Select(s => s.TVShowEpisodeId).ToList());
        Assert.Equal(LocalUtc(2026, 11, 2, 23, 0), primeDay1[^1].End);

        // The deferred third episode heads the next day's (again shifted) block.
        var primeDay2 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 3, 12, 0) && s.Start < LocalUtc(2026, 11, 4, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(primeEps[2].Id, primeDay2[0].TVShowEpisodeId);
        var allPrime = scheduled.Where(s => s.ProgramRuleId == prime.Id)
            .OrderBy(s => s.Start).Select(s => s.TVShowEpisodeId!.Value).ToList();
        Assert.Equal(Enumerable.Range(0, allPrime.Count).Select(i => primeEps[i].Id).ToList(), allPrime);
    }

    // A displaced collection film cascades through the ordered "collection:{id}" strand:
    // every later continuation block of the rule is displaced and re-materialized carrying
    // the respectively earlier collection films — no film airs before its predecessor.
    [Fact]
    public async Task Reconcile_DroppedCollectionFilm_SequelsDisplaced_PredecessorsRefill()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-collection", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await fixture.CreateCollectionAsync("Reihe", 16, TimeSpan.FromMinutes(60));
        var movies = await fixture.Db.Movies.Where(m => m.MovieCollectionId == collection.Id)
            .OrderBy(m => m.ReleaseDate).ToListAsync(ct);
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));
        var late = await fixture.CreateMovieAsync("Spätfilm", duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        await fixture.CreateRuleAsync(channel.Id, "Spätprogramm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(23, 30), ProgramRuleMaterialType.Movie, late.Id,
            priority: 10, episodeCount: 1);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Sammlungs-Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.MovieCollection, collection.Id,
            slotDuration: TimeSpan.FromMinutes(180));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var before = await ScheduledAsync(fixture, ct);
        // Unshifted, three films fit between 20:15 and the hard 23:30 bound.
        Assert.Equal(3, before.Count(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)));

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 4);
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        var displaced = await DisplacedAsync(fixture, ct);
        // Every originally planned block of the collection rule — including the later
        // continuation days — was displaced by the cascade.
        Assert.True(displaced.Count(s => s.ProgramRuleId == prime.Id) >= 3 * 7,
            Dump(displaced));

        var scheduled = await ScheduledAsync(fixture, ct);
        var primeDay1 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        // Shifted to 21:00 only films 1+2 still complete before 23:30 — film 3 defers and
        // heads the next day's block, shifting every later block one film earlier.
        Assert.Equal([movies[0].Id, movies[1].Id], primeDay1.Select(s => s.MovieId).ToList());
        var primeDay2 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 3, 12, 0) && s.Start < LocalUtc(2026, 11, 4, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(movies[2].Id, primeDay2[0].MovieId);

        // Ordered strand over the whole horizon: the aired film ids are the release-order
        // sequence — no collection film airs before its predecessor.
        var sequence = scheduled.Where(s => s.ProgramRuleId == prime.Id)
            .OrderBy(s => s.Start).Select(s => s.MovieId!.Value).ToList();
        Assert.Equal(movies.Take(sequence.Count).Select(m => m.Id).ToList(), sequence);
    }

    // The new occurrence start lands strictly inside a kept slot: the spanned slot airs to
    // its end and stays Scheduled together with the prefix before it — displacement is
    // slot-exact and only strikes from the following slot on.
    [Fact]
    public async Task Reconcile_NewRuleLaterStart_PreservesAiringPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-prefix", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, primeEps) = await fixture.CreateShowAsync("Primetime-Serie", 30, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 3);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var keptHead = (await ScheduledAsync(fixture, ct)).Single(s => s.ProgramRuleId == prime.Id
            && s.Start == LocalUtc(2026, 11, 2, 20, 15));

        // 20:45 lands strictly inside the block's first slot (20:15–21:00).
        var late = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 45), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 1);
        await service.ReconcileAsync(now.AddMinutes(30), ct);

        var slots = await fixture.SlotsAsync(ct);
        var day1 = slots.Where(s => s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).ToList();
        // The spanned slot runs to its end — prefix preserved with identity and content.
        var prefix = day1.Single(s => s.ProgramRuleId == prime.Id && s.Status == ProgramSlotStatus.Scheduled);
        Assert.Equal(keptHead.Id, prefix.Id);
        Assert.Equal(LocalUtc(2026, 11, 2, 20, 15), prefix.Start);
        Assert.Equal(LocalUtc(2026, 11, 2, 21, 0), prefix.End);
        Assert.Equal(primeEps[0].Id, prefix.TVShowEpisodeId);
        // The conflict struck from the following slot on — the suffix is displaced.
        var suffix = day1.Where(s => s.ProgramRuleId == prime.Id && s.Status == ProgramSlotStatus.Displaced).ToList();
        Assert.Equal(2, suffix.Count);
        Assert.All(suffix, s => Assert.True(s.Start >= LocalUtc(2026, 11, 2, 21, 0)));
        // The new rule's slot starts at the spanned slot's end.
        var fresh = day1.Where(s => s.ProgramRuleId == late.Id && s.Status == ProgramSlotStatus.Scheduled).ToList();
        var freshSlot = Assert.Single(fresh);
        Assert.Equal(prefix.End, freshSlot.Start);
    }

    // After the displacement + shift the plan is stable: a second reconcile run is a full
    // no-op and leaves no self-displaced twins behind.
    [Fact]
    public async Task Reconcile_NewRuleIsIdempotent_SecondRunNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-idempotent", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, _) = await fixture.CreateShowAsync("Primetime-Serie", 20, duration: TimeSpan.FromMinutes(60));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 4);
        var first = await service.ReconcileAsync(now.AddMinutes(30), ct);
        Assert.True(first.SlotsDisplaced > 0);

        var second = await service.ReconcileAsync(now.AddMinutes(35), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "Zweitlauf");
        await ProgramScheduleTestDb.AssertNoSelfDisplacedAsync(fixture, ct, "Zweitlauf");
    }

    // Re-activation freshness (stage b): the re-activated rule is YOUNGER than the equal-
    // priority window owner — seniority alone could never cover this case. Run 1 with a
    // stale UpdatedAt still loses per seniority; after the re-activation bumps UpdatedAt
    // past the kept slots' CreatedAt the same occurrence wins and materializes on every
    // already-planned day; the owner shifts behind it; a third run is a no-op.
    [Fact]
    public async Task Reconcile_ReactivatedRule_MaterializesInAlreadyPlannedDays()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-reactivated", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, primeEps) = await fixture.CreateShowAsync("Primetime-Serie", 30, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);
        // The later rule (higher Id) materializes first in a disjoint window — cursor rows
        // exist before the contested run, so freshness stage (a) does not apply and the
        // seniority loss in run 1 is actually exercised.
        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 2, createdUtc: now.UtcDateTime.AddHours(-2), updatedUtc: now.UtcDateTime.AddHours(-2));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // The rule moves into the contested window — its UpdatedAt intentionally stays the
        // old value (simulating a long-past edit): stale, established rules lose. At 19:45
        // the first episode overshoots the soft 20:15 bound into the kept primetime slot —
        // a real head-on conflict, decided by seniority (higher Id loses).
        early.TimeOfDay = new TimeOnly(19, 45);
        await fixture.SaveDiagAsync();
        var first = await service.ReconcileAsync(now.AddMinutes(30), ct);
        var scheduled = await ScheduledAsync(fixture, ct);
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == early.Id);
        Assert.Equal(2, scheduled.Count(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)));

        // De- and re-activate: the helper bumps UpdatedAt past the kept slots' CreatedAt —
        // the production path of SetRuleActiveAsync.
        var maxSlotCreated = (await fixture.SlotsAsync(ct)).Max(s => s.CreatedAt);
        await fixture.SetRuleActiveAsync(early, false);
        await fixture.SetRuleActiveAsync(early, true, maxSlotCreated.AddMinutes(5));

        var second = await service.ReconcileAsync(now.AddMinutes(40), ct);
        Assert.True(second.SlotsDisplaced > 0);
        scheduled = await ScheduledAsync(fixture, ct);
        // The re-activated rule materializes on every already-planned day at 19:45.
        for (var d = 0; d < 7; d++)
        {
            var day = scheduled.Where(s => s.ProgramRuleId == early.Id
                && s.Start >= LocalUtc(2026, 11, 2 + d, 19, 45) && s.Start < LocalUtc(2026, 11, 3 + d, 12, 0))
                .OrderBy(s => s.Start).ToList();
            Assert.True(day.Count >= 1, $"Tag {d}: keine frische Belegung — {Dump(scheduled)}");
            Assert.Equal(LocalUtc(2026, 11, 2 + d, 19, 45), day[0].Start);
        }
        // The primetime shifted behind the contested overshooting episode (19:45–20:30 —
        // the second pick defers at the soft 20:15 bound), content preserved.
        var primeDay1 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0))
            .OrderBy(s => s.Start).ToList();
        Assert.Equal(2, primeDay1.Count);
        Assert.True(primeDay1[0].Start == LocalUtc(2026, 11, 2, 20, 30), Dump(scheduled.Where(s => s.Start < LocalUtc(2026, 11, 4, 0, 0))));
        Assert.Equal([primeEps[0].Id, primeEps[1].Id], primeDay1.Select(s => s.TVShowEpisodeId).ToList());

        var third = await service.ReconcileAsync(now.AddMinutes(45), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, third, ct, "Drittlauf");
    }

    // Two equally fresh, equal-priority rules on the same window: run 1 is decided by the
    // iteration order (same start → lower id), run 2 by the still-cursorless loser's
    // freshness win, from run 3 on both are established and seniority decides — the older
    // rule wins for good. Two winner changes at most, then a stable no-op — no ping-pong.
    [Fact]
    public async Task Reconcile_FreshVsFresh_SameWindow_ConvergesDeterministic()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-freshfresh", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (showA, _, _) = await fixture.CreateShowAsync("Serie A", 30, duration: TimeSpan.FromMinutes(45));
        var (showB, _, _) = await fixture.CreateShowAsync("Serie B", 30, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var ruleA = await fixture.CreateRuleAsync(channel.Id, "Regel A", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, showA.Id,
            episodeCount: 2);
        var ruleB = await fixture.CreateRuleAsync(channel.Id, "Regel B", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, showB.Id,
            episodeCount: 2);
        Assert.True(ruleA.Id < ruleB.Id);

        var service = fixture.CreateService();
        async Task<long> WinnerAsync()
        {
            var scheduled = await ScheduledAsync(fixture, ct);
            var day1 = scheduled.Where(s => s.Start == LocalUtc(2026, 11, 2, 20, 15)).ToList();
            return Assert.Single(day1).ProgramRuleId!.Value;
        }

        await service.ReconcileAsync(now.AddMinutes(5), ct);
        Assert.Equal(ruleA.Id, await WinnerAsync());
        await service.ReconcileAsync(now.AddMinutes(10), ct);
        Assert.Equal(ruleB.Id, await WinnerAsync());
        await service.ReconcileAsync(now.AddMinutes(15), ct);
        Assert.Equal(ruleA.Id, await WinnerAsync());

        // Converged: the established seniority result holds, the next run is a full no-op.
        var fourth = await service.ReconcileAsync(now.AddMinutes(20), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, fourth, ct, "Vierter Lauf");
        Assert.Equal(ruleA.Id, await WinnerAsync());
        await ProgramScheduleTestDb.AssertNoSelfDisplacedAsync(fixture, ct, "Konvergenz");
    }

    // A window held only by filler slots hands itself over: the fillers are displaced
    // individually (no rule suffix) and the fresh block lands Scheduled — gap stays filled.
    [Fact]
    public async Task Reconcile_NewRuleOverlap_FillerSlotDisplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, _) = await fixture.CreateShowAsync("Primetime-Serie", 20, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(45));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(15));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);
        var fillerRule = await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id,
            episodeCount: 1);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 2);
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var slots = await fixture.SlotsAsync(ct);
        // The displaced share of the 17:00 window are filler rows — displaced individually.
        var displacedFillers = slots.Where(s => s.Status == ProgramSlotStatus.Displaced
            && s.RuleType == ProgramRuleType.Filler
            && s.Start >= LocalUtc(2026, 11, 2, 17, 0) && s.Start < LocalUtc(2026, 11, 2, 18, 30)).ToList();
        Assert.NotEmpty(displacedFillers);
        var earlySlots = slots.Where(s => s.ProgramRuleId == early.Id && s.Status == ProgramSlotStatus.Scheduled
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(2, earlySlots.Count);
        Assert.Equal(LocalUtc(2026, 11, 2, 17, 0), earlySlots[0].Start);
        // The primetime block is untouched and the plan remains gapless.
        ProgramScheduleTestDb.AssertGapless(
            slots.Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList(),
            now.AddMinutes(30).UtcDateTime, now.AddMinutes(30).UtcDateTime.AddDays(7));
    }

    // Priority boundary: a new rule with LOWER priority never shifts a kept higher-priority
    // block — it materializes its free prefix (partial materialization, no atomic drop) and
    // only the overshooting remainder defers to the next occurrence.
    [Fact]
    public async Task Reconcile_NewRuleLowerPriority_DoesNotShiftHigherPriorityAiring()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-lowerprio", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, _) = await fixture.CreateShowAsync("Primetime-Serie", 20, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            priority: 10, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var primeBefore = (await ScheduledAsync(fixture, ct)).Where(s => s.ProgramRuleId == prime.Id).ToList();

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            priority: 0, episodeCount: 4);
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        // The higher-priority block is completely untouched.
        Assert.Equal(0, summary.SlotsDisplaced);
        var scheduled = await ScheduledAsync(fixture, ct);
        PrimetimeSeed.AssertPreserved(primeBefore, scheduled.Where(s => s.ProgramRuleId == prime.Id).ToList(), "Primetime");
        // The lower-priority occurrence still lands its free prefix: three episodes fit
        // completely before the hard 20:15 bound; the fourth cannot complete and defers.
        var earlyDay1 = scheduled.Where(s => s.ProgramRuleId == early.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(3, earlyDay1.Count);
        Assert.Equal(LocalUtc(2026, 11, 2, 17, 0), earlyDay1[0].Start);
        Assert.Equal(LocalUtc(2026, 11, 2, 20, 0), earlyDay1[^1].End);
    }

    // Two established equal-priority rules (both with cursor rows, both UpdatedAt older than
    // the competing materialization): the seniority decision is deterministic — the older
    // rule keeps the contested window and the next run is a full no-op (no oscillation).
    [Fact]
    public async Task Reconcile_EqualPriorityEstablishedRules_DeterministicNoOscillation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-established", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, _) = await fixture.CreateShowAsync("Primetime-Serie", 30, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        var staleStamp = now.UtcDateTime.AddDays(-2);
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2, createdUtc: staleStamp, updatedUtc: staleStamp);
        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 2, createdUtc: staleStamp, updatedUtc: staleStamp);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // The established rule's occurrence moves into the contested window (19:45 — the
        // first episode overshoots into the kept 20:15 slot) — without a freshness event
        // (no re-activation, stale UpdatedAt) seniority decides against it.
        early.TimeOfDay = new TimeOnly(19, 45);
        await fixture.SaveDiagAsync();
        var first = await service.ReconcileAsync(now.AddMinutes(30), ct);
        var scheduled = await ScheduledAsync(fixture, ct);
        var primeDay1 = scheduled.Where(s => s.ProgramRuleId == prime.Id
            && s.Start >= LocalUtc(2026, 11, 2, 12, 0) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).ToList();
        Assert.Equal(2, primeDay1.Count);
        Assert.Equal(LocalUtc(2026, 11, 2, 20, 15), primeDay1[0].Start);
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == early.Id);

        // Deterministic: the follow-up run keeps the seniority outcome — no ping-pong.
        var second = await service.ReconcileAsync(now.AddMinutes(35), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "Folgelauf");
        await ProgramScheduleTestDb.AssertNoSelfDisplacedAsync(fixture, ct, "Folgelauf");
    }

    // A slot that is actually running at the reconcile instant is protected (Issue #315):
    // it airs to its end and the fresh occurrence materializes behind it — never a cut.
    [Fact]
    public async Task Reconcile_NewRuleOverlap_RunningSlotStaysProtected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-protected", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (primeShow, _, primeEps) = await fixture.CreateShowAsync("Primetime-Serie", 30, duration: TimeSpan.FromMinutes(45));
        var (earlyShow, _, _) = await fixture.CreateShowAsync("Vorabend-Serie", 40, duration: TimeSpan.FromMinutes(60));

        var planStart = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, primeShow.Id,
            episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(planStart, ct);
        var running = (await ScheduledAsync(fixture, ct)).Single(s => s.ProgramRuleId == prime.Id
            && s.Start == LocalUtc(2026, 11, 2, 20, 15));

        // Fresh rule at 20:30 — strictly inside the running slot (20:15–21:00), reconcile
        // at 20:45 while the slot is still on air.
        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 30), ProgramRuleMaterialType.TVShow, earlyShow.Id,
            episodeCount: 2);
        var now = new DateTimeOffset(LocalUtc(2026, 11, 2, 20, 45));
        await service.ReconcileAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var runningAfter = slots.Single(s => s.Id == running.Id);
        Assert.Equal(ProgramSlotStatus.Scheduled, runningAfter.Status);
        Assert.Equal(LocalUtc(2026, 11, 2, 21, 0), runningAfter.End);
        Assert.Equal(primeEps[0].Id, runningAfter.TVShowEpisodeId);
        // The fresh block starts exactly at the running slot's end — shifted, never cut.
        var freshDay1 = slots.Where(s => s.ProgramRuleId == early.Id && s.Status == ProgramSlotStatus.Scheduled
            && s.Start >= LocalUtc(2026, 11, 2, 20, 45) && s.Start < LocalUtc(2026, 11, 3, 12, 0)).OrderBy(s => s.Start).ToList();
        Assert.Equal(2, freshDay1.Count);
        Assert.Equal(runningAfter.End, freshDay1[0].Start);
    }

    // Full practice scenario on a realistic library (multi-season shows, collections,
    // standalone movies): primetime genre rotation exists, a new early-evening genre
    // rotation is added; several worker passes run with progressing time — the plan stays
    // gapless, every ordered strand keeps its air order and the plan converges.
    [Fact]
    public async Task Reconcile_NewRuleRealisticLibrary_FullScenario()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("newrule-realistic", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (shows, collections, _, genres) = await fixture.SeedRealisticLibraryAsync();
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(15));

        var day1 = new DateTimeOffset(LocalUtc(2026, 11, 2, 12, 0));
        await PrimetimeSeed.SetHorizonDaysAsync(fixture, 7, ct);
        var prime = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.Genre, 0,
            slotDuration: TimeSpan.FromMinutes(225), genreIds: [genres[1].Id], mediaSourceIds: [1]);
        var fillerRule = await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id,
            episodeCount: 1);

        await PrimetimeSeed.StepAsync(fixture, day1, ct);

        var early = await fixture.CreateRuleAsync(channel.Id, "Vorabend", ProgramRuleType.GenreRotation,
            ProgramRuleRecurrence.Daily, new TimeOnly(17, 0), ProgramRuleMaterialType.Genre, 0,
            slotDuration: TimeSpan.FromMinutes(195), genreIds: [genres[0].Id], mediaSourceIds: [1]);

        // Several worker passes with progressing time: the new rule materializes on the
        // already-planned days; no overlaps and no gap larger than the filler granularity
        // (sub-15-minute remainders legitimately go to RemainingGapSeconds).
        var service = fixture.CreateService();
        var fillerSpan = TimeSpan.FromMinutes(15);
        for (var day = 0; day < 3; day++)
        {
            var passNow = day1.AddDays(day).AddHours(1);
            await service.ExecutePassAsync(passNow, ct);
            var scheduled = (await ScheduledAsync(fixture, ct)).OrderBy(s => s.Start).ToList();
            var covered = scheduled.Where(s => s.End > passNow.UtcDateTime && s.Start < day1.UtcDateTime.AddDays(7)).ToList();
            for (var i = 1; i < covered.Count; i++)
            {
                Assert.True(covered[i].Start >= covered[i - 1].End,
                    $"Überlappung: {covered[i - 1].Start:o}–{covered[i - 1].End:o} vs {covered[i].Start:o}–{covered[i].End:o}");
                Assert.True(covered[i].Start - covered[i - 1].End <= fillerSpan,
                    $"Lücke > 15min: {covered[i - 1].End:o} bis {covered[i].Start:o}");
            }
            var earlySlots = scheduled.Where(s => s.ProgramRuleId == early.Id && PrimetimeSeed.ToLocal(s.Start).TimeOfDay >= new TimeOnly(17, 0).ToTimeSpan() && PrimetimeSeed.ToLocal(s.Start).TimeOfDay < new TimeOnly(20, 15).ToTimeSpan()).ToList();
            Assert.NotEmpty(earlySlots);
        }

        var scheduledAll = await ScheduledAsync(fixture, ct);
        // Ordered strands: inside every collection strand the aired films follow release
        // order — the k-th distinct film seen is the k-th film of the collection.
        foreach (var coll in collections)
        {
            var order = (await fixture.Db.Movies.Where(m => m.MovieCollectionId == coll.Id)
                .OrderBy(m => m.ReleaseDate).Select(m => m.Id).ToListAsync(ct));
            var seen = new List<long>();
            foreach (var slot in scheduledAll.Where(s => s.MovieId is not null).OrderBy(s => s.Start))
                if (order.Contains(slot.MovieId!.Value) && !seen.Contains(slot.MovieId.Value))
                    seen.Add(slot.MovieId.Value);
            Assert.Equal(order.Take(seen.Count).ToList(), seen);
        }
        // Episode strands: within each show the k-th distinct episode aired is the k-th in
        // air order (season, then number).
        foreach (var (show, _, episodes) in shows)
        {
            var order = episodes.Select(e => e.Id).ToList();
            var seen = new List<long>();
            foreach (var slot in scheduledAll.Where(s => s.TVShowEpisodeId is not null).OrderBy(s => s.Start))
                if (order.Contains(slot.TVShowEpisodeId!.Value) && !seen.Contains(slot.TVShowEpisodeId.Value))
                    seen.Add(slot.TVShowEpisodeId.Value);
            Assert.Equal(order.Take(seen.Count).ToList(), seen);
        }
        // No two scheduled slots overlap in time.
        var ordered = scheduledAll.OrderBy(s => s.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].Start >= ordered[i - 1].End,
                $"Überlappung: {ordered[i - 1].Start:o}–{ordered[i - 1].End:o} vs {ordered[i].Start:o}–{ordered[i].End:o}");
    }
}
