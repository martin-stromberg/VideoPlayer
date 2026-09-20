using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Services.PlaylistCover;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Tests for <see cref="JpegIntegrityChecker"/>: complete JPEGs of various sizes and chroma subsamplings
/// are never reported as corrupt, truncated or damaged ones are (Nachbesserungsrunde 1, Schritt 10,
/// Abnahme-Abweichung 2 - ImageSharp's JPEG decoder itself silently accepts both).
/// </summary>
public class JpegIntegrityCheckerTests
{
    [Theory]
    [InlineData(1, 1, JpegEncodingColor.YCbCrRatio420)]
    [InlineData(8, 8, JpegEncodingColor.YCbCrRatio444)]
    [InlineData(17, 9, JpegEncodingColor.YCbCrRatio420)]
    [InlineData(64, 64, JpegEncodingColor.YCbCrRatio422)]
    [InlineData(65, 33, JpegEncodingColor.YCbCrRatio411)]
    [InlineData(100, 37, JpegEncodingColor.YCbCrRatio410)]
    [InlineData(31, 47, JpegEncodingColor.Luminance)]
    [InlineData(250, 130, JpegEncodingColor.Rgb)]
    [InlineData(48, 48, JpegEncodingColor.Cmyk)]
    public void Check_CompleteJpeg_IsIntact(int width, int height, JpegEncodingColor color)
    {
        var jpeg = CreateJpeg(width, height, color, interleaved: true);

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(jpeg));
    }

    [Fact]
    public void Check_CompleteNonInterleavedJpeg_IsIntact()
    {
        var jpeg = CreateJpeg(70, 45, JpegEncodingColor.YCbCrRatio420, interleaved: false);

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(jpeg));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void Check_TruncatedJpeg_IsCorrupt(int divisor)
    {
        var jpeg = CreateJpeg(64, 64, JpegEncodingColor.YCbCrRatio420, interleaved: true);

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg[..(jpeg.Length / divisor)]));
    }

    [Fact]
    public void Check_JpegWithoutEndMarker_IsCorrupt()
    {
        var jpeg = CreateJpeg(64, 64, JpegEncodingColor.YCbCrRatio420, interleaved: true);

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg[..^2]));
    }

    [Fact]
    public void Check_JpegWithRandomizedImageData_IsCorrupt()
    {
        var jpeg = CreateJpeg(64, 64, JpegEncodingColor.YCbCrRatio420, interleaved: true);
        var random = new Random(3);
        for (var i = jpeg.Length / 3; i < jpeg.Length - 2; i++)
            jpeg[i] = (byte)random.Next(0, 255);

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg));
    }

    [Fact]
    public void Check_JpegWithRemovedMiddleSection_IsCorrupt()
    {
        var jpeg = CreateJpeg(64, 64, JpegEncodingColor.YCbCrRatio420, interleaved: true);
        var damaged = jpeg[..(jpeg.Length / 3)].Concat(jpeg[(2 * jpeg.Length / 3)..]).ToArray();

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(damaged));
    }

    [Fact]
    public void Check_JpegWithTrailingDataAfterEndMarker_IsIntact()
    {
        // Phone "motion photos" append extra data (a video) after the JPEG's EOI marker.
        var jpeg = CreateJpeg(32, 32, JpegEncodingColor.YCbCrRatio420, interleaved: true);
        var withTrailer = jpeg.Concat(new byte[] { 1, 2, 3, 0xFF, 0xD8, 9, 9, 9 }).ToArray();

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(withTrailer));
    }

    [Fact]
    public void Check_NotAJpeg_IsUnknown()
    {
        Assert.Equal(JpegIntegrity.Unknown, JpegIntegrityChecker.Check(new byte[] { 1, 2, 3, 4, 5, 6 }));
        Assert.Equal(JpegIntegrity.Unknown, JpegIntegrityChecker.Check(Array.Empty<byte>()));
    }

    private static byte[] CreateJpeg(int width, int height, JpegEncodingColor color, bool interleaved)
    {
        var random = new Random(42);
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
        }

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { ColorType = color, Interleaved = interleaved });
        return stream.ToArray();
    }
}
