using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.PlaylistsController.UpdatePlaylist"/>.
/// </summary>
public class PlaylistsControllerTests_Update : PlaylistsControllerTestBase
{
    [Fact]
    public async Task UpdatePlaylist_ValidInput_Returns200Ok()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Alter Name" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        var result = await _controller.UpdatePlaylist(created!.Id, new DtoUpdatePlaylistRequest { Name = "Neuer Name" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylist>(okResult.Value);
        Assert.Equal("Neuer Name", dto.Name);
    }

    [Fact]
    public async Task UpdatePlaylist_NotFound_Returns404NotFound()
    {
        var result = await _controller.UpdatePlaylist(999999, new DtoUpdatePlaylistRequest { Name = "Neuer Name" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePlaylist_OwnershipViolation_Returns403Forbidden()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Alter Name" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.UpdatePlaylist(created!.Id, new DtoUpdatePlaylistRequest { Name = "Neuer Name" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
