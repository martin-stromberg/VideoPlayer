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
