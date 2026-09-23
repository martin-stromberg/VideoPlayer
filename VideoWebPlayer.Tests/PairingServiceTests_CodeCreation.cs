using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Services.Security;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingServiceTests_CodeCreation
{
    [Fact]
    public async Task CreatePairingCodeAsync_ReturnsCodeAndPersistsHash()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-create-code", ct);
        var service = fixture.CreatePairingService();

        var before = DateTime.UtcNow;
        var created = await service.CreatePairingCodeAsync("admin-user-id", ct);

        var code = created.Code;
        Assert.Equal(8, code.Length);
        Assert.All(code, c => Assert.Contains(c, "ABCDEFGHJKMNPQRSTUVWXYZ23456789"));

        var stored = Assert.Single(await fixture.Db.PairingCodes.ToListAsync(ct));
        Assert.Equal(HashHelper.Sha256Hex(code), stored.CodeHash);
        Assert.NotEqual(code, stored.CodeHash);
        Assert.DoesNotContain(code, stored.CodeHash, StringComparison.Ordinal);
        Assert.Equal("admin-user-id", stored.CreatedByUserId);
        Assert.Null(stored.ConsumedAtUtc);
        Assert.True(stored.ExpiresAtUtc > before.AddMinutes(4));
        Assert.True(stored.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(6));
        Assert.Equal(stored.ExpiresAtUtc, created.ExpiresAtUtc);
        Assert.Equal(stored.CreatedAtUtc, created.CreatedAtUtc);
    }
}
