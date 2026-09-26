using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// A5 acceptance criteria 1 and 3, driven end to end through the library only: pair a device via the QR
/// bootstrap, list playlists, start playback, report progress and move on — and, after the device was
/// revoked, fail with one unmistakable message instead of retrying forever.
/// </summary>
public sealed class VideoWebPlayerClientTests_DeviceRunEndToEnd : DeviceClientTestBase
{
    [Fact]
    public async Task PairedDevice_ListsPlaylists_StartsPlayback_ReportsProgressAndAdvances()
    {
        var user = await CreateUserAsync($"geraetelauf-{Guid.NewGuid():N}@test.com");
        var ticket = await CreateBootstrapTicketAsync(user.Id);

        var payload = await PairDeviceAsync(ticket, "Wohnzimmer-Fernseher");
        Assert.Equal(payload.DeviceToken, Client.DeviceToken);
        Assert.Equal(payload.Token, Client.AuthorizationToken);

        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, secondEntryId) = await CreatePlaylistWithTwoMoviesAsync("Geräte-Playlist", firstMovieId, secondMovieId);

        var playlists = await Client.RequestPlaylistsAsync();
        Assert.Contains(playlists, p => p.Id == playlistId);

        var start = await Client.StartPlaylistAsync(playlistId, entryId: null);
        Assert.Equal(firstEntryId, start.CurrentEntryId);
        Assert.Equal($"/api/items/movie/{firstMovieId}/stream", start.StreamUrl);

        // ReportPlaybackProgressAsync throws on any non-success answer, so reaching the next line means
        // the server accepted the report (it is written to the database by the buffered worker afterwards).
        await Client.ReportPlaybackProgressAsync(MediaTypeValues.Movie.ToLowerInvariant(), firstMovieId, 120, 3600);
        Assert.Equal(1, Requests.CountTo("/api/continue-watching/progress"));

        var next = await Client.GetNextPlaylistEntryAsync(playlistId, firstEntryId);
        Assert.Equal(secondEntryId, next!.Entry.Id);

        await Client.LogoutAsync();
        Assert.Null(Client.DeviceToken);
    }

    [Fact]
    public async Task RevokedDevice_ReportsOneClearErrorOnNextRenewal()
    {
        var (_, payload) = await CreateUserAndPairDeviceAsync($"widerruf-{Guid.NewGuid():N}@test.com");

        await RevokeDeviceAsync(payload.DeviceToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Client.RefreshAsync());

        Assert.Contains("widerrufen", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neu koppeln", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, Requests.CountTo("/api/auth/refresh"));
    }
}
