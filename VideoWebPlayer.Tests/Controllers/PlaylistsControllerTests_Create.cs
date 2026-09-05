using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.PlaylistsController.CreatePlaylist"/>.
/// </summary>
public class PlaylistsControllerTests_Create : PlaylistsControllerTestBase
{
    [Fact]
    public async Task CreatePlaylist_ValidInput_Returns200Ok()
    {
        var result = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Playlist A" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.Equal("Playlist A", dto.Name);
    }

    [Fact]
    public async Task CreatePlaylist_InvalidName_Returns400BadRequest()
    {
        var result = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "   " });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreatePlaylist_DuplicateName_Returns409Conflict()
    {
        await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Playlist A" });

        var result = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "playlist a" });

        Assert.IsType<ConflictObjectResult>(result);
    }
}
