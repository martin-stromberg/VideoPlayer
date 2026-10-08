using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
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
        var source = CreateTestImage(4000, 2000, SKColors.Red);

        var resized = generator.ResizeImage(source, 1920, 1080);

        using var image = SKBitmap.Decode(resized);
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
        var source = CreateTestImage(64, 64, SKColors.Blue);

        var dominant = generator.GetDominantColor(source);

        Assert.Equal(SKColors.Blue.Red, dominant.Red);
        Assert.Equal(SKColors.Blue.Green, dominant.Green);
        Assert.Equal(SKColors.Blue.Blue, dominant.Blue);
    }

    [Fact]
    public void Test_CreateCanvasWithScaledImage_PlacesImageCentered()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(40, 20, SKColors.Green);

        var canvasBytes = generator.CreateCanvasWithScaledImage(source, 200, 200, SKColors.White);

        using var canvas = SKBitmap.Decode(canvasBytes);
        Assert.Equal(200, canvas.Width);
        Assert.Equal(200, canvas.Height);

        var corner = canvas.GetPixel(0, 0);
        Assert.Equal(SKColors.White.Red, corner.Red);
        Assert.Equal(SKColors.White.Green, corner.Green);
        Assert.Equal(SKColors.White.Blue, corner.Blue);

        var center = canvas.GetPixel(100, 100);
        Assert.Equal(SKColors.Green.Red, center.Red);
        Assert.Equal(SKColors.Green.Green, center.Green);
        Assert.Equal(SKColors.Green.Blue, center.Blue);
    }

    [Fact]
    public void Test_ApplyTintOverlay_OpacityApplied()
    {
        var generator = CreateGenerator();
        var source = CreateTestImage(20, 20, SKColors.White);

        var unchanged = generator.ApplyTintOverlay(source, SKColors.Black, 0f);
        var fullyTinted = generator.ApplyTintOverlay(source, SKColors.Black, 1f);
        var halfTinted = generator.ApplyTintOverlay(source, SKColors.Black, 0.5f);

        using var unchangedImage = SKBitmap.Decode(unchanged);
        using var fullyTintedImage = SKBitmap.Decode(fullyTinted);
        using var halfTintedImage = SKBitmap.Decode(halfTinted);

        Assert.Equal(255, unchangedImage.GetPixel(5, 5).Red);
        Assert.Equal(0, fullyTintedImage.GetPixel(5, 5).Red);
        Assert.True(halfTintedImage.GetPixel(5, 5).Red > 0 && halfTintedImage.GetPixel(5, 5).Red < 255);
    }

    [Fact]
    public async Task Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage()
    {
        var options = new EpisodeBackgroundImageOptions { MaxWidth = 400, MaxHeight = 300 };
        var generator = CreateGenerator(options);
        var episode = new TVShowEpisode { Id = 1, Name = "Episode" };
        var fanartData = CreateTestImage(800, 600, SKColors.Orange);

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

    private static byte[] CreateTestImage(int width, int height, SKColor color)
        => VideoWebPlayer.Tests.Helpers.TestImages.SolidPng(width, height, color);

    private static VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator CreateGenerator(EpisodeBackgroundImageOptions? options = null)
        => new(Options.Create(options ?? new EpisodeBackgroundImageOptions()), NullLogger<VideoWebPlayer.Services.EpisodeBackgroundImage.EpisodeBackgroundImageGenerator>.Instance);
}
