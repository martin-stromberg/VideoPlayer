using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Authorization
{
    private static ProgramRuleInput ValidInput(long materialId)
        => new("Regel", ProgramRuleType.SeriesFixedTime, ProgramRuleRecurrence.Daily, null, null,
            new TimeOnly(20, 0), 1, null, ProgramRuleMaterialType.TVShow, materialId, 0, true);

    [Fact]
    public async Task Mutations_NonAdmin_ThrowForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-auth-nonadmin", ct);
        var user = await fixture.CreateUserAsync("user@test.com", isAdmin: false);
        var service = new ProgramRuleService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RenameChannelAsync(user.Id, "X", ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetRulesAsync(user.Id, channel.Id, ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateRuleAsync(user.Id, channel.Id, ValidInput(1), ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateRuleAsync(user.Id, 1, ValidInput(1), ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.SetRuleActiveAsync(user.Id, 1, false, ct));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.DeleteRuleAsync(user.Id, 1, ct));
    }

    [Fact]
    public async Task Mutations_UnknownUser_ThrowUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-auth-unknown", ct);
        var service = new ProgramRuleService(fixture.Db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RenameChannelAsync("nobody", "X", ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateRuleAsync("nobody", 1, ValidInput(1), ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteRuleAsync("nobody", 1, ct));
    }
}
