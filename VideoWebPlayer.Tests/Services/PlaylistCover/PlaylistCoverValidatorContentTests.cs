using System.Buffers.Binary;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services.PlaylistCover;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Regression tests for the content checks of <see cref="PlaylistCoverValidator"/> (Nachbesserungsrunde 1,
/// Entwicklungsschritt 10, Abnahme-Abweichung 2): the format that is actually contained in the upload is
/// checked against the allowlist (not the client-supplied MIME type), pixel dimensions are limited on the
/// header - before any decoding - and truncated/corrupted images are rejected by a full decode.
/// </summary>
public class PlaylistCoverValidatorContentTests
{
    [Fact]
    public async Task ValidateUpload_GifClaimedAsPng_IsRejectedAsUnsupportedFormat()
    {
        var validator = CreateValidator();
        var gifBytes = Encode(CreateNoiseImage(16, 16), new GifEncoder());

        var result = await validator.ValidateUploadAsync(gifBytes, "image/png", gifBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("GIF wird nicht unterstützt", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_PngClaimedAsJpeg_IsAcceptedWithActualContentType()
    {
        var validator = CreateValidator();
        var pngBytes = Encode(CreateNoiseImage(16, 16), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/jpeg", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public async Task ValidateUpload_ValidWebp_Success()
    {
        var validator = CreateValidator();
        var webpBytes = Encode(CreateNoiseImage(16, 16), new WebpEncoder());

        var result = await validator.ValidateUploadAsync(webpBytes, "image/webp", webpBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("image/webp", result.ContentType);
    }

    [Fact]
    public async Task ValidateUpload_ValidLosslessWebp_Success()
    {
        var validator = CreateValidator();
        var webpBytes = Encode(CreateNoiseImage(64, 64), new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });

        var result = await validator.ValidateUploadAsync(webpBytes, "image/webp", webpBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("image/webp", result.ContentType);
    }

    /// <summary>
    /// ImageSharp's WebP decoder accepts a file that was cut off (even by a single byte), so a truncated
    /// WebP must be rejected via the length its RIFF header declares - the documentation promises that.
    /// </summary>
    /// <param name="bytesCutOff">How many bytes are removed from the end of a valid WebP file.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(40)]
    public async Task ValidateUpload_TruncatedWebp_IsRejectedAsCorrupt(int bytesCutOff)
    {
        var validator = CreateValidator();
        var webpBytes = Encode(CreateNoiseImage(64, 64), new WebpEncoder());
        var truncated = webpBytes[..(webpBytes.Length - bytesCutOff)];

        var result = await validator.ValidateUploadAsync(truncated, "image/webp", truncated.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("beschädigt oder unvollständig", result.ErrorMessage);
    }

    /// <summary>
    /// A header whose dimension overflows to a non-positive value (4e9 does not fit an int) must not slip
    /// past the pixel limits because the product of a negative and a positive number is negative.
    /// </summary>
    [Fact]
    public async Task ValidateUpload_PngHeaderWithOverflowingWidth_IsRejectedAsNotAnImage()
    {
        var validator = CreateValidator();
        var bomb = CreatePngHeaderOnly(4_000_000_000u, 100);

        var result = await validator.ValidateUploadAsync(bomb, "image/png", bomb.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task ValidateUpload_TruncatedJpeg_IsRejectedAsCorrupt()
    {
        var validator = CreateValidator();
        var jpegBytes = Encode(CreateNoiseImage(64, 64), new JpegEncoder());
        var truncated = jpegBytes[..(jpegBytes.Length / 3)];

        var result = await validator.ValidateUploadAsync(truncated, "image/jpeg", truncated.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("beschädigt oder unvollständig", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_JpegWithDestroyedBody_IsRejectedAsCorrupt()
    {
        var validator = CreateValidator();
        var jpegBytes = Encode(CreateNoiseImage(64, 64), new JpegEncoder());
        var damaged = (byte[])jpegBytes.Clone();
        // Header (up to and including the start-of-scan segment) stays intact; the entropy-coded body is
        // overwritten with pseudo-random bytes (no 0xFF, so no accidental markers), the trailing EOI marker stays.
        var random = new Random(7);
        var bodyStart = damaged.Length / 3;
        for (var i = bodyStart; i < damaged.Length - 2; i++)
            damaged[i] = (byte)random.Next(0, 255);

        var result = await validator.ValidateUploadAsync(damaged, "image/jpeg", damaged.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateUpload_TruncatedPng_IsRejectedAsCorrupt()
    {
        var validator = CreateValidator();
        var pngBytes = Encode(CreateNoiseImage(64, 64), new PngEncoder());
        var truncated = pngBytes[..(pngBytes.Length / 2)];

        var result = await validator.ValidateUploadAsync(truncated, "image/png", truncated.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateUpload_PngWithDamagedImageData_IsRejectedAsCorrupt()
    {
        var validator = CreateValidator();
        var pngBytes = Encode(CreateNoiseImage(64, 64), new PngEncoder());
        var damaged = (byte[])pngBytes.Clone();
        // Flip bytes inside the compressed pixel data (well after IHDR, before IEND).
        for (var i = 60; i < Math.Min(damaged.Length - 20, 120); i++)
            damaged[i] ^= 0xA5;

        var result = await validator.ValidateUploadAsync(damaged, "image/png", damaged.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// A tiny PNG file whose header claims 30000 x 30000 pixels (a decompression bomb) must be rejected
    /// based on the header alone - the file contains no pixel data at all, so a rejection with the
    /// dimension message proves the check ran before any decoding.
    /// </summary>
    [Fact]
    public async Task ValidateUpload_PngHeaderWith30000x30000Pixels_IsRejectedByPixelLimitBeforeDecoding()
    {
        var validator = CreateValidator();
        var bomb = CreatePngHeaderOnly(30000, 30000);

        var result = await validator.ValidateUploadAsync(bomb, "image/png", bomb.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Bild zu groß", result.ErrorMessage);
        Assert.Contains("30000 x 30000", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_PngHeaderWithFourBillionPixelsPerSide_IsRejected()
    {
        var validator = CreateValidator();
        var bomb = CreatePngHeaderOnly(4_000_000_000u, 4_000_000_000u);

        var result = await validator.ValidateUploadAsync(bomb, "image/png", bomb.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task ValidateUpload_ImageWiderThanConfiguredLimit_IsRejected()
    {
        var validator = CreateValidator(maxWidth: 32, maxHeight: 4096, maxTotal: 0);
        var pngBytes = Encode(CreateNoiseImage(33, 8), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Bild zu groß (33 x 8 Pixel)", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_ImageTallerThanConfiguredLimit_IsRejected()
    {
        var validator = CreateValidator(maxWidth: 4096, maxHeight: 32, maxTotal: 0);
        var pngBytes = Encode(CreateNoiseImage(8, 33), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Bild zu groß (8 x 33 Pixel)", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_ImageExceedingTotalPixelLimit_IsRejected()
    {
        var validator = CreateValidator(maxWidth: 4096, maxHeight: 4096, maxTotal: 100);
        var pngBytes = Encode(CreateNoiseImage(11, 10), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Megapixel", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateUpload_ImageAtExactlyTheLimits_Success()
    {
        var validator = CreateValidator(maxWidth: 32, maxHeight: 16, maxTotal: 512);
        var pngBytes = Encode(CreateNoiseImage(32, 16), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(32, result.Width);
        Assert.Equal(16, result.Height);
    }

    [Fact]
    public async Task ValidateUpload_LimitsDisabledWithZero_AllowsLargerImage()
    {
        var validator = CreateValidator(maxWidth: 0, maxHeight: 0, maxTotal: 0);
        var pngBytes = Encode(CreateNoiseImage(64, 64), new PngEncoder());

        var result = await validator.ValidateUploadAsync(pngBytes, "image/png", pngBytes.Length, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static PlaylistCoverValidator CreateValidator(int maxWidth = 4096, int maxHeight = 4096, long maxTotal = 4096L * 4096L)
        => new(Options.Create(new PlaylistSettings
        {
            AllowedCoverImageFormats = "image/jpeg,image/png,image/webp",
            MaxCoverImageSizeBytes = 5 * 1024 * 1024,
            MaxCoverImageWidthPixels = maxWidth,
            MaxCoverImageHeightPixels = maxHeight,
            MaxCoverImageTotalPixels = maxTotal
        }));

    /// <summary>
    /// Creates an image of pseudo-random pixels (fixed seed), so encoded files are large enough that
    /// truncating or damaging them actually removes/destroys pixel data.
    /// </summary>
    /// <param name="width">The image width, in pixels.</param>
    /// <param name="height">The image height, in pixels.</param>
    /// <returns>The noise image.</returns>
    private static Image<Rgba32> CreateNoiseImage(int width, int height)
    {
        var random = new Random(42);
        var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
        }

        return image;
    }

    private static byte[] Encode(Image<Rgba32> image, SixLabors.ImageSharp.Formats.IImageEncoder encoder)
    {
        using (image)
        using (var stream = new MemoryStream())
        {
            image.Save(stream, encoder);
            return stream.ToArray();
        }
    }

    /// <summary>
    /// Builds a minimal PNG consisting only of the signature and an IHDR chunk (with a correct CRC) that
    /// claims the given dimensions - no pixel data, so it is a few dozen bytes yet identifies as huge.
    /// </summary>
    /// <param name="width">The claimed width, in pixels.</param>
    /// <param name="height">The claimed height, in pixels.</param>
    /// <returns>The bytes of the header-only PNG file.</returns>
    private static byte[] CreatePngHeaderOnly(uint width, uint height)
    {
        var ihdrData = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4, 4), height);
        ihdrData[8] = 8; // bit depth
        ihdrData[9] = 2; // colour type: RGB

        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(stream, "IHDR", ihdrData);
        WriteChunk(stream, "IEND", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var lengthBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)data.Length);
        stream.Write(lengthBytes);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, Crc32(crcInput));
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}
