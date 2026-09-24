using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Security;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DeviceTokenServiceTests
{
    [Fact]
    public async Task IssueAsync_StoresHashNotPlaintext()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-issue", ct);
        var service = fixture.CreateDeviceTokenService();

        var token = (await service.IssueAsync("Test-TV", "admin-user-id", ct)).Token;

        Assert.False(string.IsNullOrWhiteSpace(token));
        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Equal("Test-TV", device.Name);
        Assert.Equal(HashHelper.Sha256Hex(token), device.TokenHash);
        Assert.NotEqual(token, device.TokenHash);
        Assert.DoesNotContain(token, device.TokenHash, StringComparison.Ordinal);
        Assert.Equal("admin-user-id", device.CreatedByUserId);
        Assert.Null(device.LastUsedAtUtc);
        Assert.Null(device.RevokedAtUtc);
        Assert.True(device.IssuedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task IssueAsync_EmptyDeviceName_UsesDefaultName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-default-name", ct);
        var service = fixture.CreateDeviceTokenService();

        await service.IssueAsync(null, null, ct);

        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        // Der Fallback-Name enthaelt den Ausstellungszeitpunkt, damit mehrere
        // namenlose Geraete in der Admin-Liste unterscheidbar bleiben.
        Assert.Matches(@"^Geraet vom \d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$", device.Name);
    }

    [Fact]
    public async Task IssueAsync_WhitespaceDeviceName_UsesDefaultName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-whitespace-name", ct);
        var service = fixture.CreateDeviceTokenService();

        await service.IssueAsync("   ", null, ct);

        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.Matches(@"^Geraet vom \d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$", device.Name);
    }

    [Fact]
    public async Task IsValidDeviceTokenAsync_ValidToken_SucceedsAndUpdatesLastUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-valid", ct);
        var service = fixture.CreateDeviceTokenService();
        var token = (await service.IssueAsync("Test-TV", null, ct)).Token;

        var valid = await service.IsValidDeviceTokenAsync(token, ct);

        Assert.True(valid);
        var device = Assert.Single(await fixture.Db.PairedDevices.ToListAsync(ct));
        Assert.NotNull(device.LastUsedAtUtc);
    }

    [Fact]
    public async Task IsValidDeviceTokenAsync_RevokedToken_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-revoked", ct);
        var service = fixture.CreateDeviceTokenService();
        var token = (await service.IssueAsync("Test-TV", null, ct)).Token;
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;
        await service.RevokeAsync(deviceId, ct);

        var valid = await service.IsValidDeviceTokenAsync(token, ct);

        Assert.False(valid);
    }

    [Fact]
    public async Task RevokeAsync_SetsRevokedAtUtc()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-revoke", ct);
        var service = fixture.CreateDeviceTokenService();
        await service.IssueAsync("Test-TV", null, ct);
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;

        var revoked = await service.RevokeAsync(deviceId, ct);

        Assert.True(revoked);
        var device = await fixture.Db.PairedDevices.SingleAsync(ct);
        Assert.NotNull(device.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeAsync_RevokesDeviceRefreshTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-revoke-refresh", ct);
        var service = fixture.CreateDeviceTokenService();
        var refreshTokens = fixture.CreateRefreshTokenService();
        var issued = await service.IssueAsync("Test-TV", null, ct);
        var refresh = await refreshTokens.IssueAsync("user-id", issued.DeviceId, ct);
        var otherDevice = await service.IssueAsync("Anderes-Geraet", null, ct);
        var otherRefresh = await refreshTokens.IssueAsync("user-id", otherDevice.DeviceId, ct);

        var revoked = await service.RevokeAsync(issued.DeviceId, ct);

        Assert.True(revoked);
        // Aktive Refresh-Tokens des widerrufenen Geraets sind gesperrt,
        // Tokens anderer Geraete bleiben nutzbar.
        Assert.False((await refreshTokens.RotateAsync(refresh.Token, ct)).Success);
        Assert.True((await refreshTokens.RotateAsync(otherRefresh.Token, ct)).Success);
    }

    [Fact]
    public async Task RenameAsync_UpdatesName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-rename", ct);
        var service = fixture.CreateDeviceTokenService();
        await service.IssueAsync("Geraet", null, ct);
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;

        var renamed = await service.RenameAsync(deviceId, "  TV Wohnzimmer  ", ct);

        Assert.True(renamed);
        var device = await fixture.Db.PairedDevices.SingleAsync(ct);
        Assert.Equal("TV Wohnzimmer", device.Name);
    }

    [Fact]
    public async Task RenameAsync_UnknownDevice_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-rename-unknown", ct);
        var service = fixture.CreateDeviceTokenService();

        var renamed = await service.RenameAsync(42, "TV Wohnzimmer", ct);

        Assert.False(renamed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameAsync_EmptyName_ReturnsFalse(string newName)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync($"device-token-rename-empty-{newName.Length}", ct);
        var service = fixture.CreateDeviceTokenService();
        await service.IssueAsync("Geraet", null, ct);
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;

        var renamed = await service.RenameAsync(deviceId, newName, ct);

        Assert.False(renamed);
        Assert.Equal("Geraet", (await fixture.Db.PairedDevices.SingleAsync(ct)).Name);
    }

    [Fact]
    public async Task RenameAsync_TooLongName_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-rename-long", ct);
        var service = fixture.CreateDeviceTokenService();
        await service.IssueAsync("Geraet", null, ct);
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;

        var renamed = await service.RenameAsync(deviceId, new string('x', 201), ct);

        Assert.False(renamed);
        Assert.Equal("Geraet", (await fixture.Db.PairedDevices.SingleAsync(ct)).Name);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("device-token-list", ct);
        var service = fixture.CreateDeviceTokenService();
        await service.IssueAsync("TV Eins", null, ct);
        await service.IssueAsync("TV Zwei", null, ct);

        var devices = await service.GetDevicesAsync(ct);

        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.Name == "TV Eins");
        Assert.Contains(devices, d => d.Name == "TV Zwei");
    }
}
