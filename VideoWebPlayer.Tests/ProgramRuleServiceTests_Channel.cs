using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Channel
{
    private static ProgramRuleService CreateService(ApplicationDbContext db) => new(db);

    [Fact]
    public async Task GetOrCreateChannel_FirstCall_CreatesSingleChannel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-channel-create", ct);
        var service = CreateService(fixture.Db);

        var channel = await service.GetOrCreateChannelAsync(ct);
        var again = await service.GetOrCreateChannelAsync(ct);

        Assert.Equal(channel.Id, again.Id);
        Assert.Equal("TV-Sender", channel.Name);
        Assert.Equal(1, await fixture.Db.TVChannels.CountAsync(ct));
    }

    [Fact]
    public async Task RenameChannel_PersistsNewName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-channel-rename", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);

        var channel = await service.RenameChannelAsync(admin.Id, "Hauptsender", ct);

        Assert.Equal("Hauptsender", channel.Name);
        Assert.Equal("Hauptsender", (await fixture.Db.TVChannels.SingleAsync(ct)).Name);
    }

    [Fact]
    public async Task DeleteChannel_CascadesRules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-channel-cascade", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);

        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        await service.CreateRuleAsync(admin.Id, channel.Id, RuleInput(), ct);
        Assert.Equal(1, await fixture.Db.ProgramRules.CountAsync(ct));

        fixture.Db.TVChannels.Remove(channel);
        await fixture.Db.SaveChangesAsync(ct);

        Assert.Equal(0, await fixture.Db.ProgramRules.CountAsync(ct));
    }

    internal static ProgramRuleInput RuleInput(ProgramRuleMaterialType materialType = ProgramRuleMaterialType.TVShow, long materialId = 1)
        => new("Regel", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, materialType, materialId, 0, true);
}
