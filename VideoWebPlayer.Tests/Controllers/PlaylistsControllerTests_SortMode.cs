using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.PlaylistsController.ChangeSortMode"/>.
/// </summary>
public class PlaylistsControllerTests_SortMode : PlaylistsControllerTestBase
{
    private async Task<long> CreatePlaylistAsync(string name, string? sortMode = null)
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = name, SortMode = sortMode });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;
        return created!.Id;
    }

    [Fact]
    public async Task ChangeSortMode_ManualToByReleaseDate_WithoutConfirmation_Returns409Conflict()
    {
        var playlistId = await CreatePlaylistAsync("Playlist", PlaylistSortModeValues.Manual);

        var result = await _controller.ChangeSortMode(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.ByReleaseDate,
            ConfirmLossOfManualOrder = false
        });

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<DtoChangeSortModeConflictResponse>(conflictResult.Value);
        Assert.True(response.IsLossOfDataConfirmationRequired);
    }

    [Fact]
    public async Task ChangeSortMode_ManualToByReleaseDate_WithConfirmation_Returns200Ok()
    {
        var playlistId = await CreatePlaylistAsync("Playlist", PlaylistSortModeValues.Manual);

        var result = await _controller.ChangeSortMode(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.ByReleaseDate,
            ConfirmLossOfManualOrder = true
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.Equal(PlaylistSortModeValues.ByReleaseDate, dto.SortMode);
    }

    [Fact]
    public async Task ChangeSortMode_ByReleaseDateToManual_Returns200Ok()
    {
        var playlistId = await CreatePlaylistAsync("Playlist", PlaylistSortModeValues.ByReleaseDate);

        var result = await _controller.ChangeSortMode(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = false
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.Equal(PlaylistSortModeValues.Manual, dto.SortMode);
    }

    [Fact]
    public async Task ChangeSortMode_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync("Playlist", PlaylistSortModeValues.Manual);
        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.ChangeSortMode(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.ByReleaseDate,
            ConfirmLossOfManualOrder = true
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ChangeSortMode_InvalidSortMode_Returns400BadRequest()
    {
        var playlistId = await CreatePlaylistAsync("Playlist");

        var result = await _controller.ChangeSortMode(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = "UnknownMode",
            ConfirmLossOfManualOrder = true
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ChangeSortMode_PlaylistNotFound_Returns404NotFound()
    {
        var result = await _controller.ChangeSortMode(999999, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = true
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
