using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Creates small synthetic PNG images of arbitrary size for tests that need extreme image proportions
/// (very tall, very wide, tiny) without shipping binary fixtures.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// Creates a PNG of the given size: a solid background, a contrasting rectangle in the middle and a white frame,
    /// so the result is visibly recognizable in screenshots and clearly not a solid colour.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The encoded PNG bytes.</returns>
    public static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var background = new Rgba32(70, 90, 40);
        var accent = new Rgba32(230, 90, 30);
        var frame = new Rgba32(255, 255, 255);
        var border = Math.Max(1, Math.Min(width, height) / 100);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isFrame = x < border || y < border || x >= width - border || y >= height - border;
                var isInner = x > width * 0.25 && x < width * 0.75 && y > height * 0.25 && y < height * 0.75;
                image[x, y] = isFrame ? frame : isInner ? accent : background;
            }
        }

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
