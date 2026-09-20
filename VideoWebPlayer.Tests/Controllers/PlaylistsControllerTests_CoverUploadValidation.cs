using System.Buffers.Binary;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Regression tests (Nachbesserungsrunde 1, Entwicklungsschritt 10, Abnahme-Abweichung 2) for the content
/// checks of <c>POST /api/playlists/{id}/cover/upload</c>, run through the real controller and service
/// against a real SQLite database: a GIF claimed as PNG, a truncated/destroyed JPEG and oversized pixel
/// dimensions used to be accepted with HTTP 200 and are now rejected with HTTP 400 and a German message.
/// </summary>
public class PlaylistsControllerTests_CoverUploadValidation : PlaylistsControllerTestBase
{
    [Fact]
    public async Task UploadPlaylistCover_GifClaimedAsPng_Returns400AndStoresNothing()
    {
        var playlistId = await CreatePlaylistAsync();
        var gif = Encode(new GifEncoder());
        var file = CreateFormFile(gif, "cover.png", "image/png");

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("GIF wird nicht unterstützt", badRequest.Value!.ToString());
        await AssertNoCoverStoredAsync(playlistId);
    }

    [Fact]
    public async Task UploadPlaylistCover_PngClaimedAsJpeg_IsStoredWithActualContentType()
    {
        var playlistId = await CreatePlaylistAsync();
        var png = Encode(new PngEncoder());
        var file = CreateFormFile(png, "cover.jpg", "image/jpeg");

        var result = await _controller.UploadPlaylistCover(playlistId, file);

        Assert.IsType<OkObjectResult>(result);
        var getResult = await _controller.GetPlaylistCover(playlistId);
        Assert.Equal("image/png", Assert.IsType<FileContentResult>(getResult).ContentType);
    }

    [Fact]
    public async Task UploadPlaylistCover_TruncatedJpeg_Returns400AndStoresNothing()
    {
        var playlistId = await CreatePlaylistAsync();
        var jpeg = Encode(new JpegEncoder());
        var truncated = jpeg[..(jpeg.Length / 3)];

        var result = await _controller.UploadPlaylistCover(playlistId, CreateFormFile(truncated, "cover.jpg", "image/jpeg"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("beschädigt oder unvollständig", badRequest.Value!.ToString());
        await AssertNoCoverStoredAsync(playlistId);
    }

    [Fact]
    public async Task UploadPlaylistCover_JpegWithDestroyedBody_Returns400AndStoresNothing()
    {
        var playlistId = await CreatePlaylistAsync();
        var jpeg = Encode(new JpegEncoder());
        var random = new Random(5);
        for (var i = jpeg.Length / 3; i < jpeg.Length - 2; i++)
            jpeg[i] = (byte)random.Next(0, 255);

        var result = await _controller.UploadPlaylistCover(playlistId, CreateFormFile(jpeg, "cover.jpg", "image/jpeg"));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoCoverStoredAsync(playlistId);
    }

    [Theory]
    [InlineData(30000u, 30000u)]
    [InlineData(4_000_000_000u, 4_000_000_000u)]
    public async Task UploadPlaylistCover_PngHeaderWithHugeDimensions_Returns400AndStoresNothing(uint width, uint height)
    {
        var playlistId = await CreatePlaylistAsync();
        var bomb = CreatePngHeaderOnly(width, height);

        var result = await _controller.UploadPlaylistCover(playlistId, CreateFormFile(bomb, "cover.png", "image/png"));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoCoverStoredAsync(playlistId);
    }

    [Fact]
    public async Task UploadPlaylistCover_OversizedDimensions_Returns400WithSizeMessage()
    {
        var playlistId = await CreatePlaylistAsync();
        var bomb = CreatePngHeaderOnly(30000, 30000);

        var result = await _controller.UploadPlaylistCover(playlistId, CreateFormFile(bomb, "cover.png", "image/png"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Bild zu groß (30000 x 30000 Pixel)", badRequest.Value!.ToString());
    }

    private async Task AssertNoCoverStoredAsync(long playlistId)
    {
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId);
        Assert.Null(stored.CoverPictureId);
        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.PlaylistId == playlistId));
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

    private static byte[] Encode(IImageEncoder encoder)
    {
        var random = new Random(42);
        using var image = new Image<Rgba32>(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
        }

        using var stream = new MemoryStream();
        image.Save(stream, encoder);
        return stream.ToArray();
    }

    /// <summary>
    /// Builds a minimal PNG (signature, IHDR with a valid CRC, IEND) claiming the given dimensions without
    /// any pixel data.
    /// </summary>
    /// <param name="width">The claimed width, in pixels.</param>
    /// <param name="height">The claimed height, in pixels.</param>
    /// <returns>The bytes of the header-only PNG file.</returns>
    private static byte[] CreatePngHeaderOnly(uint width, uint height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 2;

        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(stream, "IHDR", ihdr);
        WriteChunk(stream, "IEND", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = typeBytes.Concat(data).ToArray();
        var crc = 0xFFFFFFFFu;
        foreach (var b in crcInput)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
        stream.Write(crcBytes);
    }
}
