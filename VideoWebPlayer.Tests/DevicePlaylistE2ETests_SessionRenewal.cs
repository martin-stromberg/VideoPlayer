using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A6, Teil 3: die Sitzung eines gekoppelten Geräts — Erneuerung mit Rotation während laufender
/// Playlist-Nutzung, der abgewiesene zweite Gebrauch eines bereits rotierten Erneuerungsnachweises und der
/// Widerruf des Geräts: er sperrt die Erneuerung sofort, lässt die schon ausgestellte Sitzung aber bis zu
/// ihrem Ablauf (12 Stunden) weiterarbeiten.
/// </summary>
[Trait("Category", "E2E")]
public sealed class DevicePlaylistE2ETests_SessionRenewal : DeviceClientTestBase
{
    [Fact]
    public async Task PairedDevice_RenewsSessionWhilePlaylistIsInUse_ContinuesWithTheNewSession()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-erneuerung-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, secondEntryId) =
            await CreatePlaylistWithTwoMoviesAsync("Erneuerungs-Playlist", firstMovieId, secondMovieId);

        var start = await Client.StartPlaylistAsync(playlistId, entryId: null);
        Assert.Equal(firstEntryId, start.CurrentEntryId);

        var renewed = await Client.RefreshAsync();

        Assert.NotEqual(payload.RefreshToken, renewed.RefreshToken);
        Assert.Equal(renewed.Token, Client.AuthorizationToken);

        // Die laufende Playlist-Nutzung geht mit der erneuerten Sitzung weiter.
        Assert.Contains(await Client.RequestPlaylistsAsync(), p => p.Id == playlistId);
        var next = await Client.GetNextPlaylistEntryAsync(playlistId, firstEntryId);
        Assert.NotNull(next);
        Assert.Equal(secondEntryId, next!.Entry.Id);
    }

    [Fact]
    public async Task PairedDevice_UsesAlreadyRotatedRefreshToken_IsRefused()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"geraet-wiederverwendung-{Guid.NewGuid():N}@test.com");
        await Client.RefreshAsync();

        var response = await PostRefreshAsync(payload.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RevokedDevice_RenewalIsRefused_ButRunningPlaylistSessionKeepsWorking()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-widerruf-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, _) =
            await CreatePlaylistWithTwoMoviesAsync("Widerruf-Playlist", firstMovieId, secondMovieId);

        // Die ausgestellte Sitzung läuft zwölf Stunden - genau das Fenster, das der Widerruf bewusst offen lässt.
        var sessionExpiry = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token).ValidTo;
        Assert.InRange(sessionExpiry, DateTime.UtcNow.AddHours(11), DateTime.UtcNow.AddHours(13));

        await RevokeDeviceAsync(payload.DeviceToken);

        // 1. Die Erneuerung ist sofort gesperrt - auch mit gültigem Zugangsschlüssel der App.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(payload.RefreshToken)).StatusCode);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());
        Assert.Contains("widerrufen", failure.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Die bereits laufende Sitzung arbeitet bis zu ihrem Ablauf weiter.
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.Token);

        var listResponse = await http.GetAsync("/api/playlists", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var playlists = await listResponse.Content.ReadFromJsonAsync<DtoPlaylist[]>(ct);
        Assert.Contains(playlists!, p => p.Id == playlistId);

        var playResponse = await http.PostAsync($"/api/playlists/{playlistId}/play", content: null, ct);
        Assert.Equal(HttpStatusCode.OK, playResponse.StatusCode);
        var playback = await playResponse.Content.ReadFromJsonAsync<DtoPlaylistPlaybackStart>(ct);
        Assert.Equal(firstEntryId, playback!.CurrentEntryId);
    }

    [Fact]
    public async Task RevokedDevice_DeviceTokenIsNoLongerAcceptedAsApiKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, payload) = await CreateUserAndPairDeviceAsync($"geraet-schluessel-{Guid.NewGuid():N}@test.com");
        await RevokeDeviceAsync(payload.DeviceToken);

        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Add("X-API-Key", payload.DeviceToken);
        var response = await http.PostAsJsonAsync(
            "/api/auth/logout", new RefreshTokenRequest { RefreshToken = payload.RefreshToken }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Asks for a session renewal with the configured app key instead of the device token, so the answer
    /// reflects the state of the refresh token itself and not just the <c>X-API-Key</c> gate.
    /// </summary>
    /// <param name="refreshToken">The refresh token to present.</param>
    /// <returns>The server's answer.</returns>
    private async Task<HttpResponseMessage> PostRefreshAsync(string refreshToken)
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Add("X-API-Key", PairingWebApplicationFactory.MauiApiToken);
        return await http.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken }, ct);
    }
}
