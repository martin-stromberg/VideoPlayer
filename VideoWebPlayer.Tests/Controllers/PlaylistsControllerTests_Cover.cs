using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Tests for the playlist cover endpoints of <see cref="VideoWebPlayer.Controllers.PlaylistsController"/>
/// (Entwicklungsschritt 10, Playlist-Abbildungen): upload, regenerate, get and delete.
/// </summary>
public class PlaylistsControllerTests_Cover : PlaylistsControllerTestBase
{
    [Fact]
    public async Task UploadPlaylistCover_ValidFile_Returns200OkWithPictureId()
    {
        var playlistId = await CreatePlaylistAsync();
        var file = CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg");

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistCoverResult>(okResult.Value);
        Assert.True(dto.Success);
        Assert.NotNull(dto.PictureId);
    }

    [Fact]
    public async Task UploadPlaylistCover_InvalidFormat_Returns400BadRequest()
    {
        var playlistId = await CreatePlaylistAsync();
        var file = CreateFormFile(new byte[] { 1, 2, 3 }, "cover.bmp", "image/bmp");

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("BMP wird nicht unterstützt", badRequest.Value!.ToString());
    }

    /// <summary>
    /// Regression test for the Code-Review finding on <c>UploadPlaylistCover</c> (Entwicklungsschritt 10):
    /// the file size must be checked against <see cref="PlaylistSettings.MaxCoverImageSizeBytes"/> using
    /// only <see cref="IFormFile.Length"/> - available without touching the stream - so an oversized upload
    /// is rejected before it is ever buffered into memory. <see cref="ThrowingIFormFile"/>'s
    /// <see cref="IFormFile.OpenReadStream"/> throws if invoked, so this test fails if the controller ever
    /// tries to read the stream before rejecting the file.
    /// </summary>
    [Fact]
    public async Task UploadPlaylistCover_FileLargerThanMaxSize_Returns400BadRequestWithoutReadingStream()
    {
        var playlistId = await CreatePlaylistAsync();
        _controller = CreateController(new PlaylistSettings { MaxCoverImageSizeBytes = 1024 });
        var file = new ThrowingIFormFile(2048);

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("zu gross", badRequest.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadPlaylistCover_NotOwner_Returns403Forbidden()
    {
        var playlistId = await CreatePlaylistAsync();
        _fakeAuth.CurrentUser = _otherUser;
        var file = CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg");

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RegeneratePlaylistCover_NoSourceImages_ReturnsSuccessFalse()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.RegeneratePlaylistCover(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistCoverResult>(okResult.Value);
        Assert.False(dto.Success);
    }

    [Fact]
    public async Task RegeneratePlaylistCover_WithSourceImages_Returns200OkWithPictureId()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieWithPosterAsync("Film");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.RegeneratePlaylistCover(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistCoverResult>(okResult.Value);
        Assert.True(dto.Success);
        Assert.NotNull(dto.PictureId);
    }

    /// <summary>
    /// Regression test for Abnahme-Abweichung 1 (Nachbesserungsrunde 1, Schritt 10): regenerating over an
    /// uploaded cover must require confirmation (409 Conflict) instead of silently replacing it.
    /// </summary>
    [Fact]
    public async Task RegeneratePlaylistCover_UploadedCoverWithoutConfirmation_Returns409ConflictAndKeepsCover()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieWithPosterAsync("Film");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        await _controller.UploadPlaylistCover(playlistId, CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg"));

        var result = await _controller.RegeneratePlaylistCover(playlistId);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var dto = Assert.IsType<DtoRegeneratePlaylistCoverConflictResponse>(conflict.Value);
        Assert.True(dto.IsUploadedCoverReplacementConfirmationRequired);
        var stored = _db.Playlists.AsNoTracking().Single(p => p.Id == playlistId);
        Assert.True(stored.CoverPictureIsUserUploaded);
    }

    [Fact]
    public async Task RegeneratePlaylistCover_UploadedCoverWithConfirmation_Returns200OkAndReplacesCover()
    {
        var playlistId = await CreatePlaylistAsync();
        var movieId = await CreateMovieWithPosterAsync("Film");
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        await _controller.UploadPlaylistCover(playlistId, CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg"));

        var result = await _controller.RegeneratePlaylistCover(playlistId, confirmReplaceUploadedCover: true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True(Assert.IsType<DtoPlaylistCoverResult>(okResult.Value).Success);
        var stored = _db.Playlists.AsNoTracking().Single(p => p.Id == playlistId);
        Assert.False(stored.CoverPictureIsUserUploaded);
    }

    [Fact]
    public async Task GetPlaylistCover_Exists_Returns200OkWithImageData()
    {
        var playlistId = await CreatePlaylistAsync();
        await _controller.UploadPlaylistCover(playlistId, CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg"));

        var result = await _controller.GetPlaylistCover(playlistId);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    [Fact]
    public async Task GetPlaylistCover_NotSet_Returns404NotFound()
    {
        var playlistId = await CreatePlaylistAsync();

        var result = await _controller.GetPlaylistCover(playlistId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePlaylistCover_Returns200OkWithSuccessTrue()
    {
        var playlistId = await CreatePlaylistAsync();
        await _controller.UploadPlaylistCover(playlistId, CreateFormFile(CreateJpegBytes(), "cover.jpg", "image/jpeg"));

        var result = await _controller.DeletePlaylistCover(playlistId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DtoPlaylistCoverResult>(okResult.Value);
        Assert.True(dto.Success);

        var getResult = await _controller.GetPlaylistCover(playlistId);
        Assert.IsType<NotFoundResult>(getResult);
    }

    private async Task<long> CreateMovieWithPosterAsync(string name)
    {
        var picture = new Picture { Type = "poster", Data = CreateJpegBytes(), ContentType = "image/jpeg" };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();

        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = picture.Id };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreateJpegBytes()
    {
        using var image = new Image<Rgba32>(8, 8, Color.Teal.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// An <see cref="IFormFile"/> whose <see cref="Length"/> reports a caller-given size without any
    /// backing data, and whose <see cref="OpenReadStream"/>/<see cref="CopyTo"/>/<see cref="CopyToAsync"/>
    /// throw if actually invoked - used by <see cref="UploadPlaylistCover_FileLargerThanMaxSize_Returns400BadRequestWithoutReadingStream"/>
    /// to prove the controller rejects an oversized upload purely from <see cref="Length"/>, without ever
    /// reading its content.
    /// </summary>
    private sealed class ThrowingIFormFile : IFormFile
    {
        public ThrowingIFormFile(long length) => Length = length;

        public string ContentType => "image/jpeg";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length { get; }
        public string Name => "file";
        public string FileName => "large.jpg";

        public void CopyTo(Stream target) => throw new InvalidOperationException("Sollte fuer diesen Test nicht aufgerufen werden.");

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Sollte fuer diesen Test nicht aufgerufen werden.");

        public Stream OpenReadStream() => throw new InvalidOperationException("Sollte fuer diesen Test nicht aufgerufen werden.");
    }
}
