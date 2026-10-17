using Microsoft.Extensions.Options;
using SkiaSharp;
using VideoWebPlayer.Tests.Helpers;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services.PlaylistCover;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Tests for <see cref="PlaylistCoverValidator"/>: format allowlist, maximum file size, and whether the
/// uploaded bytes decode as a genuine image (Entwicklungsschritt 10, Playlist-Abbildungen).
/// </summary>
public class PlaylistCoverValidatorTests
{
    [Fact]
    public async Task ValidateUpload_ValidJpeg_Success()
    {
        var validator = CreateValidator();
        var jpegBytes = CreateJpegBytes(10, 10);

        var result = await validator.ValidateUploadAsync(jpegBytes, "image/jpeg", jpegBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public async Task ValidateUpload_InvalidBmp_FailsWithFormatMessage()
    {
        var validator = CreateValidator();
        var fakeBytes = new byte[] { 1, 2, 3 };

        var result = await validator.ValidateUploadAsync(fakeBytes, "image/bmp", fakeBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("BMP wird nicht unterstützt", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_FileTooLarge_FailsWithSizeMessage()
    {
        var validator = CreateValidator(maxSizeBytes: 5 * 1024 * 1024);
        var jpegBytes = CreateJpegBytes(10, 10);

        var result = await validator.ValidateUploadAsync(jpegBytes, "image/jpeg", 10 * 1024 * 1024, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("max. 5 MB", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_CorruptImage_FailsWithInvalidImageMessage()
    {
        var validator = CreateValidator();
        var corruptBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02 };

        var result = await validator.ValidateUploadAsync(corruptBytes, "image/jpeg", corruptBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("Datei ist kein gültiges Bild.", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_ValidPng_Success()
    {
        var validator = CreateValidator();
        var pngBytes = TestImages.SolidPng(4, 4, SKColors.Black);

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static byte[] CreateJpegBytes(int width, int height)
        => TestImages.SolidJpeg(width, height, SKColors.Black);

    private static PlaylistCoverValidator CreateValidator(long maxSizeBytes = 5 * 1024 * 1024)
        => new(Options.Create(new PlaylistSettings
        {
            AllowedCoverImageFormats = "image/jpeg,image/png,image/webp",
            MaxCoverImageSizeBytes = maxSizeBytes
        }));
}
