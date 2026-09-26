using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the cover preview endpoint of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>
/// (Kundenrückmeldung zur Playlist-Detailansicht): the collage is only returned as image data, nothing is
/// saved - the cover only changes once the owner applies it via the regenerate endpoint.
/// </summary>
public class PlaylistsControllerTests_CoverPreview : PlaylistsControllerTestBase
{
    [Fact]
    public async Task PreviewPlaylistCover_NoSourceImages_ReturnsSuccessFalseWithMessage()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.PreviewPlaylistCover(playlistId);

        var dto = Assert.IsType<DtoPlaylistCoverPreview>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(dto.Success);
        Assert.Null(dto.ImageData);
        Assert.False(string.IsNullOrWhiteSpace(dto.Message));
    }

    [Fact]
    public async Task PreviewPlaylistCover_WithSourceImages_ReturnsJpegCollageWithoutSavingAnything()
    {
        var playlistId = await CreatePlaylistWithMovieAsync();
        var picturesBefore = await _db.Pictures.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);

        var result = await _controller.PreviewPlaylistCover(playlistId);

        var dto = Assert.IsType<DtoPlaylistCoverPreview>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(dto.Success);
        Assert.Equal("image/jpeg", dto.ContentType);
        Assert.NotNull(dto.ImageData);
        Assert.True(dto.ImageData!.Length > 0);
        using (var image = Image.Load(dto.ImageData))
            Assert.True(image.Width > 0);

        // Nothing was persisted: no new picture, no cover set on the playlist.
        Assert.Equal(picturesBefore, await _db.Pictures.AsNoTracking().CountAsync(TestContext.Current.CancellationToken));
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, TestContext.Current.CancellationToken);
        Assert.Null(stored.CoverPictureId);
        Assert.False(stored.CoverPictureIsUserUploaded);
    }

    [Fact]
    public async Task PreviewPlaylistCover_OverUploadedCover_NeedsNoConfirmationAndKeepsUploadedCover()
    {
        var playlistId = await CreatePlaylistWithMovieAsync();
        await _controller.UploadPlaylistCover(playlistId, CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg"));
        var before = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, TestContext.Current.CancellationToken);

        var result = await _controller.PreviewPlaylistCover(playlistId);

        Assert.True(Assert.IsType<DtoPlaylistCoverPreview>(Assert.IsType<OkObjectResult>(result).Value).Success);
        var after = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, TestContext.Current.CancellationToken);
        Assert.Equal(before.CoverPictureId, after.CoverPictureId);
        Assert.True(after.CoverPictureIsUserUploaded);
    }

    /// <summary>
    /// The preview shows what "Anwenden" will produce: applying (regenerate) afterwards stores exactly the
    /// collage the preview returned - the composition is deterministic for unchanged contents.
    /// </summary>
    [Fact]
    public async Task PreviewPlaylistCover_ThenApplyViaRegenerate_StoresTheSameCollage()
    {
        var playlistId = await CreatePlaylistWithMovieAsync();
        var preview = Assert.IsType<DtoPlaylistCoverPreview>(Assert.IsType<OkObjectResult>(await _controller.PreviewPlaylistCover(playlistId)).Value);

        var apply = Assert.IsType<DtoPlaylistCoverResult>(Assert.IsType<OkObjectResult>(await _controller.RegeneratePlaylistCover(playlistId)).Value);

        Assert.True(apply.Success);
        var stored = await _db.Pictures.AsNoTracking().SingleAsync(p => p.Id == apply.PictureId, TestContext.Current.CancellationToken);
        Assert.Equal(preview.ImageData, stored.Data);
    }

    [Fact]
    public async Task PreviewPlaylistCover_NotOwner_Returns403AndSavesNothing()
    {
        var playlistId = await CreatePlaylistWithMovieAsync();
        _fakeAuth.CurrentUser = _otherUser;
        var picturesBefore = await _db.Pictures.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);

        var result = await _controller.PreviewPlaylistCover(playlistId);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(picturesBefore, await _db.Pictures.AsNoTracking().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreviewPlaylistCover_UnknownPlaylist_Returns404()
    {
        var result = await _controller.PreviewPlaylistCover(999_999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PreviewPlaylistCover_NotLoggedIn_Returns401()
    {
        var playlistId = await CreatePlaylistAsync();
        _fakeAuth.CurrentUser = null;

        var result = await _controller.PreviewPlaylistCover(playlistId);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private async Task<long> CreatePlaylistWithMovieAsync()
    {
        var playlistId = await CreatePlaylistAsync();
        var picture = new Picture { Type = "poster", Data = CreateJpegBytes(), ContentType = "image/jpeg" };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = picture.Id };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movie.Id });
        return playlistId;
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType)
        => new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static byte[] CreateJpegBytes()
    {
        using var image = new Image<Rgba32>(8, 8, Color.Teal.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }
}
