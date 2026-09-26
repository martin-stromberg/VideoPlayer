using Microsoft.Extensions.Logging.Abstractions;
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
