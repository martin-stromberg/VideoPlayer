using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Services.PlaylistService.GetPlaylistEntriesAsync"/>.
/// </summary>
public class PlaylistServiceTests_GetEntries : PlaylistServiceTestBase
{
    [Fact]
    public async Task GetEntries_ReturnsAllEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Mein Film");
        var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Meine Serie");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.TVShow, showId));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, e => e.MediaType == MediaTypeValues.Movie && e.MediaId == movieId && e.MediaTitle == "Mein Film");
        Assert.Contains(result, e => e.MediaType == MediaTypeValues.TVShow && e.MediaId == showId && e.MediaTitle == "Meine Serie");
    }

    [Fact]
    public async Task GetEntries_EmptyPlaylist_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEntries_NotOwner_ThrowsPlaylistAccessDeniedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.GetPlaylistEntriesAsync(playlistId, _otherUserId, ct));
    }

    [Fact]
    public async Task GetEntries_PlaylistNotFound_ThrowsKeyNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPlaylistEntriesAsync(999999, _testUserId, ct));
    }

    [Fact]
    public async Task GetEntries_RemovesOrphanedEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Vorhandener Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, movieId),
            (MediaTypeValues.Movie, 999999));

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.Single(result);
        Assert.Equal(movieId, result[0].MediaId);
    }

    [Fact]
    public async Task GetEntries_OrphanedEntry_DeletedFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, 999999));

        await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        Assert.False(await _db.PlaylistEntries.AsNoTracking()
            .AnyAsync(e => e.PlaylistId == playlistId && e.MediaType == MediaTypeValues.Movie && e.MediaId == 999999, ct));
    }

    [Fact]
    public async Task GetEntries_CascadedEntry_ParentMediaTitleIsLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        var show = await Helpers.TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null) }));
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShow, show.Id, ct);

        var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, ct);

        var seasonEntry = Assert.Single(result, e => e.MediaType == MediaTypeValues.TVShowSeason);
        Assert.Equal(MediaTypeValues.TVShow, seasonEntry.ParentMediaType);
        Assert.Equal(show.Id, seasonEntry.ParentMediaId);
        Assert.Equal(show.Name, seasonEntry.ParentMediaTitle);

        var episodeEntry = Assert.Single(result, e => e.MediaType == MediaTypeValues.TVShowEpisode);
        Assert.Equal(MediaTypeValues.TVShow, episodeEntry.ParentMediaType);
        Assert.Equal(show.Id, episodeEntry.ParentMediaId);
        Assert.Equal(show.Name, episodeEntry.ParentMediaTitle);
    }
}
