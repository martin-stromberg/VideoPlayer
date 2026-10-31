using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for issue #314: a higher-priority (special) rule takes over the time window of
/// lower-priority rules — their occurrence starts no longer bound its airing; it fills its
/// own window through them and beyond. The displaced occurrence shifts behind the real
/// special airing or defers to its next occurrence; when a planned regular block is
/// displaced by a later rule change, its episodes shift instead of falling out — follow-up
/// days continue the air order gap- and duplicate-free.
/// </summary>
public sealed class ProgramScheduleServiceTests_PriorityTakeover
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    private static async Task<MovieCollection> SeedCollectionAsync(
        ProgramScheduleTestDb fixture, string name, int movies, int minutes)
    {
        var collection = new MovieCollection { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        for (var i = 1; i <= movies; i++)
            await fixture.CreateMovieAsync($"{name} {i}", 1, TimeSpan.FromMinutes(minutes), collection.Id, releaseDate: new DateTime(2000, 1, i));
        return collection;
    }

    // The live case of the issue: the high-priority special rule (daily 14:00, 5 h) must
    // not stop at the lower-priority regular window (daily 16:00) — it fills its own slot
    // through it; the regular occurrence shifts behind the special airing's real end.
    [Fact]
    public async Task EnsureCoverage_SpecialRule_TakesOverLowerPriorityWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("takeover-extend", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await SeedCollectionAsync(fixture, "Special", 6, 60);
        var prime = await fixture.CreateMovieAsync("Primetime-Film", duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var special = await fixture.CreateRuleAsync(channel.Id, "Sonderprogramm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(14, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            priority: 100, slotDuration: TimeSpan.FromHours(5));
        var regular = await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(16, 0), ProgramRuleMaterialType.Movie, prime.Id,
            priority: 0, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var specialDay1 = scheduled.Where(s => s.ProgramRuleId == special.Id
            && s.Start >= LocalUtc(2026, 10, 24, 14, 0) && s.Start < LocalUtc(2026, 10, 25, 14, 0)).ToList();
        // The special rule fills its whole slot: five hours of material, contiguous from
        // 14:00 — the former lower-priority bound at 16:00 no longer stops it.
        Assert.Equal(5, specialDay1.Count);
        Assert.Equal(LocalUtc(2026, 10, 24, 14, 0), specialDay1[0].Start);
        for (var i = 1; i < specialDay1.Count; i++)
            Assert.Equal(specialDay1[i - 1].End, specialDay1[i].Start);
        Assert.Equal(LocalUtc(2026, 10, 24, 19, 0), specialDay1[^1].End);

        // The displaced regular occurrence shifts behind the special airing's real end —
        // it is not dropped: the movie still airs, complete.
        var regularDay1 = scheduled.Where(s => s.ProgramRuleId == regular.Id
            && s.Start >= LocalUtc(2026, 10, 24, 12, 0) && s.Start < LocalUtc(2026, 10, 25, 12, 0)).ToList();
        var slot = Assert.Single(regularDay1);
        Assert.Equal(specialDay1[^1].End, slot.Start);
        Assert.Equal(LocalUtc(2026, 10, 24, 19, 0), slot.Start);
        Assert.Equal(TimeSpan.FromMinutes(60), slot.End - slot.Start);
    }

    // When a planned regular block loses its window to a newly added special rule, the
    // discarded episodes are re-aired — shifted, never skipped: the next written episode
    // after the kept prefix is exactly the displaced block's head.
    [Fact]
    public async Task Reconcile_SpecialRuleDisplacesPlannedBlock_EpisodeShiftedNotSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("takeover-shift", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 10, duration: TimeSpan.FromMinutes(45));
        var collection = await SeedCollectionAsync(fixture, "Special", 6, 60);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var regular = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, show.Id,
            priority: 0, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var planned = await ScheduledAsync(fixture, ct);
        var day2Airing = planned.Where(s => s.ProgramRuleId == regular.Id
            && s.Start >= LocalUtc(2026, 10, 25, 12, 0) && s.Start < LocalUtc(2026, 10, 26, 12, 0)).ToList();
        Assert.Equal(2, day2Airing.Count);
        var displacedHead = day2Airing[0].TVShowEpisodeId!.Value;

        // The special rule arrives after the plan exists: single day, 19:00 + 6 h covers
        // the whole regular window of day 2.
        var special = await fixture.CreateRuleAsync(channel.Id, "Sonderprogramm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Once, new TimeOnly(19, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            priority: 100, slotDuration: TimeSpan.FromHours(6), singleDate: new DateOnly(2026, 10, 25));
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var scheduled = await ScheduledAsync(fixture, ct);
        // The special rule fills its whole six-hour slot through the former regular window —
        // no dead rest after the displaced 20:15 boundary.
        var specialSlots = scheduled.Where(s => s.ProgramRuleId == special.Id).OrderBy(s => s.Start).ToList();
        Assert.Equal(6, specialSlots.Count);
        Assert.Equal(LocalUtc(2026, 10, 25, 19, 0), specialSlots[0].Start);
        for (var i = 1; i < specialSlots.Count; i++)
            Assert.Equal(specialSlots[i - 1].End, specialSlots[i].Start);
        Assert.Equal(LocalUtc(2026, 10, 26, 1, 0), specialSlots[^1].End);
        var regularEpisodes = scheduled.Where(s => s.ProgramRuleId == regular.Id)
            .Select(s => s.TVShowEpisodeId).ToList();
        var dump = string.Join(" | ", (await fixture.SlotsAsync(ct)).Select(s =>
            $"{(s.Status == ProgramSlotStatus.Displaced ? "X" : "S")} {s.Start:dd.HH:mm}-{s.End:dd.HH:mm} rule={s.ProgramRuleId} ep={s.TVShowEpisodeId}"));
        // The displaced head episode airs — shifted, not skipped: it is exactly the episode
        // following the kept day-1 airing, in air order.
        var day1Count = scheduled.Where(s => s.ProgramRuleId == regular.Id
            && s.Start < LocalUtc(2026, 10, 25, 12, 0)).Count();
        Assert.True(regularEpisodes.Count > day1Count, dump);
        Assert.Equal(displacedHead, regularEpisodes[day1Count]);
        Assert.DoesNotContain(scheduled, s => s.ProgramRuleId == regular.Id
            && s.Start >= LocalUtc(2026, 10, 25, 19, 0) && s.Start < LocalUtc(2026, 10, 26, 1, 0));
    }

    // The special window spans several days: every planned future airing in it is
    // displaced — the follow-up days' episodes shift consistently through the remaining
    // schedule: the air order stays complete, gapless, duplicate-free and ordered.
    [Fact]
    public async Task Reconcile_SpecialRuleMultiDay_FollowUpDaysShiftedConsistently()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("takeover-multiday", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 20, duration: TimeSpan.FromMinutes(45));
        var collection = await SeedCollectionAsync(fixture, "Special", 14, 60);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleHorizonHours = 96;
        await fixture.Db.SaveChangesAsync(ct);

        var regular = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.TVShow, show.Id,
            priority: 0, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        // Two-day special block (25.–26.10., daily 19:00, 14 h) — it covers the regular
        // 20:15 windows of both days completely.
        var special = await fixture.CreateRuleAsync(channel.Id, "Sonderprogramm", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(19, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            priority: 100, slotDuration: TimeSpan.FromHours(14),
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 10, 25), To: new DateOnly(2026, 10, 26)));
        var summary = await service.ReconcileAsync(now.AddMinutes(30), ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var scheduled = await ScheduledAsync(fixture, ct);
        // Both special days fill their 14-hour slot right through the former regular
        // windows — they are not cut off at 20:15.
        var specialSlots = scheduled.Where(s => s.ProgramRuleId == special.Id).OrderBy(s => s.Start).ToList();
        Assert.Equal(28, specialSlots.Count);
        Assert.Equal(LocalUtc(2026, 10, 25, 19, 0), specialSlots[0].Start);
        Assert.Equal(LocalUtc(2026, 10, 26, 9, 0), specialSlots[13].End);
        Assert.Equal(LocalUtc(2026, 10, 26, 19, 0), specialSlots[14].Start);
        Assert.Equal(LocalUtc(2026, 10, 27, 9, 0), specialSlots[^1].End);
        var regularEpisodes = scheduled.Where(s => s.ProgramRuleId == regular.Id)
            .OrderBy(s => s.Start).Select(s => s.TVShowEpisodeId!.Value).ToList();
        var expected = scheduled.Where(s => s.ProgramRuleId == regular.Id).Count();
        // Air order: a strictly increasing run of episode numbers — nothing skipped,
        // nothing doubled — over every shifted occurrence.
        var numbers = regularEpisodes
            .Select(id => episodes.Single(e => e.Id == id).Number).ToList();
        Assert.Equal(Enumerable.Range(numbers[0], expected), numbers);
        for (var i = 1; i < numbers.Count; i++)
            Assert.True(numbers[i] == numbers[i - 1] + 1,
                $"Folge {numbers[i - 1]} wird nicht von {numbers[i]} fortgesetzt");

        // Stability: a follow-up evaluation neither displaces nor re-materializes.
        var second = await service.ReconcileAsync(now.AddMinutes(45), ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, second, ct, "Folgelauf");
    }

    // Regression guard for equal-priority neighbours: the soft-bound "verschiebt sich"
    // mechanism is unchanged — the earlier airing still stops at the equal-priority
    // window and only its overshooting tail crosses it.
    [Fact]
    public async Task EnsureCoverage_EqualPrioritySuccessor_StillBoundsAiringStart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("takeover-equal", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = await SeedCollectionAsync(fixture, "Mittag", 8, 45);
        var nextMovie = await fixture.CreateMovieAsync("Nachmittagsfilm", duration: TimeSpan.FromMinutes(60));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var first = await fixture.CreateRuleAsync(channel.Id, "Mittag", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(14, 0), ProgramRuleMaterialType.MovieCollection, collection.Id,
            priority: 0, slotDuration: TimeSpan.FromHours(6));
        var next = await fixture.CreateRuleAsync(channel.Id, "Nachmittag", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(16, 0), ProgramRuleMaterialType.Movie, nextMovie.Id,
            priority: 0, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var scheduled = await ScheduledAsync(fixture, ct);
        var firstDay1 = scheduled.Where(s => s.ProgramRuleId == first.Id
            && s.Start >= LocalUtc(2026, 10, 24, 14, 0) && s.Start < LocalUtc(2026, 10, 24, 16, 0)).ToList();
        // Equal priority: the 16:00 window still bounds starts — items begin at 14:00,
        // 14:45 and 15:30; the last one overshoots to 16:15 and the equal-priority
        // successor shifts behind its real end ("verschiebt sich").
        Assert.Equal(3, firstDay1.Count);
        Assert.Equal(LocalUtc(2026, 10, 24, 15, 30), firstDay1[^1].Start);
        Assert.Equal(LocalUtc(2026, 10, 24, 16, 15), firstDay1[^1].End);
        var nextDay1 = scheduled.Where(s => s.ProgramRuleId == next.Id
            && s.Start >= LocalUtc(2026, 10, 24, 12, 0) && s.Start < LocalUtc(2026, 10, 25, 12, 0)).ToList();
        Assert.Equal(firstDay1[^1].End, nextDay1[0].Start);
    }
}
