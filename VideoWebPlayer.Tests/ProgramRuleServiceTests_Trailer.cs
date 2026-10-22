using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Trailer
{
    private static ProgramRuleInput TrailerInput(ProgramRuleType type)
        => new("Trailer-Regel", type, ProgramRuleRecurrence.Daily, null, null, new TimeOnly(0, 0),
            null, TimeSpan.FromMinutes(30), ProgramRuleMaterialType.Trailer, 0, 0, true);

    [Fact]
    public async Task GetAllowedMaterialTypes_Filler_IncludesTrailer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-trailer-allowed", ct);
        var service = new ProgramRuleService(fixture.Db);

        Assert.Contains(ProgramRuleMaterialType.Trailer, service.GetAllowedMaterialTypes(ProgramRuleType.Filler));
    }

    [Fact]
    public async Task CreateRule_TrailerMaterial_ForFiller_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-trailer-create", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var service = new ProgramRuleService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id, TrailerInput(ProgramRuleType.Filler), ct);

        Assert.Equal(ProgramRuleMaterialType.Trailer, rule.MaterialType);
        Assert.Equal(0, rule.MaterialId);
    }

    [Fact]
    public async Task CreateRule_TrailerMaterial_ForSeriesFixedTime_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-trailer-rejected", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var service = new ProgramRuleService(fixture.Db);

        var input = TrailerInput(ProgramRuleType.SeriesFixedTime) with { EpisodeCount = 1 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateRuleAsync(admin.Id, channel.Id, input, ct));
    }
}
