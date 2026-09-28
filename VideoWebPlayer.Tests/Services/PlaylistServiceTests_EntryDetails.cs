using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the additional entry information (release date, plot, episode number) that
/// <see cref="VideoWebPlayer.Services.PlaylistService"/> delivers with every <see cref="DtoPlaylistEntry"/> for the
/// header of the playlist detail page (selected title), and for the side-effect-free cover preview.
/// </summary>
public class PlaylistServiceTests_EntryDetails : PlaylistServiceTestBase
{
    [Fact]
    public async Task GetEntriesPaged_Movie_ReturnsReleaseDateAndPlot()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow, ReleaseDate = new DateTime(2019, 5, 17), Plot = "Eine Handlung." };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));

        var page = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        var entry = Assert.Single(page.Entries);
        Assert.Equal(new DateTime(2019, 5, 17), entry.ReleaseDate);
        Assert.Equal("Eine Handlung.", entry.Plot);
        Assert.Null(entry.EpisodeNumber);
    }

    [Fact]
    public async Task GetEntriesPaged_Episode_ReturnsEpisodeNumberPlotAndFallbackReleaseDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 3);
        var episode = await _db.TVShowEpisodes.SingleAsync(e => e.Id == episodeIds[1], ct);
        episode.Plot = "Episodenhandlung";
        episode.ReleaseDate = new DateTime(2021, 3, 4);
        await _db.SaveChangesAsync(ct);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodeIds[1]));

        var page = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        var entry = Assert.Single(page.Entries);
        Assert.Equal(2, entry.EpisodeNumber);
        Assert.Equal("Episodenhandlung", entry.Plot);
        Assert.Equal(new DateTime(2021, 3, 4), entry.ReleaseDate);
    }

    [Fact]
    public async Task GetEntriesPaged_MovieWithoutPlotOrDate_ReturnsNulls()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ohne Angaben");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var page = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        var entry = Assert.Single(page.Entries);
        Assert.Null(entry.Plot);
        Assert.Null(entry.ReleaseDate);
    }

    /// <summary>
    /// Regression (Abnahme Korrektur Detailansicht): plot, release date and episode number are content details
    /// that the item endpoints refuse without an unlock, so a playlist must not deliver them for a locked
    /// entry either - the entry keeps showing its title (grayed out), nothing more.
    /// </summary>
    [Fact]
    public async Task GetEntriesPaged_LockedMovie_HidesPlotAndReleaseDate_ButKeepsTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = new Movie { Name = "Gesperrter Film", MediaSourceId = 99, CreatedAt = DateTime.UtcNow, ReleaseDate = new DateTime(2019, 5, 17), Plot = "Geheime Handlung." };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        await GrantMediaSourceAccessForUserAsync(_testUserId, mediaSourceId: 1);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));

        var page = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        var entry = Assert.Single(page.Entries);
        Assert.False(entry.IsAccessible);
        Assert.Equal("Gesperrter Film", entry.MediaTitle);
        Assert.Null(entry.Plot);
        Assert.Null(entry.ReleaseDate);
        Assert.Null(entry.EpisodeNumber);
    }

    [Fact]
    public async Task GetEntriesPaged_LockedEpisode_HidesEpisodeNumberPlotAndReleaseDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 3);
        var episode = await _db.TVShowEpisodes.SingleAsync(e => e.Id == episodeIds[1], ct);
        episode.Plot = "Geheime Episodenhandlung";
        episode.ReleaseDate = new DateTime(2021, 3, 4);
        await _db.SaveChangesAsync(ct);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodeIds[1]));

        var page = await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct);

        var entry = Assert.Single(page.Entries);
        Assert.False(entry.IsAccessible);
        Assert.Null(entry.EpisodeNumber);
        Assert.Null(entry.Plot);
        Assert.Null(entry.ReleaseDate);
    }

    /// <summary>
    /// A viewer of a public playlist gets the details only when the VIEWER has access - the owner's unlock
    /// must not carry over (the same viewer/owner mix-up class as the unlock id-space bug of Schritt 3).
    /// </summary>
    [Fact]
    public async Task GetEntriesPaged_PublicPlaylist_DetailsFollowTheViewersAccess_NotTheOwners()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow, ReleaseDate = new DateTime(2019, 5, 17), Plot = "Eine Handlung." };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync(ct);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));
        await MakePlaylistPublicAsync(playlistId);

        var ownerEntry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _testUserId, 1, 20, ct)).Entries);
        var lockedViewerEntry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 20, ct)).Entries);
        await GrantMediaSourceAccessForUserAsync(_otherUserId);
        var unlockedViewerEntry = Assert.Single((await _service.GetPlaylistEntriesPagedAsync(playlistId, _otherUserId, 1, 20, ct)).Entries);

        Assert.Equal("Eine Handlung.", ownerEntry.Plot);
        Assert.False(lockedViewerEntry.IsAccessible);
        Assert.Null(lockedViewerEntry.Plot);
        Assert.Null(lockedViewerEntry.ReleaseDate);
        Assert.True(unlockedViewerEntry.IsAccessible);
        Assert.Equal("Eine Handlung.", unlockedViewerEntry.Plot);
        Assert.Equal(new DateTime(2019, 5, 17), unlockedViewerEntry.ReleaseDate);
    }

    [Fact]
    public async Task PreviewPlaylistCover_NoImages_ReturnsNullAndSavesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Ohne Poster");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var bytes = await _service.PreviewPlaylistCoverAsync(playlistId, _testUserId, ct);

        Assert.Null(bytes);
        Assert.Null((await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct)).CoverPictureId);
    }

    [Fact]
    public async Task PreviewPlaylistCover_ForeignUser_ThrowsAccessDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
        await MakePlaylistPublicAsync(playlistId);

        // Even for a public playlist only the owner may compose a cover preview.
        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(() => _service.PreviewPlaylistCoverAsync(playlistId, _otherUserId, ct));
    }
}
