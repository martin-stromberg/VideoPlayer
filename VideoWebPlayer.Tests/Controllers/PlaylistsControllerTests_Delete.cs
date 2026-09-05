using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for <see cref="VideoWebPlayer.Controllers.PlaylistsController.DeletePlaylist"/>.
/// </summary>
public class PlaylistsControllerTests_Delete : PlaylistsControllerTestBase
{
    [Fact]
    public async Task DeletePlaylist_ValidInput_Returns204NoContent()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Zu Loeschen" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        var result = await _controller.DeletePlaylist(created!.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeletePlaylist_NotFound_Returns404NotFound()
    {
        var result = await _controller.DeletePlaylist(999999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeletePlaylist_OwnershipViolation_Returns403Forbidden()
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = "Meine Playlist" });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;

        _fakeAuth.CurrentUser = _otherUser;

        var result = await _controller.DeletePlaylist(created!.Id);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
