using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.EpisodeBackgroundImage;
using Xunit;

namespace VideoWebPlayer.Tests.Services.EpisodeBackgroundImage;

public class EpisodeBackgroundImageGeneratorTests
{
    [Fact]
    public void Test_ResizeImage_KeepAspectRatio()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(4000, 2000, Color.Red);

        var resized = generator.ResizeImage(source, 1920, 1080);

        using var image = Image.Load(resized);
        Assert.True(image.Width <= 1920);
        Assert.True(image.Height <= 1080);
        var originalAspect = 4000.0 / 2000.0;
        var resizedAspect = (double)image.Width / image.Height;
        Assert.True(Math.Abs(originalAspect - resizedAspect) < 0.02);
    }

    [Fact]
    public void Test_GetDominantColor_ReturnsCorrectColor()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(64, 64, Color.Blue);

        var dominant = generator.GetDominantColor(source);
        var pixel = dominant.ToPixel<Rgba32>();
        var expected = Color.Blue.ToPixel<Rgba32>();

        Assert.Equal(expected.R, pixel.R);
        Assert.Equal(expected.G, pixel.G);
        Assert.Equal(expected.B, pixel.B);
    }

    [Fact]
    public void Test_CreateCanvasWithScaledImage_PlacesImageCentered()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(40, 20, Color.Green);

        var canvasBytes = generator.CreateCanvasWithScaledImage(source, 200, 200, Color.White);

        using var canvas = Image.Load<Rgba32>(canvasBytes);
        Assert.Equal(200, canvas.Width);
        Assert.Equal(200, canvas.Height);

        var corner = canvas[0, 0];
        var whitePixel = Color.White.ToPixel<Rgba32>();
        Assert.Equal(whitePixel.R, corner.R);
        Assert.Equal(whitePixel.G, corner.G);
        Assert.Equal(whitePixel.B, corner.B);

        var center = canvas[100, 100];
        var greenPixel = Color.Green.ToPixel<Rgba32>();
        Assert.Equal(greenPixel.R, center.R);
        Assert.Equal(greenPixel.G, center.G);
        Assert.Equal(greenPixel.B, center.B);
    }

    [Fact]
    public void Test_ApplyTintOverlay_OpacityApplied()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(20, 20, Color.White);

        var unchanged = generator.ApplyTintOverlay(source, Color.Black, 0f);
        var fullyTinted = generator.ApplyTintOverlay(source, Color.Black, 1f);
        var halfTinted = generator.ApplyTintOverlay(source, Color.Black, 0.5f);

        using var unchangedImage = Image.Load<Rgba32>(unchanged);
        using var fullyTintedImage = Image.Load<Rgba32>(fullyTinted);
        using var halfTintedImage = Image.Load<Rgba32>(halfTinted);

        Assert.Equal(255, unchangedImage[5, 5].R);
        Assert.Equal(0, fullyTintedImage[5, 5].R);
        Assert.True(halfTintedImage[5, 5].R > 0 && halfTintedImage[5, 5].R < 255);
    }

    [Fact]
    public async Task Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage()
    {
        var options = new EpisodeBackgroundImageOptions { MaxWidth = 400, MaxHeight = 300 };
        var generator = CreateGenerator(options);
        var episode = new TVShowEpisode { Id = 1, Name = "Episode" };
        var fanartData = CreateTestImage(800, 600, Color.Orange);

        var picture = await generator.GenerateBackgroundImageAsync(episode, fanartData, CancellationToken.None);

        Assert.NotNull(picture);
        Assert.True(picture!.Data.Length > 0);
        Assert.Equal("image/jpeg", picture.ContentType);
        Assert.True(picture.IsGeneratedBackground);
        Assert.Equal(400, picture.Width);
        Assert.Equal(300, picture.Height);
    }

    [Fact]
    public async Task Test_GenerateBackgroundImage_WithMissingFanart_ReturnsNull()
    {
        var generator = CreateGenerator();
        var episode = new TVShowEpisode { Id = 1, Name = "Episode" };
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };

        var picture = await generator.GenerateBackgroundImageAsync(episode, invalidData, CancellationToken.None);

        Assert.Null(picture);
    }

    private static byte[] CreateTestImage(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height, color.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator CreateGenerator(EpisodeBackgroundImageOptions? options = null)
        => new(Options.Create(options ?? new EpisodeBackgroundImageOptions()), NullLogger<VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator>.Instance);
}
