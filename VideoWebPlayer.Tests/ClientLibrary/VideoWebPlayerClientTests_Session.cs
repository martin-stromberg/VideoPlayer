using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// Session handling of the client library (A5): renewing a device session, the unmistakable error after
/// the device was revoked, and ending the session with logout.
/// </summary>
public sealed class VideoWebPlayerClientTests_Session : DeviceClientTestBase
{
    [Fact]
    public async Task RefreshAsync_WithValidRefreshToken_AppliesNewSessionAndRotatesToken()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"refresh-ok-{Guid.NewGuid():N}@test.com");

        var refreshed = await Client.RefreshAsync();

        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        Assert.NotEqual(payload.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(refreshed.Token, Client.AuthorizationToken);
        Assert.Equal(refreshed.RefreshToken, Client.DeviceRefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_WithAlreadyUsedRefreshToken_ThrowsInvalidOperation()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"refresh-used-{Guid.NewGuid():N}@test.com");
        await Client.RefreshAsync();

        // Present the rotated-away token again: the server refuses it (single use).
        Client.DeviceRefreshToken = payload.RefreshToken;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());

        Assert.Contains("neu koppeln", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_WithoutRefreshToken_ThrowsInvalidOperation()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());

        Assert.Contains("neu koppeln", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_AfterDeviceRevocation_ThrowsInvalidOperationWithClearMessage()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"refresh-revoked-{Guid.NewGuid():N}@test.com");

        await RevokeDeviceAsync(payload.DeviceToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());

        Assert.Contains("widerrufen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutAsync_ClearsTokens_AndRevokesSessionOnServer()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"logout-{Guid.NewGuid():N}@test.com");

        await Client.LogoutAsync();

        Assert.Null(Client.DeviceToken);
        Assert.Null(Client.DeviceRefreshToken);
        Assert.Null(Client.AuthorizationToken);

        // The revoked refresh token is no longer accepted.
        Client.DeviceToken = payload.DeviceToken;
        Client.DeviceRefreshToken = payload.RefreshToken;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());
    }
}
