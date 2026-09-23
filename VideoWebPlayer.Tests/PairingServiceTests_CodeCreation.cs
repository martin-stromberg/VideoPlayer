using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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

    [Fact]
    public async Task CreatePairingCodeAsync_ConfiguredCodeLengthZero_UsesMinimumLength()
    {
        // Regressionstest zu continue.md: Pairing:CodeLength=0 erzeugte einen
        // leeren Code. Die Konfiguration muss auf eine Mindestlaenge geklemmt werden.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-code-length-zero", ct);
        var service = CreatePairingServiceWithConfig(fixture.Db, new Dictionary<string, string?>
        {
            ["Pairing:CodeLength"] = "0"
        });

        var created = await service.CreatePairingCodeAsync("admin-user-id", ct);

        Assert.Equal(4, created.Code.Length);
        Assert.All(created.Code, c => Assert.Contains(c, "ABCDEFGHJKMNPQRSTUVWXYZ23456789"));
    }

    [Fact]
    public async Task CreatePairingCodeAsync_ConfiguredCodeLengthNegative_DoesNotThrow()
    {
        // Regressionstest zu continue.md: Pairing:CodeLength=-5 warf eine
        // ArgumentOutOfRangeException (HTTP 500 statt eines gueltigen Codes).
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-code-length-negative", ct);
        var service = CreatePairingServiceWithConfig(fixture.Db, new Dictionary<string, string?>
        {
            ["Pairing:CodeLength"] = "-5"
        });

        var created = await service.CreatePairingCodeAsync("admin-user-id", ct);

        Assert.Equal(4, created.Code.Length);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    public async Task CreatePairingCodeAsync_ConfiguredTtlNotPositive_ExpiresInFuture(string ttl)
    {
        // Regressionstest zu continue.md: Pairing:CodeTtlMinutes<=0 erzeugte einen
        // sofort (oder bereits in der Vergangenheit) abgelaufenen Code.
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"pairing-code-ttl-{ttl.TrimStart('-')}", ct);
        var service = CreatePairingServiceWithConfig(fixture.Db, new Dictionary<string, string?>
        {
            ["Pairing:CodeTtlMinutes"] = ttl
        });

        var created = await service.CreatePairingCodeAsync("admin-user-id", ct);

        Assert.True(created.ExpiresAtUtc > created.CreatedAtUtc);
    }

    private static PairingService CreatePairingServiceWithConfig(ApplicationDbContext db, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new PairingService(db, new DeviceTokenService(db), configuration);
    }
}
