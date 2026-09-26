using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for the automatic genre derivation and manual override behavior of playlists
/// (Entwicklungsschritt 9): <see cref="PlaylistService.GetPlaylistAsync"/>'s
/// <see cref="DtoPlaylist.Genres"/>/<see cref="DtoPlaylist.AllGenreIds"/>/<see cref="DtoPlaylist.GenresManuallyOverridden"/>,
/// recomputation on add/remove (<see cref="PlaylistService.AddMediaToPlaylistAsync"/>,
/// <see cref="PlaylistService.RemoveMediaFromPlaylistAsync"/>), manual override
/// (<see cref="PlaylistService.SetPlaylistGenresAsync"/>) and reset
/// (<see cref="PlaylistService.ResetPlaylistGenresAsync"/>).
/// </summary>
public class PlaylistServiceTests_Genres : PlaylistServiceTestBase
{
    [Fact]
    public async Task AddMediaToPlaylist_MoviesWithDifferentGenres_DerivesCombinedGenresSortedByFrequency()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Filme", null, null, ct);

        var movieA = await CreateMovieWithGenresAsync("Film A", "Action", "Drama");
        var movieB = await CreateMovieWithGenresAsync("Film B", "Action", "Komödie");

        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, ct);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieB, ct);

        var result = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.NotNull(result);
        // "Action" occurs in both movies (frequency 2) and must therefore be listed first.
        Assert.Equal("Action", result!.Genres[0].Name);
        Assert.Equal(3, result.Genres.Length);
        Assert.Contains(result.Genres, g => g.Name == "Drama");
        Assert.Contains(result.Genres, g => g.Name == "Komödie");
        Assert.Equal(3, result.AllGenreIds.Length);
        Assert.False(result.GenresManuallyOverridden);
    }

    [Fact]
    public async Task AddMediaToPlaylist_MovieCollection_DerivesGenresFromContainedMovies()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Sammlung-Playlist", null, null, ct);

        var collection = new MovieCollection { Name = "Trilogie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.MovieCollections.Add(collection);
        await _db.SaveChangesAsync(ct);

        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Teil 1", "Fantasy");
        await CreateMovieInCollectionWithGenresAsync(collection.Id, "Teil 2", "Fantasy", "Abenteuer");

        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.MovieCollection, collection.Id, ct);

        var result = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.NotNull(result);
        Assert.Equal("Fantasy", result!.Genres[0].Name);
        Assert.Contains(result.Genres, g => g.Name == "Abenteuer");
    }

    [Fact]
    public async Task AddMediaToPlaylist_TVShowSeasonAndEpisode_DeriveGenresFromParentTVShow()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Serien-Playlist", null, null, ct);

        var (showId, seasonId, episodeIds) = await CreateShowWithSeasonAsync("Serie", "Staffel 1", 1);
        await AddGenresToTVShowAsync(showId, "Krimi");

        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.TVShowSeason, seasonId, ct);

        var afterSeason = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);
        Assert.NotNull(afterSeason);
        Assert.Contains(afterSeason!.Genres, g => g.Name == "Krimi");

        // A second show contributes via a directly-added episode instead of a season/show entry.
        var (otherShowId, _, otherEpisodeIds) = await CreateShowWithSeasonAsync("Serie 2", "Staffel 1", 1);
        await AddGenresToTVShowAsync(otherShowId, "Thriller");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.TVShowEpisode, otherEpisodeIds[0], ct);

        var afterEpisode = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);
        Assert.NotNull(afterEpisode);
        Assert.Contains(afterEpisode!.Genres, g => g.Name == "Krimi");
        Assert.Contains(afterEpisode.Genres, g => g.Name == "Thriller");
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_LastTitleWithGenre_GenreDisappears()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);

        var movieA = await CreateMovieWithGenresAsync("Film A", "Action");
        var movieB = await CreateMovieWithGenresAsync("Film B", "Drama");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, ct);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieB, ct);

        await _service.RemoveMediaFromPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, cancellationToken: ct);

        var result = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Genres, g => g.Name == "Action");
        Assert.Contains(result.Genres, g => g.Name == "Drama");
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_GenreStillPresentOnOtherTitle_FrequencyDecreasesButGenreStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);

        var movieA = await CreateMovieWithGenresAsync("Film A", "Action");
        var movieB = await CreateMovieWithGenresAsync("Film B", "Action");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, ct);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieB, ct);

        await _service.RemoveMediaFromPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, cancellationToken: ct);

        var result = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.NotNull(result);
        Assert.Contains(result!.Genres, g => g.Name == "Action");

        var stored = await _db.PlaylistGenres.AsNoTracking().SingleAsync(pg => pg.PlaylistId == playlist.Id, ct);
        Assert.Equal(1, stored.Count);
    }

    [Fact]
    public async Task SetPlaylistGenres_OverridesSelectionAndBlocksFurtherAutomaticRecompute()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var movieA = await CreateMovieWithGenresAsync("Film A", "Action");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, ct);

        var manualGenreId = await GetOrCreateGenreIdAsync("Handverlesen");
        var updated = await _service.SetPlaylistGenresAsync(playlist.Id, _testUserId, new[] { manualGenreId }, ct);

        Assert.True(updated.GenresManuallyOverridden);
        Assert.Single(updated.Genres);
        Assert.Equal("Handverlesen", updated.Genres[0].Name);

        // Further content changes must not touch the manual selection anymore.
        var movieB = await CreateMovieWithGenresAsync("Film B", "Drama");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieB, ct);
        await _service.RemoveMediaFromPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, cancellationToken: ct);

        var afterChanges = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);
        Assert.NotNull(afterChanges);
        Assert.True(afterChanges!.GenresManuallyOverridden);
        Assert.Single(afterChanges.Genres);
        Assert.Equal("Handverlesen", afterChanges.Genres[0].Name);
    }

    [Fact]
    public async Task ResetPlaylistGenres_RecomputesFromCurrentContentAndClearsOverrideFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var movieA = await CreateMovieWithGenresAsync("Film A", "Action");
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movieA, ct);

        var manualGenreId = await GetOrCreateGenreIdAsync("Handverlesen");
        await _service.SetPlaylistGenresAsync(playlist.Id, _testUserId, new[] { manualGenreId }, ct);

        var reset = await _service.ResetPlaylistGenresAsync(playlist.Id, _testUserId, ct);

        Assert.False(reset.GenresManuallyOverridden);
        Assert.Single(reset.Genres);
        Assert.Equal("Action", reset.Genres[0].Name);
    }

    [Fact]
    public async Task GetPlaylist_MoreGenresThanDisplayLimit_CapsDisplayButKeepsAllGenreIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Viele Genres", null, null, ct);

        var genreNames = new[] { "G1", "G2", "G3", "G4", "G5", "G6", "G7" };
        var movie = await CreateMovieWithGenresAsync("Film", genreNames);
        await _service.AddMediaToPlaylistAsync(playlist.Id, _testUserId, MediaTypeValues.Movie, movie, ct);

        var result = await _service.GetPlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.NotNull(result);
        Assert.Equal(Playlist.MaxDisplayedGenres, result!.Genres.Length);
        Assert.Equal(genreNames.Length, result.AllGenreIds.Length);
    }

    [Fact]
    public async Task GetPlaylists_FilterByGenreId_ReturnsOnlyMatchingPlaylists()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlistWithAction = await _service.CreatePlaylistAsync(_testUserId, "Action-Playlist", null, null, ct);
        var playlistWithDrama = await _service.CreatePlaylistAsync(_testUserId, "Drama-Playlist", null, null, ct);

        var actionMovie = await CreateMovieWithGenresAsync("Actionfilm", "Action");
        var dramaMovie = await CreateMovieWithGenresAsync("Dramafilm", "Drama");
        await _service.AddMediaToPlaylistAsync(playlistWithAction.Id, _testUserId, MediaTypeValues.Movie, actionMovie, ct);
        await _service.AddMediaToPlaylistAsync(playlistWithDrama.Id, _testUserId, MediaTypeValues.Movie, dramaMovie, ct);

        var actionGenreId = await GetOrCreateGenreIdAsync("Action");

        var result = await _service.GetPlaylistsAsync(_testUserId, actionGenreId, ct);

        var single = Assert.Single(result);
        Assert.Equal(playlistWithAction.Id, single.Id);
    }

}
