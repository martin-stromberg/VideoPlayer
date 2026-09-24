using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Services.Security;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task IssueAsync_StoresHashNotPlaintext()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-issue", ct);
        var service = fixture.CreateRefreshTokenService();
        var device = await fixture.CreateDeviceTokenService().IssueAsync("Test-TV", null, ct);

        var issued = await service.IssueAsync("user-id", device.DeviceId, ct);

        Assert.False(string.IsNullOrWhiteSpace(issued.Token));
        Assert.True(issued.Token.Length >= 32);
        var stored = Assert.Single(await fixture.Db.RefreshTokens.ToListAsync(ct));
        Assert.Equal("user-id", stored.UserId);
        Assert.Equal(device.DeviceId, stored.DeviceId);
        Assert.Equal(HashHelper.Sha256Hex(issued.Token), stored.TokenHash);
        Assert.NotEqual(issued.Token, stored.TokenHash);
        Assert.DoesNotContain(issued.Token, stored.TokenHash, StringComparison.Ordinal);
        Assert.Null(stored.RevokedAtUtc);
        Assert.Null(stored.ReplacedByHash);
        Assert.True(stored.ExpiresAtUtc > DateTime.UtcNow.AddDays(25));
    }

    [Fact]
    public async Task RotateAsync_ValidToken_ReturnsRotatedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate", ct);
        var service = fixture.CreateRefreshTokenService();
        var device = await fixture.CreateDeviceTokenService().IssueAsync("Test-TV", null, ct);
        var issued = await service.IssueAsync("user-id", device.DeviceId, ct);

        var rotated = await service.RotateAsync(issued.Token, ct);

        Assert.True(rotated.Success);
        Assert.False(string.IsNullOrWhiteSpace(rotated.NewToken));
        Assert.NotEqual(issued.Token, rotated.NewToken);
        Assert.Equal("user-id", rotated.UserId);
        Assert.Equal(device.DeviceId, rotated.DeviceId);

        // ExecuteUpdate schreibt am Change-Tracker vorbei — neu laden.
        fixture.Db.ChangeTracker.Clear();
        var old = await fixture.Db.RefreshTokens.SingleAsync(r => r.TokenHash == HashHelper.Sha256Hex(issued.Token), ct);
        Assert.NotNull(old.RevokedAtUtc);
        Assert.Equal(HashHelper.Sha256Hex(rotated.NewToken!), old.ReplacedByHash);
    }

    [Fact]
    public async Task RotateAsync_RotatedToken_IsSingleUse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate-single-use", ct);
        var service = fixture.CreateRefreshTokenService();
        var device = await fixture.CreateDeviceTokenService().IssueAsync("Test-TV", null, ct);
        var issued = await service.IssueAsync("user-id", device.DeviceId, ct);

        var first = await service.RotateAsync(issued.Token, ct);
        var second = await service.RotateAsync(issued.Token, ct);

        Assert.True(first.Success);
        Assert.False(second.Success);
        // Reuse-Detection: Das erneute Vorlegen des alten Tokens sperrt die
        // komplette Token-Familie — auch der frisch rotierte Token ist ungueltig.
        var chained = await service.RotateAsync(first.NewToken!, ct);
        Assert.False(chained.Success);
    }

    [Fact]
    public async Task RotateAsync_UnknownToken_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate-unknown", ct);
        var service = fixture.CreateRefreshTokenService();

        var rotated = await service.RotateAsync("unbekanntes-token", ct);

        Assert.False(rotated.Success);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RotateAsync_EmptyToken_Fails(string? token)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"refresh-rotate-empty-{token?.Length ?? -1}", ct);
        var service = fixture.CreateRefreshTokenService();

        var rotated = await service.RotateAsync(token!, ct);

        Assert.False(rotated.Success);
    }

    [Fact]
    public async Task RotateAsync_RevokedToken_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate-revoked", ct);
        var service = fixture.CreateRefreshTokenService();
        var device = await fixture.CreateDeviceTokenService().IssueAsync("Test-TV", null, ct);
        var issued = await service.IssueAsync("user-id", device.DeviceId, ct);

        await service.RevokeAsync(issued.Token, ct);
        var rotated = await service.RotateAsync(issued.Token, ct);

        Assert.False(rotated.Success);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.RefreshTokens.SingleAsync(ct);
        Assert.NotNull(stored.RevokedAtUtc);
    }

    [Fact]
    public async Task RotateAsync_ExpiredToken_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate-expired", ct);
        var service = fixture.CreateRefreshTokenService();
        var device = await fixture.CreateDeviceTokenService().IssueAsync("Test-TV", null, ct);
        var issued = await service.IssueAsync("user-id", device.DeviceId, ct);
        var stored = await fixture.Db.RefreshTokens.SingleAsync(ct);
        stored.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync(ct);

        var rotated = await service.RotateAsync(issued.Token, ct);

        Assert.False(rotated.Success);
    }

    [Fact]
    public async Task RevokeAllForDeviceAsync_RevokesOnlyActiveTokensOfThatDevice()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-revoke-device", ct);
        var service = fixture.CreateRefreshTokenService();
        var deviceTokens = fixture.CreateDeviceTokenService();
        var deviceA = await deviceTokens.IssueAsync("TV-Eins", null, ct);
        var deviceB = await deviceTokens.IssueAsync("TV-Zwei", null, ct);
        var tokenA = await service.IssueAsync("user-id", deviceA.DeviceId, ct);
        var tokenB = await service.IssueAsync("user-id", deviceA.DeviceId, ct);
        var otherDevice = await service.IssueAsync("user-id", deviceB.DeviceId, ct);
        var otherUserOtherDevice = await service.IssueAsync("other-user", deviceB.DeviceId, ct);

        await service.RevokeAllForDeviceAsync(deviceA.DeviceId, ct);

        Assert.False((await service.RotateAsync(tokenA.Token, ct)).Success);
        Assert.False((await service.RotateAsync(tokenB.Token, ct)).Success);
        Assert.True((await service.RotateAsync(otherDevice.Token, ct)).Success);
        Assert.True((await service.RotateAsync(otherUserOtherDevice.Token, ct)).Success);
    }

    [Fact]
    public async Task RotateAsync_TokenOfRevokedDevice_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("refresh-rotate-device-revoked", ct);
        var service = fixture.CreateRefreshTokenService();
        var deviceTokens = fixture.CreateDeviceTokenService();
        var issued = await deviceTokens.IssueAsync("Test-TV", null, ct);
        var refresh = await service.IssueAsync("user-id", issued.DeviceId, ct);

        // Geraet direkt als widerrufen markieren (ohne RevokeAllForDeviceAsync,
        // damit nur die Device-Pruefung im RotateAsync greift).
        var device = await fixture.Db.PairedDevices.SingleAsync(ct);
        device.RevokedAtUtc = DateTime.UtcNow;
        await fixture.Db.SaveChangesAsync(ct);

        var rotated = await service.RotateAsync(refresh.Token, ct);

        Assert.False(rotated.Success);
    }
}
