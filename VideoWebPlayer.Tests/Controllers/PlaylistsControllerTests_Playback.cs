using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the playlist-playback endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>:
/// <c>/play</c>, <c>/play/next</c>, <c>/play/previous</c> and <c>/play/advance</c>.
/// </summary>
public class PlaylistsControllerTests_Playback : PlaylistsControllerTestBase
{
    /// <summary>
    /// Creates a playlist with two movies for the current user, grants media-source access so both are
    /// playable, and returns the playlist id plus the two playlist entry ids in insertion order.
    /// </summary>
    /// <returns>The playlist id together with the first and second playlist entry ids, in insertion order.</returns>
    private async Task<(long PlaylistId, long FirstEntryId, long SecondEntryId)> CreatePlayablePlaylistWithTwoMoviesAsync()
    {
        var playlistId = await CreatePlaylistAsync();
        var movie1Id = await CreateMovieAsync("Film 1");
        var movie2Id = await CreateMovieAsync("Film 2");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie1Id });
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie2Id });
        await GrantMediaSourceAccessAsync();

        var entriesResult = await _controller.GetPlaylistEntries(playlistId);
        var entries = Assert.IsType<DtoPlaylistEntry[]>(Assert.IsType<OkObjectResult>(entriesResult).Value);
        var firstEntry = Assert.Single(entries, e => e.MediaId == movie1Id);
        var secondEntry = Assert.Single(entries, e => e.MediaId == movie2Id);

        return (playlistId, firstEntry.Id, secondEntry.Id);
    }

    [Fact]
    public async Task PlayEndpoint_StartsPlaylistWithValidEntry()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.StartPlaylist(playlistId, firstEntryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistPlaybackStart>(okResult.Value);
        Assert.Equal(playlistId, dto.PlaylistId);
        Assert.Equal(firstEntryId, dto.CurrentEntryId);
        Assert.Equal(2, dto.TotalCount);
        Assert.Equal(1, dto.CurrentPosition);
    }

    [Fact]
    public async Task PlayEndpoint_NoEntryIdProvided_StartsAtFirstEntry()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.StartPlaylist(playlistId, entryId: null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistPlaybackStart>(okResult.Value);
        Assert.Equal(firstEntryId, dto.CurrentEntryId);
    }

    [Fact]
    public async Task PlayEndpoint_RequiresAuthentication()
    {
        var playlistId = await CreatePlaylistAsync();
        _fakeAuth.CurrentUser = null;

        var result = await _controller.StartPlaylist(playlistId, entryId: null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task PlayEndpoint_RequiresOwnership()
    {
        var (playlistId, _, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.StartPlaylist(playlistId, entryId: null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task PlayEndpoint_PlaylistNotFound_Returns404NotFound()
    {
        var result = await _controller.StartPlaylist(999999, entryId: null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PlayEndpoint_EntryNotInPlaylist_Returns404NotFound()
    {
        var (playlistId, _, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.StartPlaylist(playlistId, entryId: 999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task NextEndpoint_ReturnsNextEntry()
    {
        var (playlistId, firstEntryId, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.GetNextPlaylistEntry(playlistId, firstEntryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistNavigationResult>(okResult.Value);
        Assert.Equal(secondEntryId, dto.Entry.Id);
        Assert.Equal(2, dto.Position);
    }

    [Fact]
    public async Task NextEndpoint_Returns204AtEnd()
    {
        var (playlistId, _, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.GetNextPlaylistEntry(playlistId, secondEntryId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task NextEndpoint_RequiresAuthentication()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = null;

        var result = await _controller.GetNextPlaylistEntry(playlistId, firstEntryId);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task NextEndpoint_RequiresOwnership()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.GetNextPlaylistEntry(playlistId, firstEntryId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task NextEndpoint_SkipsLockedEntry_ReturnsCorrectPosition()
    {
        var playlistId = await CreatePlaylistAsync();
        var movie1Id = await CreateMovieAsync("Film 1");
        var lockedMovie = new Movie { Name = "Gesperrter Film", MediaSourceId = 99, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(lockedMovie);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var movie3Id = await CreateMovieAsync("Film 3");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie1Id });
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = lockedMovie.Id });
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie3Id });
        await GrantMediaSourceAccessAsync();

        var entriesResult = await _controller.GetPlaylistEntries(playlistId);
        var entries = Assert.IsType<DtoPlaylistEntry[]>(Assert.IsType<OkObjectResult>(entriesResult).Value);
        var firstEntryId = Assert.Single(entries, e => e.MediaId == movie1Id).Id;
        var thirdEntryId = Assert.Single(entries, e => e.MediaId == movie3Id).Id;

        var result = await _controller.GetNextPlaylistEntry(playlistId, firstEntryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistNavigationResult>(okResult.Value);
        Assert.Equal(thirdEntryId, dto.Entry.Id);
        // The locked entry at position 2 is skipped, so the reported position must be the third entry's
        // actual position (3), not the caller's previous position incremented by one (2).
        Assert.Equal(3, dto.Position);
    }

    [Fact]
    public async Task NextEndpoint_CurrentEntryNotInPlaylist_Returns400BadRequest()
    {
        var (playlistId, _, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.GetNextPlaylistEntry(playlistId, 999999);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PreviousEndpoint_ReturnsPreviousEntry()
    {
        var (playlistId, firstEntryId, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.GetPreviousPlaylistEntry(playlistId, secondEntryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistNavigationResult>(okResult.Value);
        Assert.Equal(firstEntryId, dto.Entry.Id);
        Assert.Equal(1, dto.Position);
    }

    [Fact]
    public async Task PreviousEndpoint_Returns204AtBeginning()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.GetPreviousPlaylistEntry(playlistId, firstEntryId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PreviousEndpoint_RequiresAuthentication()
    {
        var (playlistId, _, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = null;

        var result = await _controller.GetPreviousPlaylistEntry(playlistId, secondEntryId);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task AdvanceEndpoint_TriggersAutoAdvance()
    {
        var (playlistId, firstEntryId, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.AdvancePlaylist(playlistId, firstEntryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistNavigationResult>(okResult.Value);
        Assert.Equal(secondEntryId, dto.Entry.Id);
        Assert.Equal(2, dto.Position);
    }

    [Fact]
    public async Task AdvanceEndpoint_Returns204AtEnd()
    {
        var (playlistId, _, secondEntryId) = await CreatePlayablePlaylistWithTwoMoviesAsync();

        var result = await _controller.AdvancePlaylist(playlistId, secondEntryId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AdvanceEndpoint_RequiresAuthentication()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = null;

        var result = await _controller.AdvancePlaylist(playlistId, firstEntryId);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task AdvanceEndpoint_RequiresOwnership()
    {
        var (playlistId, firstEntryId, _) = await CreatePlayablePlaylistWithTwoMoviesAsync();
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.AdvancePlaylist(playlistId, firstEntryId);

        Assert.IsType<ForbidResult>(result);
    }
}
