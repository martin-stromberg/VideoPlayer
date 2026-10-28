using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="ProgramScheduleService.PurgeExpiredRulesAsync"/>: the worker-side cleanup
/// that deletes one-off (<see cref="ProgramRuleValidityMode.OneTime"/>) rules whose Bis date has
/// passed — against real SQLite so the FK cascade on cursors and join rows is proven.
/// </summary>
public sealed class ProgramScheduleServiceTests_Purge
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    [Fact]
    public async Task PurgeExpiredRules_DeletesExpiredOneTimeRule()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("purge-expired", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Ablaufdatum 20.10. — der lokale Tag des Laufes (24.10.) liegt dahinter.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var rule = await fixture.CreateRuleAsync(channel.Id, "Abgelaufen", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 20)));
        fixture.Db.ProgramRuleCursors.Add(new ProgramRuleCursor
        {
            ProgramRuleId = rule.Id,
            LastContentType = ProgramCursorContentType.Movie,
            LastContentId = movie.Id
        });
        await fixture.SaveDiagAsync();

        var service = fixture.CreateService();
        var deleted = await service.PurgeExpiredRulesAsync(now, ct);

        Assert.Equal(1, deleted);
        Assert.Equal(0, await fixture.Db.ProgramRules.CountAsync(ct));
        // FK-Kaskade: der Fortsetzungsstand ist mitgelöscht.
        Assert.Equal(0, await fixture.Db.ProgramRuleCursors.CountAsync(ct));
    }

    [Fact]
    public async Task PurgeExpiredRules_SlotsDisplacedAfterReconcile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("purge-displaced", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Fenster nur am 24.10.: die 20h-Ausstrahlung ab 20:00 läuft über den Tageswechsel
        // hinaus in den 25.10. — Lauf am 25.10. löscht die abgelaufene Regel mitten im Block.
        var rule = await fixture.CreateRuleAsync(channel.Id, "Befristet", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(20),
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, To: new DateOnly(2026, 10, 24)));
        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0)), ct);
        Assert.True((await fixture.SlotsAsync(ct)).Any(s => s.ProgramRuleId == rule.Id));

        var purgeNow = new DateTimeOffset(LocalUtc(2026, 10, 25, 12, 0));
        Assert.Equal(1, await service.PurgeExpiredRulesAsync(purgeNow, ct));
        var summary = await service.ReconcileAsync(purgeNow, ct);

        Assert.True(summary.SlotsDisplaced > 0);
        var slots = await fixture.SlotsAsync(ct);
        // Soft-Referenz: die Slots bleiben als Zeilen (Historie) und tragen weiter die Regel-Id;
        // nur der noch laufende Rest wird Displaced, der ausgestrahlte Kopf bleibt Scheduled.
        var orphaned = slots.Where(s => s.ProgramRuleId == rule.Id).ToList();
        Assert.NotEmpty(orphaned);
        Assert.All(orphaned.Where(s => s.End > purgeNow.UtcDateTime), s => Assert.Equal(ProgramSlotStatus.Displaced, s.Status));
    }

    [Fact]
    public async Task PurgeExpiredRules_KeepsOpenEndAndRecurringRules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("purge-keeps", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // Einseitig offen ohne Bis-Datum — läuft nie ab.
        await fixture.CreateRuleAsync(channel.Id, "Offen", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, From: new DateOnly(2026, 1, 1)));
        // Noch laufender Zeitraum (Bis heute → nicht abgelaufen).
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now.UtcDateTime, Tz));
        await fixture.CreateRuleAsync(channel.Id, "BisHeute", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(21, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, To: localToday));
        // Wiederkehrende Fenster bleiben grundsätzlich erhalten — auch außerhalb der Gültigkeit.
        await fixture.CreateRuleAsync(channel.Id, "Jaehrlich", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(22, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow,
                From: new DateOnly(2000, 1, 1), To: new DateOnly(2000, 1, 2)));
        await fixture.CreateRuleAsync(channel.Id, "Monatlich", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(23, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: 1, ToDay: 2));

        var deleted = await fixture.CreateService().PurgeExpiredRulesAsync(now, ct);

        Assert.Equal(0, deleted);
        Assert.Equal(4, await fixture.Db.ProgramRules.CountAsync(ct));
    }
}
