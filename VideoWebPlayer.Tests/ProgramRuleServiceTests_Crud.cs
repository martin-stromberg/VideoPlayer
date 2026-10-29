using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Crud
{
    private static ProgramRuleService CreateService(ApplicationDbContext db) => new(db);

    private static async Task<(PairingTestDb Fixture, ApplicationUser Admin, TVChannel Channel)> SetupAsync(string name, CancellationToken ct)
    {
        var fixture = await PairingTestDb.CreateAsync(name, ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = new ProgramRuleService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        return (fixture, admin, channel);
    }

    private static async Task<(PairingTestDb Fixture, ApplicationUser Admin, TVChannel Channel, TVShow Show)> SetupWithShowAsync(string name, CancellationToken ct)
    {
        var (fixture, admin, channel) = await SetupAsync(name, ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        return (fixture, admin, channel, show);
    }

    [Fact]
    public async Task CreateRule_ValidInput_PersistsRule()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-create", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        var input = new ProgramRuleInput("Abendserie", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Weekly, DayOfWeek.Friday, null, new TimeOnly(20, 15), 2, null,
            ProgramRuleMaterialType.TVShow, show.Id, 5, true);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id, input, ct);

        Assert.True(rule.Id > 0);
        var stored = await fixture.Db.ProgramRules.SingleAsync(ct);
        Assert.Equal("Abendserie", stored.Name);
        Assert.Equal(ProgramRuleType.SeriesFixedTime, stored.RuleType);
        Assert.Equal(ProgramRuleRecurrence.Weekly, stored.Recurrence);
        Assert.Equal(DayOfWeek.Friday, stored.DayOfWeek);
        Assert.Equal(new TimeOnly(20, 15), stored.TimeOfDay);
        Assert.Equal(2, stored.EpisodeCount);
        Assert.Equal(ProgramRuleMaterialType.TVShow, stored.MaterialType);
        Assert.Equal(show.Id, stored.MaterialId);
        Assert.Equal(5, stored.Priority);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task GetRulesAsync_ReturnsRulesOrderedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-list", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        await service.CreateRuleAsync(admin.Id, channel.Id, ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with { Name = "B-Regel" }, ct);
        await service.CreateRuleAsync(admin.Id, channel.Id, ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with { Name = "A-Regel" }, ct);

        var rules = await service.GetRulesAsync(admin.Id, channel.Id, ct);

        Assert.Equal(new[] { "A-Regel", "B-Regel" }, rules.Select(r => r.Name));
    }

    [Fact]
    public async Task CreateRule_OneTimeValidity_PersistsFromTo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-onetime", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with
            {
                Name = "Einmalig",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                    From: new DateOnly(2026, 3, 1), To: new DateOnly(2026, 6, 30))
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.OneTime, rule.ValidityMode);
        Assert.Equal(new DateOnly(2026, 3, 1), rule.ValidFrom);
        Assert.Equal(new DateOnly(2026, 6, 30), rule.ValidTo);
    }

    [Fact]
    public async Task CreateRule_AnnualWindowValidity_PersistsMonthDayBounds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-annual", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with
            {
                Name = "Jaehrlich",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow,
                    From: new DateOnly(2000, 12, 20), To: new DateOnly(2000, 1, 6))
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.AnnualWindow, rule.ValidityMode);
        Assert.Equal(new DateOnly(2000, 12, 20), rule.ValidFrom);
        Assert.Equal(new DateOnly(2000, 1, 6), rule.ValidTo);
    }

    [Fact]
    public async Task CreateRule_MonthlyWindowValidity_PersistsDays()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-monthly", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with
            {
                Name = "Monatlich",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: 25, ToDay: 5)
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.MonthlyWindow, rule.ValidityMode);
        Assert.Equal(25, rule.ValidFromDay);
        Assert.Equal(5, rule.ValidToDay);
    }

    [Fact]
    public async Task CreateRule_WeeklyWindowValidity_PersistsWeekdayMask()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-weekly", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with
            {
                Name = "Woechentlich",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow,
                    Weekdays: (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Friday))
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.WeeklyWindow, rule.ValidityMode);
        Assert.Equal((1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Friday), rule.ValidWeekdays);
    }

    [Fact]
    public async Task CreateRule_HolidayWindowValidity_PersistsAnchorAndOffsets()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-holiday", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with
            {
                Name = "Feiertag",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow,
                    HolidayAnchor: ProgramRuleHolidayAnchor.EasterSunday, HolidayBeforeDays: 3, HolidayAfterDays: 0)
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.HolidayWindow, rule.ValidityMode);
        Assert.Equal(ProgramRuleHolidayAnchor.EasterSunday, rule.ValidHolidayAnchor);
        Assert.Equal(3, rule.ValidHolidayBeforeDays);
        Assert.Equal(0, rule.ValidHolidayAfterDays);
    }

    [Fact]
    public async Task UpdateRule_ValidityModeSwitch_NormalizesOldWindowFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var (fixture, admin, channel, show) = await SetupWithShowAsync("programrule-crud-validity-switch", ct);
        using var _ = fixture;
        var service = CreateService(fixture.Db);
        var baseInput = ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id, baseInput with
        {
            Name = "Einmalig",
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 3, 1), To: new DateOnly(2026, 6, 30))
        }, ct);

        // Update schaltet den Modus um und verwirft die alten Fensterfelder.
        var updated = await service.UpdateRuleAsync(admin.Id, rule.Id, baseInput with
        {
            Name = "Einmalig",
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: 1, ToDay: 15)
        }, ct);

        Assert.Equal(ProgramRuleValidityMode.MonthlyWindow, updated.ValidityMode);
        Assert.Equal(1, updated.ValidFromDay);
        Assert.Equal(15, updated.ValidToDay);
        Assert.Null(updated.ValidFrom);
        Assert.Null(updated.ValidTo);
    }

    [Fact]
    public async Task GetRulesAsync_InvalidRule_SortsLast()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-sort", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        var baseInput = ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id);

        // Ungültig: Jahresfenster nur am Monat-Tag des lokalen morgigen Tages — heute außerhalb.
        var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Local).AddDays(1));
        var tomorrowMonthDay = new DateOnly(2000, tomorrow.Month, tomorrow.Day);
        await service.CreateRuleAsync(admin.Id, channel.Id, baseInput with
        {
            Name = "A-Ungueltig",
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow,
                From: tomorrowMonthDay, To: tomorrowMonthDay)
        }, ct);
        await service.CreateRuleAsync(admin.Id, channel.Id, baseInput with { Name = "B-Gueltig" }, ct);
        await service.CreateRuleAsync(admin.Id, channel.Id, baseInput with
        {
            Name = "C-Gueltig",
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: 1, ToDay: 31)
        }, ct);

        var rules = await service.GetRulesAsync(admin.Id, channel.Id, ct);

        Assert.Equal(new[] { "B-Gueltig", "C-Gueltig", "A-Ungueltig" }, rules.Select(r => r.Name));
    }

    [Fact]
    public async Task UpdateRule_ChangesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-update", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        fixture.Db.Movies.Add(movie);
        await fixture.Db.SaveChangesAsync(ct);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        var updated = await service.UpdateRuleAsync(admin.Id, rule.Id,
            new ProgramRuleInput("Neu", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Once,
                null, new DateOnly(2026, 12, 24), new TimeOnly(20, 15), null, TimeSpan.FromMinutes(120),
                ProgramRuleMaterialType.Movie, movie.Id, 9, false), ct);

        Assert.Equal("Neu", updated.Name);
        Assert.Equal(ProgramRuleType.MoviePrimeTime, updated.RuleType);
        Assert.Equal(ProgramRuleRecurrence.Once, updated.Recurrence);
        Assert.Null(updated.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 12, 24), updated.SingleDate);
        Assert.Equal(TimeSpan.FromMinutes(120), updated.SlotDuration);
        Assert.Equal(ProgramRuleMaterialType.Movie, updated.MaterialType);
        Assert.Equal(movie.Id, updated.MaterialId);
        Assert.Equal(9, updated.Priority);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task SetRuleActive_TogglesFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-toggle", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await service.SetRuleActiveAsync(admin.Id, rule.Id, false, ct);
        Assert.False((await fixture.Db.ProgramRules.SingleAsync(ct)).IsActive);

        await service.SetRuleActiveAsync(admin.Id, rule.Id, true, ct);
        Assert.True((await fixture.Db.ProgramRules.SingleAsync(ct)).IsActive);
    }

    [Fact]
    public async Task DeleteRule_RemovesRule()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-delete", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await service.DeleteRuleAsync(admin.Id, rule.Id, ct);

        Assert.Equal(0, await fixture.Db.ProgramRules.CountAsync(ct));
    }

    [Fact]
    public async Task CreateRule_DuplicateNameSameChannel_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-unique", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CreateRuleAsync(admin.Id, channel.Id,
                ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct));
    }
}
