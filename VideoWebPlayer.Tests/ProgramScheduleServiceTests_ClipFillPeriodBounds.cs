using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression tests for issue #291: placeholder clip distribution inside a slot-duration
/// rule period must stay inside the nominal period (occurrence start + slot duration),
/// keep chronological order and never overlap occupied or reserved space.
/// </summary>
public sealed class ProgramScheduleServiceTests_ClipFillPeriodBounds
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int y, int mo, int d, int h, int mi)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified), Tz);

    // The 20:15 / 240 min "Primetime" rule of the issue: period end is 00:15 the next day.
    private static DateTime PeriodEnd => LocalUtc(2026, 10, 27, 20, 15) + TimeSpan.FromMinutes(240);

    private static async Task<(ProgramRule Rule, HashSet<long> ClipIds)> SeedPrimetimeAsync(
        ProgramScheduleTestDb fixture, CancellationToken ct, TimeSpan? grace = null)
    {
        var channel = await fixture.GetChannelAsync(ct);
        await fixture.EnsureSourceAsync(1);
        var collection = new MovieCollection { Name = "Primetime-Filme", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.SaveDiagAsync();
        // Zwei Titel je Ausstrahlung (212 min) lassen mit Grace 30 min 28 min Rest — die
        // Lookback-Occurrence des Vortags verbraucht denselben Ring, also muessen alle
        // Vierer-Paare gleich lang sein. Unter der ItemOrderKey-Ringmischung (Issue #297)
        // ist die Entnahmereihenfolge eine Permutation: nur wirklich gleichlange Titel
        // (106-119 min) halten die Paarsumme permutationsinvariant — 4×105 ergaebe Rest
        // exakt 30 = Grace (strikt <), ab 120 stuende kein Rest mehr fuer Clips.
        foreach (var minutes in new[] { 106, 106, 106, 106 })
            await fixture.CreateMovieAsync($"Film {minutes}", 1, TimeSpan.FromMinutes(minutes), collection.Id);

        var group = await fixture.CreateClipGroupAsync("Platzhalter", ct);
        var clipIds = new HashSet<long>();
        for (var i = 0; i < 220; i++)
        {
            var clip = await fixture.CreateMediaItemAsync(1, TimeSpan.FromSeconds(14));
            clipIds.Add(clip.Id);
            await fixture.AddClipToGroupAsync(group.Id, clip.Id, ct);
        }

        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 15), ProgramRuleMaterialType.MovieCollection, collection.Id,
            slotDuration: TimeSpan.FromMinutes(240), clipGroupIds: [group.Id],
            placeholderGrace: grace ?? TimeSpan.FromMinutes(15));
        return (rule, clipIds);
    }

    private static async Task AssertNoOverlapAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
    {
        var slots = (await fixture.Db.ProgramSlots.AsNoTracking()
                .Where(s => s.Status == ProgramSlotStatus.Scheduled)
                .OrderBy(s => s.Start).ToListAsync(ct))
            .OrderBy(s => s.Start).ThenBy(s => s.End).ToList();
        for (var i = 1; i < slots.Count; i++)
            Assert.True(slots[i].Start >= slots[i - 1].End,
                $"Ueberlappung: [{slots[i - 1].Start:HH:mm}-{slots[i - 1].End:HH:mm}] vs [{slots[i].Start:HH:mm}-{slots[i].End:HH:mm}]");
    }

    [Fact]
    public async Task EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cf-midperiod", ct);
        var (rule, clipIds) = await SeedPrimetimeAsync(fixture, ct);

        // Lauf startet mitten in der laufenden Periode (22:30) — der Lookback materialisiert
        // nur das Suffix der Ausstrahlung; das Periodenende bleibt 00:15.
        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 27, 22, 30)), ct);

        var slots = await fixture.Db.ProgramSlots.AsNoTracking()
            .Where(s => s.ProgramRuleId == rule.Id && s.Start < LocalUtc(2026, 10, 28, 20, 15))
            .OrderBy(s => s.Start).ToListAsync(ct);
        var clipSlots = slots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();

        Assert.All(clipSlots, s => Assert.True(s.Start < PeriodEnd,
            $"Clip startet {s.Start:HH:mm} nach Periodenende {PeriodEnd:HH:mm}"));
        await AssertNoOverlapAsync(fixture, ct);
    }

    [Fact]
    public async Task EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cf-occupied", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (rule, clipIds) = await SeedPrimetimeAsync(fixture, ct, grace: TimeSpan.FromMinutes(30));

        // Höherpriore Regel mitten in der Primetime-Periode (00:00–00:30).
        var lateMovie = await fixture.CreateMovieAsync("Nachrichten", 1, TimeSpan.FromMinutes(30));
        await fixture.CreateRuleAsync(channel.Id, "Mitternacht", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, lateMovie.Id,
            priority: 9, episodeCount: 1);

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 27, 19, 0)), ct);

        await AssertNoOverlapAsync(fixture, ct);
        var clipSlots = await fixture.Db.ProgramSlots.AsNoTracking()
            .Where(s => s.ProgramRuleId == rule.Id && clipIds.Contains(s.MediaItemId)
                && s.Start >= LocalUtc(2026, 10, 27, 20, 15) && s.Start < LocalUtc(2026, 10, 28, 20, 15))
            .OrderBy(s => s.Start).ToListAsync(ct);
        Assert.All(clipSlots, s => Assert.True(s.Start < PeriodEnd,
            $"Clip startet {s.Start:HH:mm} nach Periodenende"));
    }

    [Fact]
    public async Task EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cf-full", ct);
        var (rule, clipIds) = await SeedPrimetimeAsync(fixture, ct, grace: TimeSpan.FromMinutes(30));

        await fixture.CreateService().EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 27, 12, 0)), ct);

        var slots = await fixture.Db.ProgramSlots.AsNoTracking()
            .Where(s => s.ProgramRuleId == rule.Id && s.Start >= LocalUtc(2026, 10, 27, 20, 15) && s.Start < LocalUtc(2026, 10, 28, 20, 15))
            .OrderBy(s => s.Start).ToListAsync(ct);

        var clipSlots = slots.Where(s => clipIds.Contains(s.MediaItemId)).ToList();
        var titles = slots.Where(s => !clipIds.Contains(s.MediaItemId)).ToList();

        // Verteilung: Clips vor dem ersten Titel, zwischen den Titeln und nach dem letzten.
        Assert.NotEmpty(clipSlots);
        Assert.True(titles.Count >= 2);
        Assert.True(clipSlots.Any(s => s.End <= titles[0].Start), "keine Clips vor dem ersten Titel");
        Assert.True(clipSlots.Any(s => s.Start >= titles[^1].End), "keine Clips nach dem letzten Titel");

        // Grenzen: kein Clip-Block beginnt nach dem nominalen Periodenende.
        Assert.All(clipSlots, s => Assert.True(s.Start < PeriodEnd,
            $"Clip startet {s.Start:HH:mm} nach Periodenende {PeriodEnd:HH:mm}"));

        // Chronologie: lueckenlos verkettet und ueberlappungsfrei.
        for (var i = 1; i < slots.Count; i++)
            Assert.Equal(slots[i - 1].End, slots[i].Start);
        await AssertNoOverlapAsync(fixture, ct);
    }
}
