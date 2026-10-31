using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Validation
{
    private static async Task<(ProgramRuleService Service, string AdminId, long ChannelId)> SetupAsync(
        PairingTestDb fixture, CancellationToken ct)
    {
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = new ProgramRuleService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        return (service, admin.Id, channel.Id);
    }

    private static ProgramRuleInput Base(ProgramRuleMaterialType type = ProgramRuleMaterialType.TVShow, long materialId = 1)
        => new("Regel", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, type, materialId, 0, true);

    private static async Task<long> SeedShowAsync(PairingTestDb fixture, CancellationToken ct)
    {
        var source = new MediaSource { Name = "Quelle" };
        fixture.Db.MediaSources.Add(source);
        await fixture.Db.SaveChangesAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        return show.Id;
    }

    [Fact]
    public async Task CreateRule_WeeklyWithoutDayOfWeek_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-weekly", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Recurrence = ProgramRuleRecurrence.Weekly };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_OnceWithoutDate_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-once", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Recurrence = ProgramRuleRecurrence.Once };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_WeekdayWithoutDayOfWeekOrDate_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-weekday", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);
        var showId = await SeedShowAsync(fixture, ct);

        var rule = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with { Recurrence = ProgramRuleRecurrence.Weekday }, ct);

        Assert.Equal(ProgramRuleRecurrence.Weekday, rule.Recurrence);
        Assert.Null(rule.DayOfWeek);
        Assert.Null(rule.SingleDate);
    }

    [Fact]
    public async Task CreateRule_WeekendWithoutDayOfWeekOrDate_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-weekend", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);
        var showId = await SeedShowAsync(fixture, ct);

        var rule = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with { Recurrence = ProgramRuleRecurrence.Weekend }, ct);

        Assert.Equal(ProgramRuleRecurrence.Weekend, rule.Recurrence);
        Assert.Null(rule.DayOfWeek);
        Assert.Null(rule.SingleDate);
    }

    [Fact]
    public async Task CreateRule_WithoutEpisodeCountAndDuration_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-length", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { EpisodeCount = null };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_SeriesFixedTime_RequiresEpisodeCount()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-seriescount", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { EpisodeCount = null, SlotDuration = TimeSpan.FromMinutes(60) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Theory]
    [InlineData(ProgramRuleType.SeriesFixedTime, ProgramRuleMaterialType.Movie)]
    [InlineData(ProgramRuleType.SeriesFixedTime, ProgramRuleMaterialType.Genre)]
    [InlineData(ProgramRuleType.GenreRotation, ProgramRuleMaterialType.TVShow)]
    [InlineData(ProgramRuleType.MoviePrimeTime, ProgramRuleMaterialType.TVShow)]
    [InlineData(ProgramRuleType.Filler, ProgramRuleMaterialType.TVShow)]
    public async Task CreateRule_DisallowedMaterialType_Throws(ProgramRuleType ruleType, ProgramRuleMaterialType materialType)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"programrule-val-matrix-{(int)ruleType}-{(int)materialType}", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { RuleType = ruleType, MaterialType = materialType, EpisodeCount = 1, SlotDuration = TimeSpan.FromMinutes(60) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_OneTimeWithoutBounds_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-onetime-none", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_OneTime_ValidFromAfterValidTo_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-onetime-order", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime,
                From: new DateOnly(2026, 7, 1), To: new DateOnly(2026, 6, 1))
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_OneTime_OpenBound_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-onetime-open", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);
        var showId = await SeedShowAsync(fixture, ct);

        // Nur Von.
        var rule = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with
            {
                Name = "Ab-Datum",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, From: new DateOnly(2026, 3, 1))
            }, ct);
        Assert.Equal(new DateOnly(2026, 3, 1), rule.ValidFrom);
        Assert.Null(rule.ValidTo);

        // Nur Bis.
        var rule2 = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with
            {
                Name = "Bis-Datum",
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.OneTime, To: new DateOnly(2026, 6, 30))
            }, ct);
        Assert.Null(rule2.ValidFrom);
        Assert.Equal(new DateOnly(2026, 6, 30), rule2.ValidTo);
    }

    [Fact]
    public async Task CreateRule_AnnualWithoutBounds_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-annual-none", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));

        var partial = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.AnnualWindow, From: new DateOnly(2000, 3, 1))
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, partial, ct));
    }

    [Fact]
    public async Task CreateRule_UnknownMode_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-unknownmode", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput((ProgramRuleValidityMode)99) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_Monthly_WithoutDays_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-monthly-none", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(32, 15)]
    [InlineData(1, 0)]
    [InlineData(1, 32)]
    public async Task CreateRule_Monthly_InvalidDay_Throws(int fromDay, int toDay)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"programrule-val-monthly-{fromDay}-{toDay}", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.MonthlyWindow, FromDay: fromDay, ToDay: toDay)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_Weekly_EmptyMask_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-weekly-empty", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow, Weekdays: 0) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));

        var missing = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, missing, ct));
    }

    [Fact]
    public async Task CreateRule_Weekly_InvalidMask_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-weekly-invalid", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with { Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow, Weekdays: 128) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));
    }

    [Fact]
    public async Task CreateRule_Holiday_WithoutAnchorOrOffsets_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-holiday-missing", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var noAnchor = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow, HolidayBeforeDays: 1, HolidayAfterDays: 1)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, noAnchor, ct));

        var noOffsets = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow, HolidayAnchor: ProgramRuleHolidayAnchor.EasterSunday)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, noOffsets, ct));
    }

    [Fact]
    public async Task CreateRule_Holiday_OffsetOutOfRange_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-holiday-range", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        var input = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow,
                HolidayAnchor: ProgramRuleHolidayAnchor.EasterSunday, HolidayBeforeDays: 63, HolidayAfterDays: 0)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, input, ct));

        var negative = Base() with
        {
            Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.HolidayWindow,
                HolidayAnchor: ProgramRuleHolidayAnchor.EasterSunday, HolidayBeforeDays: 0, HolidayAfterDays: -1)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(adminId, channelId, negative, ct));
    }

    [Fact]
    public async Task CreateRule_Unrestricted_IgnoresValidityFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-unrestricted-ignore", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);
        var showId = await SeedShowAsync(fixture, ct);

        var rule = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with
            {
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.Unrestricted,
                    From: new DateOnly(2026, 1, 1), FromDay: 5, Weekdays: 127,
                    HolidayAnchor: ProgramRuleHolidayAnchor.NewYear, HolidayBeforeDays: 1, HolidayAfterDays: 1)
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.Unrestricted, rule.ValidityMode);
        Assert.Null(rule.ValidFrom);
        Assert.Null(rule.ValidTo);
        Assert.Null(rule.ValidFromDay);
        Assert.Null(rule.ValidToDay);
        Assert.Null(rule.ValidWeekdays);
        Assert.Null(rule.ValidHolidayAnchor);
        Assert.Null(rule.ValidHolidayBeforeDays);
        Assert.Null(rule.ValidHolidayAfterDays);
    }

    [Fact]
    public async Task CreateRule_NonMatchingFields_NormalizedToNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-normalize", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);
        var showId = await SeedShowAsync(fixture, ct);

        // WeeklyWindow: alle anderen Fensterfelder werden verworfen.
        var rule = await service.CreateRuleAsync(adminId, channelId,
            Base(materialId: showId) with
            {
                Validity = new ProgramRuleValidityInput(ProgramRuleValidityMode.WeeklyWindow,
                    From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 12, 31),
                    FromDay: 1, ToDay: 31, Weekdays: 1 << (int)DayOfWeek.Monday,
                    HolidayAnchor: ProgramRuleHolidayAnchor.NewYear, HolidayBeforeDays: 2, HolidayAfterDays: 3)
            }, ct);

        Assert.Equal(ProgramRuleValidityMode.WeeklyWindow, rule.ValidityMode);
        Assert.Equal(1 << (int)DayOfWeek.Monday, rule.ValidWeekdays);
        Assert.Null(rule.ValidFrom);
        Assert.Null(rule.ValidTo);
        Assert.Null(rule.ValidFromDay);
        Assert.Null(rule.ValidToDay);
        Assert.Null(rule.ValidHolidayAnchor);
        Assert.Null(rule.ValidHolidayBeforeDays);
        Assert.Null(rule.ValidHolidayAfterDays);
    }

    [Fact]
    public async Task CreateRule_MissingMaterial_ThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-missing", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateRuleAsync(adminId, channelId, Base(materialId: 999), ct));
    }

    [Fact]
    public async Task CreateRule_EmptyName_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-val-name", ct);
        var (service, adminId, channelId) = await SetupAsync(fixture, ct);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRuleAsync(adminId, channelId, Base() with { Name = "  " }, ct));
    }

    [Fact]
    public void GetAllowedMaterialTypes_ReturnsExpectedSets()
    {
        var service = new ProgramRuleService(null!);

        Assert.Equal([ProgramRuleMaterialType.TVShow], service.GetAllowedMaterialTypes(ProgramRuleType.SeriesFixedTime));
        Assert.Equal([ProgramRuleMaterialType.Genre], service.GetAllowedMaterialTypes(ProgramRuleType.GenreRotation));
        Assert.Equal(
            [ProgramRuleMaterialType.Movie, ProgramRuleMaterialType.MovieCollection],
            service.GetAllowedMaterialTypes(ProgramRuleType.MoviePrimeTime));
        var repeat = service.GetAllowedMaterialTypes(ProgramRuleType.Repeat);
        var holiday = service.GetAllowedMaterialTypes(ProgramRuleType.HolidaySpecial);
        Assert.DoesNotContain(ProgramRuleMaterialType.Trailer, repeat);
        Assert.DoesNotContain(ProgramRuleMaterialType.Trailer, holiday);
        Assert.DoesNotContain(ProgramRuleMaterialType.TVShow, service.GetAllowedMaterialTypes(ProgramRuleType.Filler));
        Assert.Contains(ProgramRuleMaterialType.Trailer, service.GetAllowedMaterialTypes(ProgramRuleType.Filler));
    }
}
