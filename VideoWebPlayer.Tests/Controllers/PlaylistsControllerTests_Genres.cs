using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the genre-related playlist endpoints (Entwicklungsschritt 9): the <c>genreId</c> filter on
/// <see cref="VideoWebPlayer.Controllers.PlaylistsController.GetPlaylists"/>, and
/// <see cref="VideoWebPlayer.Controllers.PlaylistsController.SetPlaylistGenres"/>/
/// <see cref="VideoWebPlayer.Controllers.PlaylistsController.ResetPlaylistGenres"/>.
/// </summary>
public class PlaylistsControllerTests_Genres : PlaylistsControllerTestBase
{
    private bool _mediaSourceEnsured;

    [Fact]
    public async Task GetPlaylists_FilterByGenreId_ReturnsOnlyMatchingPlaylists()
    {
        var actionPlaylistId = await CreatePlaylistAsync("Action-Playlist");
        var dramaPlaylistId = await CreatePlaylistAsync("Drama-Playlist");

        var actionMovieId = await CreateMovieWithGenreAsync("Actionfilm", "Action");
        var dramaMovieId = await CreateMovieWithGenreAsync("Dramafilm", "Drama");
        await _controller.AddMediaToPlaylist(actionPlaylistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = actionMovieId });
        await _controller.AddMediaToPlaylist(dramaPlaylistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = dramaMovieId });

        var actionGenreId = await GetGenreIdByNameAsync("Action");

        var result = await _controller.GetPlaylists(actionGenreId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var playlists = Assert.IsAssignableFrom<DtoPlaylist[]>(okResult.Value);
        var single = Assert.Single(playlists);
        Assert.Equal(actionPlaylistId, single.Id);
    }

    [Fact]
    public async Task SetPlaylistGenres_ValidInput_Returns200OkWithOverriddenGenres()
    {
        var playlistId = await CreatePlaylistAsync();
        var genreId = await CreateGenreAsync("Handverlesen");

        var result = await _controller.SetPlaylistGenres(playlistId, new DtoSetPlaylistGenresRequest { GenreIds = new[] { genreId } });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.True(dto.GenresManuallyOverridden);
        var single = Assert.Single(dto.Genres);
        Assert.Equal("Handverlesen", single.Name);
    }

    [Fact]
    public async Task SetPlaylistGenres_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        var genreId = await CreateGenreAsync("Handverlesen");
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.SetPlaylistGenres(playlistId, new DtoSetPlaylistGenresRequest { GenreIds = new[] { genreId } });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ResetPlaylistGenres_AfterManualOverride_RecomputesFromContentAndClearsFlag()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieWithGenreAsync("Film", "Action");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var manualGenreId = await CreateGenreAsync("Handverlesen");
        await _controller.SetPlaylistGenres(playlistId, new DtoSetPlaylistGenresRequest { GenreIds = new[] { manualGenreId } });

        var result = await _controller.ResetPlaylistGenres(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.False(dto.GenresManuallyOverridden);
        var single = Assert.Single(dto.Genres);
        Assert.Equal("Action", single.Name);
    }

    [Fact]
    public async Task ResetPlaylistGenres_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.ResetPlaylistGenres(playlistId);

        Assert.IsType<ForbidResult>(result);
    }

    private async Task<long> CreateGenreAsync(string name)
    {
        if (!_mediaSourceEnsured)
        {
            if (!await _db.MediaSources.AnyAsync(s => s.Id == 1))
            {
                _db.MediaSources.Add(new MediaSource { Id = 1, Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 });
                await _db.SaveChangesAsync();
            }
            _mediaSourceEnsured = true;
        }

        var genre = new Genre { MediaSourceId = 1, Name = name };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync();
        return genre.Id;
    }

    private async Task<long> GetGenreIdByNameAsync(string name)
        => (await _db.Genres.AsNoTracking().FirstAsync(g => g.Name == name)).Id;

    private async Task<long> CreateMovieWithGenreAsync(string name, string genreName)
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        var genreId = await CreateGenreAsync(genreName);
        _db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genreId });
        await _db.SaveChangesAsync();

        return movie.Id;
    }
}
