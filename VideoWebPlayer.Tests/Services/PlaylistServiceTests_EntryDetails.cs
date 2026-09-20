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
