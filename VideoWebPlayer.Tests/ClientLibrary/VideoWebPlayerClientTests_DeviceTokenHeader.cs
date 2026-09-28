using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// The gate key of a paired device (A5): while a device token is set, every call of the library carries
/// it as <c>X-API-Key</c>; without one no such header is sent.
/// </summary>
public sealed class VideoWebPlayerClientTests_DeviceTokenHeader : DeviceClientTestBase
{
    [Fact]
    public async Task DeviceToken_IsSentAsApiKeyOnEveryRequest()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"header-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);

        var (playlistId, firstEntryId, _) = await CreatePlaylistWithTwoMoviesAsync("Kopfzeilen-Playlist", firstMovieId, secondMovieId);
        await Client.RequestPlaylistsAsync();
        await Client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 10, TestContext.Current.CancellationToken);
        await Client.StartPlaylistAsync(playlistId, firstEntryId);
        await Client.DeletePlaylistAsync(playlistId);

        var afterPairing = Requests.Entries.Where(e => e.Path != "/api/pairing/bootstrap").ToArray();
        Assert.NotEmpty(afterPairing);
        Assert.All(afterPairing, entry => Assert.Equal(payload.DeviceToken, entry.ApiKey));
    }

    [Fact]
    public async Task WithoutDeviceToken_NoApiKeyHeaderIsSent()
    {
        var user = await CreateUserAsync($"header-none-{Guid.NewGuid():N}@test.com");
        var ticket = await CreateBootstrapTicketAsync(user.Id);
        var payload = await PairDeviceAsync(ticket);

        Client.DeviceToken = null;
        await Client.RequestPlaylistsAsync();

        var lastRequest = Requests.Entries[^1];
        Assert.Equal("/api/playlists", lastRequest.Path);
        Assert.Null(lastRequest.ApiKey);
        Assert.False(string.IsNullOrWhiteSpace(payload.DeviceToken));
    }

    /// <summary>
    /// The gate key another component configured on the same <see cref="HttpClient"/> (the web interface
    /// does exactly that) must survive: while a device token is set it is replaced for that one request,
    /// and once the device token is gone the foreign key is in use again.
    /// </summary>
    [Fact]
    public async Task ForeignApiKeyOnTheHttpClient_SurvivesTheDeviceToken()
    {
        var recorded = CreateRecordedClient(http => http.DefaultRequestHeaders.Add("X-API-Key", PairingWebApplicationFactory.MauiApiToken));
        var user = await CreateUserAsync($"header-foreign-{Guid.NewGuid():N}@test.com");
        var ticket = await CreateBootstrapTicketAsync(user.Id);

        // Without a device token the foreign gate key is what leaves the client.
        await recorded.Client.PairingBootstrapAsync(new PairingBootstrapRequest
        {
            Ticket = ticket,
            ClientPublicKey = Convert.ToBase64String(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo())
        });
        Assert.Equal(PairingWebApplicationFactory.MauiApiToken, recorded.Requests.Entries[^1].ApiKey);

        recorded.Client.DeviceToken = "geraete-token";
        await recorded.Client.HealthCheckAsync();
        Assert.Equal("geraete-token", recorded.Requests.Entries[^1].ApiKey);

        recorded.Client.DeviceToken = null;
        await recorded.Client.HealthCheckAsync();
        Assert.Equal(PairingWebApplicationFactory.MauiApiToken, recorded.Requests.Entries[^1].ApiKey);
    }

    [Fact]
    public async Task DeviceToken_IsPerClientInstance_AndNotShared()
    {
        var first = await CreateUserAndPairDeviceAsync($"header-a-{Guid.NewGuid():N}@test.com");
        var otherClient = new VideoWebPlayer.Client.VideoWebPlayerClient(
            new HttpClient { BaseAddress = new Uri("http://localhost/") },
            NullLogger<VideoWebPlayer.Client.VideoWebPlayerClient>.Instance);

        otherClient.DeviceToken = "anderes-geraet";

        Assert.Equal(first.Payload.DeviceToken, Client.DeviceToken);
        Assert.Equal("anderes-geraet", otherClient.DeviceToken);
    }
}
