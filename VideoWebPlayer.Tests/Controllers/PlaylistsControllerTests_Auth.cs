using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests that unauthenticated access to <see cref="VideoWebPlayer.Controllers.PlaylistsController"/> is rejected.
/// </summary>
public class PlaylistsControllerTests_Auth : PlaylistsControllerTestBase
{
    [Fact]
    public async Task Unauthorized_Returns401Unauthorized()
    {
        _fakeAuth.CurrentUser = null;

        var result = await _controller.GetPlaylists();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
