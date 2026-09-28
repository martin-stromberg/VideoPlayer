using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// A5 acceptance criterion 2 and 4: every playlist call of the library survives a session that expires
/// in between — the three playback navigation calls, the paged read with a cancellation token, and the
/// changing calls with PUT, PATCH and DELETE.
/// </summary>
public sealed class VideoWebPlayerClientTests_PlaylistSessionRenewal : DeviceClientTestBase
{
    private const string RefreshRoute = "/api/auth/refresh";

    [Fact]
    public async Task PlaybackNavigation_SurvivesExpiredSessionBetweenCalls()
    {
        var (playlistId, firstEntryId, secondEntryId) = await ArrangePlaylistAsync("Wiedergabe-Erneuerung");

        ExpireSession();
        var start = await Client.StartPlaylistAsync(playlistId, firstEntryId);
        Assert.Equal(firstEntryId, start.CurrentEntryId);

        ExpireSession();
        var next = await Client.GetNextPlaylistEntryAsync(playlistId, firstEntryId);
        Assert.Equal(secondEntryId, next!.Entry.Id);

        ExpireSession();
        var previous = await Client.GetPreviousPlaylistEntryAsync(playlistId, secondEntryId);
        Assert.Equal(firstEntryId, previous!.Entry.Id);

        ExpireSession();
        var advanced = await Client.AdvancePlaylistAsync(playlistId, firstEntryId);
        Assert.Equal(secondEntryId, advanced!.Entry.Id);

        Assert.Equal(4, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task PagedReadWithCancellationToken_SurvivesExpiredSession()
    {
        var (playlistId, _, _) = await ArrangePlaylistAsync("Seitenabruf-Erneuerung");

        ExpireSession();
        var page = await Client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 10, TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task RenameWithPut_SurvivesExpiredSession()
    {
        var (playlistId, _, _) = await ArrangePlaylistAsync("Umbenennen-Erneuerung");

        ExpireSession();
        var updated = await Client.UpdatePlaylistAsync(playlistId, new DtoUpdatePlaylistRequest { Name = "Umbenannt", Description = "Neu" });

        Assert.Equal("Umbenannt", updated.Name);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task SortModeChangeWithPatch_SurvivesExpiredSession()
    {
        var (playlistId, _, _) = await ArrangePlaylistAsync("Sortierung-Erneuerung");

        ExpireSession();
        var updated = await Client.ChangeSortModeAsync(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = true
        });

        Assert.Equal(PlaylistSortModeValues.Manual, updated.SortMode);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    /// <summary>
    /// Reordering uses the PUT overload without a response body, a third path next to the two covered
    /// above; it must renew the session as well.
    /// </summary>
    [Fact]
    public async Task ReorderWithPut_SurvivesExpiredSession()
    {
        var (playlistId, _, secondEntryId) = await ArrangePlaylistAsync("Umsortieren-Erneuerung");
        await Client.ChangeSortModeAsync(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = true
        });
        var refreshesBefore = Requests.CountTo(RefreshRoute);

        ExpireSession();
        await Client.ReorderPlaylistEntryAsync(playlistId, secondEntryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 1 });

        var page = await Client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 10, TestContext.Current.CancellationToken);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(refreshesBefore + 1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task CoverDeletionWithDelete_SurvivesExpiredSession()
    {
        var (playlistId, _, _) = await ArrangePlaylistAsync("Bild-Erneuerung");

        ExpireSession();
        var result = await Client.DeletePlaylistCoverAsync(playlistId);

        Assert.NotNull(result);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task PlaylistDeletionWithDelete_SurvivesExpiredSession()
    {
        var (playlistId, _, _) = await ArrangePlaylistAsync("Loeschen-Erneuerung");

        ExpireSession();
        await Client.DeletePlaylistAsync(playlistId);

        Assert.Null(await Client.RequestPlaylistAsync(playlistId));
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    /// <summary>
    /// Pairs a device, seeds two playable movies and puts them into a new playlist.
    /// </summary>
    /// <param name="playlistName">The name of the playlist to create.</param>
    /// <returns>The playlist id and the ids of its two entries.</returns>
    /// <!-- Tupel-Elemente: PlaylistId, FirstEntryId, SecondEntryId -->
    private async Task<(long PlaylistId, long FirstEntryId, long SecondEntryId)> ArrangePlaylistAsync(string playlistName)
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"playlist-session-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        return await CreatePlaylistWithTwoMoviesAsync(playlistName, firstMovieId, secondMovieId);
    }
}
