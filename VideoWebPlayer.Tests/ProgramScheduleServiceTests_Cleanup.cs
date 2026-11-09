using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramScheduleService.CleanupSelfDisplacedSlotsAsync"/>: the worker
/// housekeeping that deletes displaced rows with a proven scheduled twin (self-displacement
/// duplicates). The strict twin criterion — identical channel, interval, producing rule,
/// <see cref="ProgramSlot.IsClipFill"/> and content — keeps genuine displacement history.
/// </summary>
public sealed class ProgramScheduleServiceTests_Cleanup
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    private static async Task<List<ProgramSlot>> ScheduledAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Scheduled).ToList();

    private static async Task<List<ProgramSlot>> DisplacedAsync(ProgramScheduleTestDb fixture, CancellationToken ct)
        => (await fixture.SlotsAsync(ct)).Where(s => s.Status == ProgramSlotStatus.Displaced).ToList();

    [Fact]
    public async Task CleanupSelfDisplaced_RemovesProvenDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-duplicates", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var itemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).SingleAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var start = LocalUtc(2026, 10, 24, 20, 0);
        var end = LocalUtc(2026, 10, 24, 21, 30);
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: rule.Id);
        // Duplikat-Kette: drei verdrängte Kopien desselben Sendeplatzes.
        for (var i = 0; i < 3; i++)
            await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: rule.Id,
                status: ProgramSlotStatus.Displaced);

        var service = fixture.CreateService();
        var deleted = await service.CleanupSelfDisplacedSlotsAsync(ct);

        Assert.Equal(3, deleted);
        var scheduled = await ScheduledAsync(fixture, ct);
        Assert.Single(scheduled); // der geplante Zwilling bleibt
        Assert.Empty(await DisplacedAsync(fixture, ct));
    }

    [Fact]
    public async Task CleanupSelfDisplaced_KeepsDisplacedWithoutTwin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-no-twin", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var other = await fixture.CreateMovieAsync("Anderer", duration: TimeSpan.FromMinutes(90));
        var itemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).SingleAsync(ct);
        var otherItemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == other.Id).Select(l => l.MediaItemId).SingleAsync(ct);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        var foreign = await fixture.CreateRuleAsync(channel.Id, "Fremd", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(21, 0), ProgramRuleMaterialType.Movie, other.Id);

        var start = LocalUtc(2026, 10, 24, 20, 0);
        var end = LocalUtc(2026, 10, 24, 21, 30);
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: rule.Id);

        // (a) Echter Verdrängungsverlierer: gleiches Intervall und Regel, anderer Inhalt.
        await fixture.CreateSlotAsync(channel.Id, start, end, otherItemId, movieId: other.Id, ruleId: rule.Id,
            status: ProgramSlotStatus.Displaced);
        // (b) Verdrängt ohne geplanten Zwilling (freies Intervall).
        await fixture.CreateSlotAsync(channel.Id, LocalUtc(2026, 10, 24, 22, 0), LocalUtc(2026, 10, 24, 23, 30),
            itemId, movieId: movie.Id, ruleId: rule.Id, status: ProgramSlotStatus.Displaced);
        // (c) Gleiches Intervall und Inhalt, aber fremde erzeugende Regel.
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: foreign.Id,
            status: ProgramSlotStatus.Displaced);
        // (d) Abweichende Clip-Füll-Markierung.
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: rule.Id,
            status: ProgramSlotStatus.Displaced, isClipFill: true);

        var service = fixture.CreateService();
        var deleted = await service.CleanupSelfDisplacedSlotsAsync(ct);

        Assert.Equal(0, deleted);
        Assert.Equal(4, (await DisplacedAsync(fixture, ct)).Count);
    }

    // EF Core kompensiert die Null-Semantik: s.ProgramRuleId == d.ProgramRuleId matcht auch
    // zwei NULL-Werte. Ein Zwillingpaar ohne erzeugende Regel ist damit ebenfalls beweisbar —
    // Intervall, Inhalt und Clip-Füllung stimmen überein — und wird entfernt.
    [Fact]
    public async Task CleanupSelfDisplaced_NullRuleIdPair_Removes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-null-rule", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var itemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).SingleAsync(ct);

        var start = LocalUtc(2026, 10, 25, 8, 0);
        var end = LocalUtc(2026, 10, 25, 9, 0);
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: null);
        await fixture.CreateSlotAsync(channel.Id, start, end, itemId, movieId: movie.Id, ruleId: null,
            status: ProgramSlotStatus.Displaced);

        var service = fixture.CreateService();
        var deleted = await service.CleanupSelfDisplacedSlotsAsync(ct);

        Assert.Equal(1, deleted);
        Assert.Empty(await DisplacedAsync(fixture, ct));
    }

    [Fact]
    public async Task CleanupSelfDisplaced_RepresentativeSwitch_Removes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-rep-switch", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));
        var firstItemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).SingleAsync(ct);
        var secondItem = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.LinkMovieMediaItemAsync(movie.Id, secondItem.Id);
        var rule = await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var start = LocalUtc(2026, 10, 24, 20, 0);
        var end = LocalUtc(2026, 10, 24, 21, 30);
        // Repräsentantenwechsel: der geplante Zwilling trägt die andere Datei desselben Films.
        await fixture.CreateSlotAsync(channel.Id, start, end, secondItem.Id, movieId: movie.Id, ruleId: rule.Id);
        await fixture.CreateSlotAsync(channel.Id, start, end, firstItemId, movieId: movie.Id, ruleId: rule.Id,
            status: ProgramSlotStatus.Displaced);

        var service = fixture.CreateService();
        var deleted = await service.CleanupSelfDisplacedSlotsAsync(ct);

        Assert.Equal(1, deleted);
        Assert.Empty(await DisplacedAsync(fixture, ct));
    }

    // Sicherheits-Gegenprobe: liegt die Invader-Evidenz einer verschobenen Ausstrahlung als
    // Scheduled/Displaced-Zwillingspaar vor, entfernt die Bereinigung nur die Kopie — das
    // Intervall bleibt durch den Zwilling vollständig abgedeckt, das folgende Reconcile
    // bleibt ein No-Op (kein Evidenz-Verlust, keine echte Neuplanung).
    [Fact]
    public async Task CleanupSelfDisplaced_PreservesInvaderEvidence_NoReplan()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("cleanup-evidence", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Vormittagsfilm", duration: TimeSpan.FromMinutes(90));
        var (show, _, _) = await fixture.CreateShowAsync("Serie", 40, duration: TimeSpan.FromMinutes(45));
        var itemId = await fixture.Db.MovieMediaItems.Where(l => l.MovieId == movie.Id).Select(l => l.MediaItemId).SingleAsync(ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var invader = await fixture.CreateRuleAsync(channel.Id, "Vormittag", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(15, 30), ProgramRuleMaterialType.Movie, movie.Id);
        var shifted = await fixture.CreateRuleAsync(channel.Id, "Vorbendserien", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(16, 0), ProgramRuleMaterialType.TVShow, show.Id, slotDuration: TimeSpan.FromHours(4));

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);
        var shiftedBefore = (await ScheduledAsync(fixture, ct))
            .Where(s => s.ProgramRuleId == shifted.Id).ToList();
        Assert.Equal(LocalUtc(2026, 10, 24, 17, 0), shiftedBefore[0].Start);

        // Evidenz-Zwillingspaar: zusätzliche verdrängte Kopie des Invader-Sendeplatzes.
        await fixture.CreateSlotAsync(channel.Id, LocalUtc(2026, 10, 24, 15, 30), LocalUtc(2026, 10, 24, 17, 0),
            itemId, movieId: movie.Id, ruleId: invader.Id, status: ProgramSlotStatus.Displaced);

        var deleted = await service.CleanupSelfDisplacedSlotsAsync(ct);
        Assert.Equal(1, deleted);

        var summary = await service.ReconcileAsync(now, ct);
        await ProgramScheduleTestDb.AssertReconcileNoOpAsync(fixture, summary, ct);
        var shiftedAfter = (await ScheduledAsync(fixture, ct))
            .Where(s => s.ProgramRuleId == shifted.Id).ToList();
        Assert.Equal(shiftedBefore.Select(s => (s.Start, s.End)).ToList(),
            shiftedAfter.Select(s => (s.Start, s.End)).ToList());
    }
}
