using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramScheduleServiceTests_Generation
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;

    private static DateTime LocalUtc(int year, int month, int day, int hour, int minute)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    [Fact]
    public async Task EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-daily-movie", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Start on a Saturday 2026-10-24 12:00 local so TimeOfDay 20:00 occurrences exist both days.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var service = fixture.CreateService();
        var summary = await service.EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var ruleSlots = slots.Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.True(ruleSlots.Count >= 2);
        Assert.All(ruleSlots, s => Assert.Equal(movie.Id, s.MovieId));
        // Each airing starts exactly at 20:00 local.
        Assert.All(ruleSlots, s => Assert.Equal(20, TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz).Hour));
        Assert.Equal(ruleSlots.Count, summary.SlotsCreated - summary.FillerSlotsCreated);
    }

    [Fact]
    public async Task EnsureCoverage_WeeklyRule_OnlyOnDayOfWeek()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-weekly", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Sunday 2026-10-25 10:00 local; weekly rule on Mondays.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 25, 10, 0));
        await fixture.CreateRuleAsync(channel.Id, "Montagsfilm", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Weekly,
            new TimeOnly(20, 15), ProgramRuleMaterialType.Movie, movie.Id, dayOfWeek: DayOfWeek.Monday);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var ruleSlots = slots.Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.NotEmpty(ruleSlots);
        Assert.All(ruleSlots, s => Assert.Equal(DayOfWeek.Monday, TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz).DayOfWeek));
    }

    [Fact]
    public async Task EnsureCoverage_Once_OnlyOnSingleDate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-once", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var singleDate = new DateOnly(2026, 10, 25);
        await fixture.CreateRuleAsync(channel.Id, "Einmalig", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Once,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id, singleDate: singleDate);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var ruleSlots = slots.Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Single(ruleSlots);
        Assert.Equal(singleDate, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ruleSlots[0].Start, DateTimeKind.Utc), Tz)));
    }

    [Fact]
    public async Task EnsureCoverage_Holiday_OnlyOnHoliday()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-holiday", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Weihnachtsfilm", duration: TimeSpan.FromMinutes(90));

        // 2026-12-24 (Thu) 10:00; holiday recurrence; 25.+26.12. are nationwide holidays.
        var now = new DateTimeOffset(LocalUtc(2026, 12, 24, 10, 0));
        await fixture.CreateRuleAsync(channel.Id, "Feiertag", ProgramRuleType.HolidaySpecial, ProgramRuleRecurrence.Holiday,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var ruleSlots = slots.Where(s => s.RuleType == ProgramRuleType.HolidaySpecial).ToList();
        Assert.NotEmpty(ruleSlots);
        Assert.All(ruleSlots, s =>
        {
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.True(GermanHolidayCalendar.IsHoliday(local, null));
        });
    }

    [Fact]
    public async Task EnsureCoverage_WeekdayRule_OnlyMondayToFriday()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-weekday", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Werktagsfilm", duration: TimeSpan.FromMinutes(90));

        // Friday 2026-10-23 10:00 local; weekday rule at 20:00 — Sa/So must be skipped.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 23, 10, 0));
        await fixture.CreateRuleAsync(channel.Id, "Werktags", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Weekday,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.NotEmpty(ruleSlots);
        Assert.All(ruleSlots, s =>
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz);
            Assert.True(local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        });
    }

    [Fact]
    public async Task EnsureCoverage_WeekendRule_OnlySaturdaySunday()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-weekend", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Wochenendfilm", duration: TimeSpan.FromMinutes(90));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 23, 10, 0));
        await fixture.CreateRuleAsync(channel.Id, "Wochenende", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Weekend,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.NotEmpty(ruleSlots);
        Assert.All(ruleSlots, s =>
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz);
            Assert.True(local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        });
    }

    [Fact]
    public async Task EnsureCoverage_OneTimeRule_SchedulesOnlyWithinWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-onetime", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Samstag 2026-10-24 12:00 lokal; Gültigkeitszeitraum 25.–26.10. (inklusiv).
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleHorizonHours = 96;
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateRuleAsync(channel.Id, "Zeitraum", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 10, 25), To: new DateOnly(2026, 10, 26)));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Equal(2, ruleSlots.Count);
        Assert.All(ruleSlots, s =>
        {
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.Contains(local, new[] { new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 26) });
        });
    }

    [Fact]
    public async Task EnsureCoverage_OneTimeRule_OpenStart_SchedulesUntilEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-onetime-open", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Einseitig offenes Fenster: nur Bis-Datum 25.10. — der 24. gilt noch, der 26. nicht mehr.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Offen", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, To: new DateOnly(2026, 10, 25)));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Equal(2, ruleSlots.Count);
        Assert.All(ruleSlots, s =>
        {
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.True(local <= new DateOnly(2026, 10, 25));
        });
    }

    [Fact]
    public async Task EnsureCoverage_AnnualRule_SchedulesOnlyWithinWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-annual", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Jahresfenster 25.10.–26.10. — nur diese beiden Tage erzeugen Occurrences.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleHorizonHours = 96;
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateRuleAsync(channel.Id, "Jahresfenster", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow,
                From: new DateOnly(2000, 10, 25), To: new DateOnly(2000, 10, 26)));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Equal(2, ruleSlots.Count);
        Assert.All(ruleSlots, s =>
        {
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.Contains(local, new[] { new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 26) });
        });
    }

    [Fact]
    public async Task EnsureCoverage_MonthlyRule_SchedulesOnlyWithinWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-monthly", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Monatsfenster Tag 25–31: innerhalb des Horizonts nur der 25. und 26.10.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Monatsfenster", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: 25, ToDay: 31));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Equal(2, ruleSlots.Count);
        Assert.All(ruleSlots, s =>
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz);
            Assert.True(local.Day is >= 25 and <= 31);
        });
    }

    [Fact]
    public async Task EnsureCoverage_WeeklyRule_SchedulesOnlyOnSelectedWeekdays()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-weekly", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Wochentags-Gültigkeit nur Sonntag+Montag → im Horizont nur der 25.10. (So).
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Wochenfenster", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow,
                Weekdays: (1 << (int)DayOfWeek.Sunday) | (1 << (int)DayOfWeek.Monday)));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Single(ruleSlots);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ruleSlots[0].Start, DateTimeKind.Utc), Tz);
        Assert.Equal(DayOfWeek.Sunday, local.DayOfWeek);
    }

    [Fact]
    public async Task EnsureCoverage_HolidayRule_SchedulesOnlyAroundAnchor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-holiday", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Feiertagsfenster um den 1. Weihnachtstag (−1 bis +1 Tag) → gültig 24.–26.12.
        var now = new DateTimeOffset(LocalUtc(2026, 12, 24, 10, 0));
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleHorizonHours = 96;
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateRuleAsync(channel.Id, "Weihnachtsfenster", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow,
                HolidayAnchor: ProgramRuleHolidayAnchor.ChristmasDay, HolidayBeforeDays: 1, HolidayAfterDays: 1));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Equal(3, ruleSlots.Count);
        Assert.All(ruleSlots, s =>
        {
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.Contains(local, new[] { new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 26) });
        });
    }

    [Fact]
    public async Task EnsureCoverage_ExpiredOneTimeRule_ProducesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-expired", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(90));

        // Komplett vergangenes Fenster — keine Occurrences, keine Slots.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Abgelaufen", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 20)));

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var ruleSlots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        Assert.Empty(ruleSlots);
        Assert.Equal(0, summary.SlotsCreated - summary.FillerSlotsCreated);
    }

    [Fact]
    public async Task EnsureCoverage_InvalidFiller_FillsOnlyWithinWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-validity-filler", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(30));

        // Filler nur am 25.10. gültig — die übrigen Lücken bleiben ungefüllt.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, movie.Id, episodeCount: 1,
            validity: new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 10, 25), To: new DateOnly(2026, 10, 25)));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.Equal(ProgramRuleType.Filler, s.RuleType);
            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.Start, DateTimeKind.Utc), Tz));
            Assert.Equal(new DateOnly(2026, 10, 25), local);
        });
    }

    [Fact]
    public async Task EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-series-cursor", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 5, duration: TimeSpan.FromMinutes(45));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Abendserie", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 2);

        var service = fixture.CreateService();
        await service.EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var episodeOrder = slots.Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime).Select(s => s.TVShowEpisodeId).ToList();
        // 48 h horizon at 2 episodes/day → 3 airings in the window; the lookback airing
        // (yesterday 20:00) already consumed episodes 1+2 → today gets 3,4 | 5,wrap→1.
        var expected = episodes.Skip(2).Select(e => (long?)e.Id).Concat(new long?[] { episodes[0].Id }).ToList();
        Assert.Equal(expected, episodeOrder);

        var cursor = await fixture.Db.ProgramRuleCursors.SingleAsync(ct);
        Assert.Equal(ProgramCursorContentType.TVShowEpisode, cursor.LastContentType);
        Assert.Equal(episodes[0].Id, cursor.LastContentId);

        // A second run continues where the first stopped (extends coverage).
        var later = now.AddHours(30);
        await service.EnsureCoverageAsync(later, ct);
        var slots2 = await fixture.SlotsAsync(ct);
        var last = slots2.Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime).Select(s => s.TVShowEpisodeId).ToList();
        // Continues in air order after the wrapped position — no restart at episode 1 mid-series.
        Assert.True(last.Count > 4);
        Assert.Contains(episodes[4].Id, last.Cast<long>());
    }

    [Fact]
    public async Task EnsureCoverage_SeriesExhausted_WrapsAround()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-series-wrap", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var (show, _, episodes) = await fixture.CreateShowAsync("Serie", 2, duration: TimeSpan.FromMinutes(30));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Dauerschleife", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(12, 0), ProgramRuleMaterialType.TVShow, show.Id, episodeCount: 3);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var order = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.SeriesFixedTime)
            .Select(s => s.TVShowEpisodeId).ToList();
        // 3 episodes/airing but only 2 episodes exist → wraps 1,2,1|2,1,2...; the lookback airing
        // consumed 1,2,1 already → the first placed airing is 2,1,2.
        Assert.Equal(new long?[] { episodes[1].Id, episodes[0].Id, episodes[1].Id }, order.Take(3).ToList());
    }

    [Fact]
    public async Task EnsureCoverage_SlotDuration_FillsUntilTarget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-slotduration", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.MovieCollections.Add(collection);
        await fixture.Db.SaveChangesAsync(ct);
        await fixture.CreateMovieAsync("F1", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2000, 1, 1));
        await fixture.CreateMovieAsync("F2", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2001, 1, 1));
        await fixture.CreateMovieAsync("F3", collectionId: collection.Id, duration: TimeSpan.FromMinutes(60), releaseDate: new DateTime(2002, 1, 1));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.MovieCollection, collection.Id, slotDuration: TimeSpan.FromMinutes(150));

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = (await fixture.SlotsAsync(ct)).Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).ToList();
        // First airing covers 3 movies (180 min >= 150 min), then wraps.
        Assert.True(slots.Count >= 3);
        var firstAiring = slots.Take(3).ToList();
        Assert.Equal(TimeSpan.FromHours(3), firstAiring[^1].End - firstAiring[0].Start);
    }

    [Fact]
    public async Task EnsureCoverage_PlanIsGapless_WhenFillerExists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-gapless", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Primetimefilm", duration: TimeSpan.FromMinutes(100));
        var filler = await fixture.CreateMovieAsync("Füller", duration: TimeSpan.FromMinutes(20));

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);
        await fixture.CreateRuleAsync(channel.Id, "Füller", ProgramRuleType.Filler, ProgramRuleRecurrence.Daily,
            new TimeOnly(0, 0), ProgramRuleMaterialType.Movie, filler.Id, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        ProgramScheduleTestDb.AssertGapless(slots, now.UtcDateTime, now.UtcDateTime.AddHours(48));
        Assert.Equal(0, summary.RemainingGapSeconds);
    }

    [Fact]
    public async Task EnsureCoverage_OrphanedMaterial_ProducesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-orphan", ct);
        var channel = await fixture.GetChannelAsync(ct);

        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 12, 0));
        // MaterialId 999 exists nowhere.
        await fixture.CreateRuleAsync(channel.Id, "Verwaist", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.TVShow, 999, episodeCount: 1);

        var summary = await fixture.CreateService().EnsureCoverageAsync(now, ct);

        Assert.Equal(0, summary.SlotsCreated);
        Assert.True(summary.OrphanedRules >= 1);
        Assert.Empty(await fixture.Db.ProgramSlots.ToListAsync(ct));
    }

    [Fact]
    public async Task EnsureCoverage_SpanningOccurrence_KeepsTrueStart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-spanning", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("Film", duration: TimeSpan.FromMinutes(120));

        // Now = 21:00 local; daily rule at 20:00 → airing started an hour ago, still running.
        var now = new DateTimeOffset(LocalUtc(2026, 10, 24, 21, 0));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Daily,
            new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id);

        await fixture.CreateService().EnsureCoverageAsync(now, ct);

        var slots = await fixture.SlotsAsync(ct);
        var first = slots.Where(s => s.RuleType == ProgramRuleType.MoviePrimeTime).OrderBy(s => s.Start).First();
        Assert.Equal(LocalUtc(2026, 10, 24, 20, 0), first.Start);
    }

    [Fact]
    public async Task EnsureCoverage_SecondRun_ExtendsNotDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("schedule-extend", ct);
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
        var firstCount = await fixture.Db.ProgramSlots.CountAsync(ct);

        // Same run again 15 min later: only extends beyond previous coverage (past slot count must not shrink, no dup).
        var later = now.AddMinutes(15);
        await service.EnsureCoverageAsync(later, ct);

        var slots = await fixture.SlotsAsync(ct);
        Assert.True(slots.Count >= firstCount);
        Assert.Equal(slots.Count, slots.Select(s => (s.Start, s.End, s.MediaItemId)).Distinct().Count());
        ProgramScheduleTestDb.AssertGapless(slots, now.UtcDateTime, later.UtcDateTime.AddHours(48));
    }
}
