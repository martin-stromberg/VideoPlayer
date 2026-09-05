using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the playlist entries endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>:
/// <c>AddMediaToPlaylist</c>, <c>RemoveMediaFromPlaylist</c> and <c>GetPlaylistEntries</c>.
/// </summary>
public class PlaylistsControllerTests_Entries : PlaylistsControllerTestBase
{
    private async Task<long> CreateMovieAsync(string name = "Testfilm")
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    private async Task<long> CreatePlaylistAsync(string name = "Meine Playlist")
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = name });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;
        return created!.Id;
    }

    [Fact]
    public async Task AddMediaToPlaylist_ValidInput_Returns200Ok()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest
        {
            MediaType = MediaTypeValues.Movie,
            MediaId = movieId
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistEntry>(okResult.Value);
        Assert.Equal(movieId, dto.MediaId);
    }

    [Fact]
    public async Task AddMediaToPlaylist_Duplicate_Returns409Conflict()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AddMediaToPlaylist_UnknownMediaType_Returns400BadRequest()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = "UnknownType", MediaId = 1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddMediaToPlaylist_MediaNotFound_Returns404NotFound()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = 999999 });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddMediaToPlaylist_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_ValidInput_Returns204NoContent()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_NotFound_Returns404NotFound()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, 999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetPlaylistEntries_ValidInput_Returns200Ok()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.GetPlaylistEntries(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var entries = Assert.IsType<DtoPlaylistEntry[]>(okResult.Value);
        Assert.Single(entries);
        Assert.Equal(movieId, entries[0].MediaId);
    }

    [Fact]
    public async Task GetPlaylistEntries_EmptyPlaylist_Returns200OkWithEmptyArray()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.GetPlaylistEntries(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var entries = Assert.IsType<DtoPlaylistEntry[]>(okResult.Value);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetPlaylistEntries_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.GetPlaylistEntries(playlistId);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
