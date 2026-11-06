using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_ClipSource
{
    private static ProgramRuleInput ClipSourceInput(ProgramRuleType type, long sourceId)
        => new("Clip-Regel", type, ProgramRuleRecurrence.Daily, null, null, new TimeOnly(0, 0),
            null, TimeSpan.FromMinutes(30), ProgramRuleMaterialType.MediaSourceClips, sourceId, 0, true);

    [Fact]
    public async Task GetAllowedMaterialTypes_Filler_IncludesClipSource_AndRepeatExcludesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-clip-allowed", ct);
        var service = new ProgramRuleService(fixture.Db);

        Assert.Contains(ProgramRuleMaterialType.MediaSourceClips, service.GetAllowedMaterialTypes(ProgramRuleType.Filler));
        Assert.DoesNotContain(ProgramRuleMaterialType.MediaSourceClips, service.GetAllowedMaterialTypes(ProgramRuleType.Repeat));
        Assert.DoesNotContain(ProgramRuleMaterialType.MediaSourceClips, service.GetAllowedMaterialTypes(ProgramRuleType.HolidaySpecial));
    }

    [Fact]
    public async Task CreateRule_ClipSourceMaterial_ForFiller_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-clip-create", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var source = await fixture.EnsureSourceAsync(1);
        var service = new ProgramRuleService(fixture.Db);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id, ClipSourceInput(ProgramRuleType.Filler, source.Id), ct);

        Assert.Equal(ProgramRuleMaterialType.MediaSourceClips, rule.MaterialType);
        Assert.Equal(source.Id, rule.MaterialId);
    }

    [Fact]
    public async Task CreateRule_ClipSourceMaterial_ForSeriesFixedTime_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-clip-rejected", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var source = await fixture.EnsureSourceAsync(1);
        var service = new ProgramRuleService(fixture.Db);

        var input = ClipSourceInput(ProgramRuleType.SeriesFixedTime, source.Id) with { EpisodeCount = 1 };
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateRuleAsync(admin.Id, channel.Id, input, ct));
    }

    [Fact]
    public async Task CreateRule_ClipSourceMaterial_UnknownSource_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("rule-clip-unknown", ct);
        var admin = await fixture.AddUserAsync("admin@x.y", isAdmin: true);
        var channel = await fixture.GetChannelAsync(ct);
        var service = new ProgramRuleService(fixture.Db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateRuleAsync(admin.Id, channel.Id, ClipSourceInput(ProgramRuleType.Filler, 999), ct));
    }
}
