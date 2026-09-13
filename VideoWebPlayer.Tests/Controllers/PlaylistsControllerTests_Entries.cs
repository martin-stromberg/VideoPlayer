using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the playlist entries endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>:
/// <c>AddMediaToPlaylist</c>, <c>RemoveMediaFromPlaylist</c> and <c>GetPlaylistEntries</c>.
/// </summary>
public class PlaylistsControllerTests_Entries : PlaylistsControllerTestBase
{
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
        var dto = Assert.IsType<DtoPlaylistAddResult>(okResult.Value);
        Assert.Equal(movieId, dto.TopLevelEntry?.MediaId);
    }

    [Fact]
    public async Task AddMediaToPlaylist_Duplicate_Returns200OkWithSkippedCount()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistAddResult>(okResult.Value);
        Assert.Equal(1, dto.SkippedDuplicateCount);
        Assert.Empty(dto.AddedEntries);
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

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AddMediaToPlaylist_TVShowAddedTwice_SecondCallSkipsAllAsDuplicates()
    {
        var playlistId = await CreatePlaylistAsync();
        var show = await TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));

        var firstResult = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.TVShow, MediaId = show.Id });
        var firstDto = Assert.IsType<DtoPlaylistAddResult>(Assert.IsType<OkObjectResult>(firstResult).Value);
        Assert.Equal(4, firstDto.AddedEntries.Length);
        Assert.Equal(0, firstDto.SkippedDuplicateCount);

        var secondResult = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.TVShow, MediaId = show.Id });
        var secondDto = Assert.IsType<DtoPlaylistAddResult>(Assert.IsType<OkObjectResult>(secondResult).Value);
        Assert.Empty(secondDto.AddedEntries);
        Assert.Equal(4, secondDto.SkippedDuplicateCount);
    }

    [Fact]
    public async Task AddMediaToPlaylist_AfterRemovingEpisode_ReAddingShowRestoresEpisode()
    {
        var playlistId = await CreatePlaylistAsync();
        var show = await TestHelpers.CreateTvShowWithSeasonsAsync(_db,
            ("Staffel 1", new[] { (1, (DateTime?)null), (2, (DateTime?)null) }));
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.TVShow, MediaId = show.Id });
        var episodeId = await _db.TVShowEpisodes.AsNoTracking().Select(e => e.Id).FirstAsync(TestContext.Current.CancellationToken);

        await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.TVShowEpisode, episodeId);
        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.TVShow, MediaId = show.Id });

        var dto = Assert.IsType<DtoPlaylistAddResult>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(dto.AddedEntries);
        Assert.Equal(MediaTypeValues.TVShowEpisode, dto.AddedEntries[0].MediaType);
        Assert.Equal(episodeId, dto.AddedEntries[0].MediaId);
        Assert.Equal(3, dto.SkippedDuplicateCount);
    }

    [Fact]
    public async Task AddMediaToPlaylist_DifferentCasingSameMedia_SecondCallDetectsDuplicate()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = "Movie", MediaId = movieId });

        var result = await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = "movie", MediaId = movieId });

        var dto = Assert.IsType<DtoPlaylistAddResult>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, dto.SkippedDuplicateCount);
        Assert.Empty(dto.AddedEntries);

        var entriesResult = await _controller.GetPlaylistEntries(playlistId);
        var entries = Assert.IsType<DtoPlaylistEntry[]>(Assert.IsType<OkObjectResult>(entriesResult).Value);
        Assert.Single(entries);
        Assert.Equal("Movie", entries[0].MediaType);
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
    public async Task RemoveMediaFromPlaylist_DifferentCasingMediaType_Returns204NoContent()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, "movie", movieId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_UnknownMediaType_Returns400BadRequest()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, "UnknownType", 1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId);

        Assert.IsType<ForbidResult>(result);
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

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetEntriesPaged_ValidatesPageNumber_GreaterThanZero()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.GetPlaylistEntriesPaged(playlistId, 0, 20);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetEntriesPaged_ValidatesPageSize_TooSmall()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.GetPlaylistEntriesPaged(playlistId, 1, 0);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetEntriesPaged_ValidatesPageSize_TooLarge()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.GetPlaylistEntriesPaged(playlistId, 1, 101);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetEntriesPaged_UsesConfiguredDefaultPageSize_WhenPageSizeOmitted()
    {
        var playlistId = await CreatePlaylistAsync();
        _controller = CreateController(new PlaylistSettings { DefaultPageSize = 5, MaxPageSize = 100 });

        var result = await _controller.GetPlaylistEntriesPaged(playlistId, 1, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistEntriesPagedResult>(okResult.Value);
        Assert.Equal(5, dto.PageSize);
    }

    [Fact]
    public async Task GetEntriesPaged_UsesConfiguredMaxPageSize_ToRejectTooLargePageSize()
    {
        var playlistId = await CreatePlaylistAsync();
        _controller = CreateController(new PlaylistSettings { DefaultPageSize = 20, MaxPageSize = 10 });

        var withinConfiguredMax = await _controller.GetPlaylistEntriesPaged(playlistId, 1, 10);
        var beyondConfiguredMax = await _controller.GetPlaylistEntriesPaged(playlistId, 1, 11);

        Assert.IsType<OkObjectResult>(withinConfiguredMax);
        Assert.IsType<BadRequestObjectResult>(beyondConfiguredMax);
    }

    [Fact]
    public async Task GetEntriesPaged_Endpoint_ReturnsCorrectResponse()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.GetPlaylistEntriesPaged(playlistId, 1, 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistEntriesPagedResult>(okResult.Value);
        Assert.Single(dto.Entries);
        Assert.Equal(movieId, dto.Entries[0].MediaId);
        Assert.Equal(1, dto.TotalCount);
        Assert.False(dto.HasNextPage);
        Assert.Equal(1, dto.PageNumber);
        Assert.Equal(20, dto.PageSize);
    }
}
