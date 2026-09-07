using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.PlaylistsController.GetPlaylists"/> and
/// <see cref="VideoWebPlayer.Controllers.PlaylistsController.GetPlaylist"/>.
/// </summary>
public class PlaylistsControllerTests_Read : PlaylistsControllerTestBase
{
    [Fact]
    public async Task GetPlaylists_Returns200Ok()
    {
        await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Playlist A" });

        var result = await _controller.GetPlaylists();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var playlists = Assert.IsAssignableFrom<DtoPlaylist[]>(okResult.Value);
        Assert.Single(playlists);
    }

    [Fact]
    public async Task GetPlaylist_ValidId_Returns200Ok()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Playlist A" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        var result = await _controller.GetPlaylist(created!.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.Equal(created.Id, dto.Id);
    }

    [Fact]
    public async Task GetPlaylist_NotFound_Returns404NotFound()
    {
        var result = await _controller.GetPlaylist(999999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPlaylist_OwnershipViolation_Returns403Forbidden()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Playlist A" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.GetPlaylist(created!.Id);

        Assert.IsType<ForbidResult>(result);
    }
}
