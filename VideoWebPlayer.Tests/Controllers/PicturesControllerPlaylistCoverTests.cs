using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Entwicklungsschritt 11: playlist cover pictures must not be reachable through the generic
/// <c>GET /api/pictures/{id}</c> endpoint by users who may not read the playlist (otherwise the cover of a
/// private playlist - a collage of its contents - could be fetched by guessing its picture id, bypassing
/// <c>GET /api/playlists/{id}/cover</c>).
/// </summary>
public class PicturesControllerPlaylistCoverTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string ViewerId = "viewer-1";
    private readonly ApplicationDbContext _db;
    private readonly FakeAuthService _auth = new();
    private readonly PicturesController _controller;

    public PicturesControllerPlaylistCoverTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options, new EventManager());
        _controller = new PicturesController(_db, new MemoryCache(new MemoryCacheOptions()), _auth, null!, NullLogger<PicturesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetPicture_CoverOfPrivatePlaylist_ForeignUser_IsForbidden()
    {
        var pictureId = await CreateCoverAsync(isPublic: false);
        _auth.CurrentUser = new ApplicationUser { Id = ViewerId };

        var result = await _controller.GetPicture(pictureId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetPicture_CoverOfPrivatePlaylist_Owner_IsServed()
    {
        var pictureId = await CreateCoverAsync(isPublic: false);
        _auth.CurrentUser = new ApplicationUser { Id = OwnerId };

        var result = await _controller.GetPicture(pictureId);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task GetPicture_CoverOfPublicPlaylist_ForeignUser_IsServed()
    {
        var pictureId = await CreateCoverAsync(isPublic: true);
        _auth.CurrentUser = new ApplicationUser { Id = ViewerId };

        var result = await _controller.GetPicture(pictureId);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task GetPicture_OrdinaryMediaPicture_StaysAvailableToEveryUser()
    {
        var picture = new Picture { Type = "poster", Data = new byte[] { 1, 2, 3 }, ContentType = "image/png" };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _auth.CurrentUser = new ApplicationUser { Id = ViewerId };

        var result = await _controller.GetPicture(picture.Id);

        Assert.IsType<FileContentResult>(result);
    }

    private async Task<long> CreateCoverAsync(bool isPublic)
    {
        var playlist = new Playlist { UserId = OwnerId, Name = "Playlist", IsPublic = isPublic };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync();
        var picture = new Picture { Type = "cover", Data = new byte[] { 1, 2, 3 }, ContentType = "image/png", PlaylistId = playlist.Id };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();
        return picture.Id;
    }
}
