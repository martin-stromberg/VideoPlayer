using VideoWebPlayer.Services.PlaylistCover;
using Xunit;

namespace VideoWebPlayer.Tests.Services.PlaylistCover;

/// <summary>
/// Tests for <see cref="JpegIntegrityChecker"/>: complete JPEGs of various sizes and chroma subsamplings
/// are never reported as corrupt, truncated or damaged ones are (Nachbesserungsrunde 1, Schritt 10,
/// Abnahme-Abweichung 2 - SkiaSharp's JPEG decoder silently accepts both).
/// </summary>
/// <remarks>
/// The JPEG variants were generated once with ImageSharp (which can control chroma subsampling and
/// interleaving - SkiaSharp's encoder cannot) and are stored as fixtures under
/// <c>TestData/Jpeg</c>: same image content (fixed-seed noise), same parameters as before.
/// </remarks>
public class JpegIntegrityCheckerTests
{
    [Theory]
    [InlineData("jpeg_1x1_ycbcr420.jpg")]
    [InlineData("jpeg_8x8_ycbcr444.jpg")]
    [InlineData("jpeg_17x9_ycbcr420.jpg")]
    [InlineData("jpeg_64x64_ycbcr422.jpg")]
    [InlineData("jpeg_65x33_ycbcr411.jpg")]
    [InlineData("jpeg_100x37_ycbcr410.jpg")]
    [InlineData("jpeg_31x47_luminance.jpg")]
    [InlineData("jpeg_250x130_rgb.jpg")]
    [InlineData("jpeg_48x48_cmyk.jpg")]
    public void Check_CompleteJpeg_IsIntact(string fixtureName)
    {
        var jpeg = LoadFixture(fixtureName);

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(jpeg));
    }

    [Fact]
    public void Check_CompleteNonInterleavedJpeg_IsIntact()
    {
        var jpeg = LoadFixture("jpeg_70x45_ycbcr420_noninterleaved.jpg");

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(jpeg));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void Check_TruncatedJpeg_IsCorrupt(int divisor)
    {
        var jpeg = LoadFixture("jpeg_64x64_ycbcr420.jpg");

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg[..(jpeg.Length / divisor)]));
    }

    [Fact]
    public void Check_JpegWithoutEndMarker_IsCorrupt()
    {
        var jpeg = LoadFixture("jpeg_64x64_ycbcr420.jpg");

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg[..^2]));
    }

    [Fact]
    public void Check_JpegWithRandomizedImageData_IsCorrupt()
    {
        var jpeg = LoadFixture("jpeg_64x64_ycbcr420.jpg");
        var random = new Random(3);
        for (var i = jpeg.Length / 3; i < jpeg.Length - 2; i++)
            jpeg[i] = (byte)random.Next(0, 255);

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(jpeg));
    }

    [Fact]
    public void Check_JpegWithRemovedMiddleSection_IsCorrupt()
    {
        var jpeg = LoadFixture("jpeg_64x64_ycbcr420.jpg");
        var damaged = jpeg[..(jpeg.Length / 3)].Concat(jpeg[(2 * jpeg.Length / 3)..]).ToArray();

        Assert.Equal(JpegIntegrity.Corrupt, JpegIntegrityChecker.Check(damaged));
    }

    [Fact]
    public void Check_JpegWithTrailingDataAfterEndMarker_IsIntact()
    {
        // Phone "motion photos" append extra data (a video) after the JPEG's EOI marker.
        var jpeg = LoadFixture("jpeg_64x64_ycbcr420.jpg");
        var withTrailer = jpeg.Concat(new byte[] { 1, 2, 3, 0xFF, 0xD8, 9, 9, 9 }).ToArray();

        Assert.Equal(JpegIntegrity.Intact, JpegIntegrityChecker.Check(withTrailer));
    }

    [Fact]
    public void Check_NotAJpeg_IsUnknown()
    {
        Assert.Equal(JpegIntegrity.Unknown, JpegIntegrityChecker.Check(new byte[] { 1, 2, 3, 4, 5, 6 }));
        Assert.Equal(JpegIntegrity.Unknown, JpegIntegrityChecker.Check(Array.Empty<byte>()));
    }

    private static byte[] LoadFixture(string fileName)
        => File.ReadAllBytes(Path.Combine(FindTestDataDirectory(), "Jpeg", fileName));

    private static string FindTestDataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "VideoWebPlayer.Tests", "TestData");
            if (Directory.Exists(candidate))
                return candidate;

            candidate = Path.Combine(directory.FullName, "TestData");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"TestData-Verzeichnis oberhalb von '{AppContext.BaseDirectory}' nicht gefunden.");
    }
}
