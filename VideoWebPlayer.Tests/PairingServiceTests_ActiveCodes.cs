using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingServiceTests_ActiveCodes
{
    [Fact]
    public async Task GetActiveCodesAsync_ReturnsOnlyActiveCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("pairing-active-codes", ct);
        var service = fixture.CreatePairingService();
        var now = DateTime.UtcNow;

        fixture.Db.PairingCodes.Add(new PairingCode
        {
            CodeHash = "active-hash",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(5),
            CreatedByUserId = "admin-1"
        });
        fixture.Db.PairingCodes.Add(new PairingCode
        {
            CodeHash = "expired-hash",
            CreatedAtUtc = now.AddMinutes(-10),
            ExpiresAtUtc = now.AddMinutes(-5),
            CreatedByUserId = "admin-2"
        });
        fixture.Db.PairingCodes.Add(new PairingCode
        {
            CodeHash = "consumed-hash",
            CreatedAtUtc = now.AddMinutes(-2),
            ExpiresAtUtc = now.AddMinutes(3),
            ConsumedAtUtc = now.AddMinutes(-1),
            CreatedByUserId = "admin-3"
        });
        await fixture.Db.SaveChangesAsync(ct);

        var active = await service.GetActiveCodesAsync(ct);

        var info = Assert.Single(active);
        Assert.Equal("admin-1", info.CreatedByUserId);
        Assert.Equal(now.AddMinutes(5), info.ExpiresAtUtc, TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(typeof(VideoWebPlayer.Services.PairingCodeInfo).GetProperties(), p => p.Name.Contains("Hash") || p.Name == "Code");
    }
}
