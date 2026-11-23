using SkiaSharp;
using VideoWebPlayer.Services.DemoData;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Unit-Tests für den SkiaSharp-Bildgenerator des Demodatenbestands.
/// </summary>
public sealed class DemoMediaImageGeneratorTests
{
    [Theory]
    [InlineData(nameof(DemoMediaImageGenerator.CreatePoster), 800, 1200)]
    [InlineData(nameof(DemoMediaImageGenerator.CreateFanart), 1280, 720)]
    [InlineData(nameof(DemoMediaImageGenerator.CreateBanner), 1000, 185)]
    [InlineData(nameof(DemoMediaImageGenerator.CreateEpisodeImage), 640, 360)]
    public void Create_ReturnsDecodableJpeg(string method, int expectedWidth, int expectedHeight)
    {
        var generator = new DemoMediaImageGenerator();

        var bytes = method switch
        {
            nameof(DemoMediaImageGenerator.CreatePoster) => generator.CreatePoster("Testtitel"),
            nameof(DemoMediaImageGenerator.CreateFanart) => generator.CreateFanart("Testtitel"),
            nameof(DemoMediaImageGenerator.CreateBanner) => generator.CreateBanner("Testtitel"),
            nameof(DemoMediaImageGenerator.CreateEpisodeImage) => generator.CreateEpisodeImage("Testtitel"),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        using var image = SKImage.FromEncodedData(bytes);
        Assert.NotNull(image);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
        Assert.True(bytes.Length > 1000, "Bild ist verdächtig klein.");
    }
}
