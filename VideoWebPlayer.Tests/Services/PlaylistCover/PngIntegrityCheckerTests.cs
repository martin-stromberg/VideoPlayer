using VideoWebPlayer.Services.PlaylistCover;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Tests for <see cref="PngIntegrityChecker"/>: a complete PNG is reported intact, truncated files,
/// files with a broken chunk CRC and files without the terminating IEND chunk are reported corrupt.
/// </summary>
public class PngIntegrityCheckerTests
{
    [Fact]
    public void Check_CompletePng_IsIntact()
    {
        var png = TestImages.SolidPng(16, 16, SkiaSharp.SKColors.Teal);

        Assert.Equal(PngIntegrity.Intact, PngIntegrityChecker.Check(png));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void Check_TruncatedPng_IsCorrupt(int divisor)
    {
        var png = TestImages.Noise(64, 64);
        var bytes = TestImages.Encode(png, SkiaSharp.SKEncodedImageFormat.Png);

        Assert.Equal(PngIntegrity.Corrupt, PngIntegrityChecker.Check(bytes[..(bytes.Length / divisor)]));
    }

    [Fact]
    public void Check_PngWithoutIendChunk_IsCorrupt()
    {
        var png = TestImages.SolidPng(16, 16, SkiaSharp.SKColors.Teal);

        Assert.Equal(PngIntegrity.Corrupt, PngIntegrityChecker.Check(png[..^12]));
    }

    [Fact]
    public void Check_PngWithDamagedChunkData_IsCorrupt()
    {
        var png = TestImages.Noise(64, 64);
        var damaged = TestImages.Encode(png, SkiaSharp.SKEncodedImageFormat.Png);
        // Flip bytes inside the compressed pixel data (well after IHDR, before IEND) - the CRC no longer matches.
        for (var i = 60; i < Math.Min(damaged.Length - 20, 120); i++)
            damaged[i] ^= 0xA5;

        Assert.Equal(PngIntegrity.Corrupt, PngIntegrityChecker.Check(damaged));
    }

    [Fact]
    public void Check_PngWithTrailingDataAfterIend_IsIntact()
    {
        var png = TestImages.SolidPng(16, 16, SkiaSharp.SKColors.Teal);
        var withTrailer = png.Concat(new byte[] { 1, 2, 3, 9, 9, 9 }).ToArray();

        Assert.Equal(PngIntegrity.Intact, PngIntegrityChecker.Check(withTrailer));
    }

    [Fact]
    public void Check_NotAPng_IsUnknown()
    {
        Assert.Equal(PngIntegrity.Unknown, PngIntegrityChecker.Check(new byte[] { 1, 2, 3, 4, 5, 6 }));
        Assert.Equal(PngIntegrity.Unknown, PngIntegrityChecker.Check(Array.Empty<byte>()));
    }
}
