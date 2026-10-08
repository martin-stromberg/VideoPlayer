using SkiaSharp;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Creates small synthetic images of arbitrary size for tests that need extreme image proportions
/// (very tall, very wide, tiny) or specific formats without shipping binary fixtures.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// Gets the bytes of a minimal valid GIF (a 1x1 transparent pixel). SkiaSharp does not encode GIF,
    /// so tests that need GIF content use this constant.
    /// </summary>
    /// <returns>The encoded GIF bytes.</returns>
    public static byte[] Gif => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    /// <summary>
    /// Creates a PNG of the given size: a solid background, a contrasting rectangle in the middle and a white frame,
    /// so the result is visibly recognizable in screenshots and clearly not a solid colour.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The encoded PNG bytes.</returns>
    public static byte[] Png(int width, int height)
    {
        var background = new SKColor(70, 90, 40);
        var accent = new SKColor(230, 90, 30);
        var frame = new SKColor(255, 255, 255);
        var border = Math.Max(1, Math.Min(width, height) / 100);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);

        using var paint = new SKPaint { Color = accent };
        canvas.DrawRect(width * 0.25f, height * 0.25f, width * 0.5f, height * 0.5f, paint);

        paint.Color = frame;
        canvas.DrawRect(0, 0, width, border, paint);
        canvas.DrawRect(0, height - border, width, border, paint);
        canvas.DrawRect(0, 0, border, height, paint);
        canvas.DrawRect(width - border, 0, border, height, paint);

        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>
    /// Creates a single-colour PNG of the given size.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="color">The fill colour.</param>
    /// <returns>The encoded PNG bytes.</returns>
    public static byte[] SolidPng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>
    /// Creates a single-colour JPEG of the given size.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="color">The fill colour.</param>
    /// <returns>The encoded JPEG bytes.</returns>
    public static byte[] SolidJpeg(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return Encode(bitmap, SKEncodedImageFormat.Jpeg);
    }

    /// <summary>
    /// Creates a bitmap of pseudo-random pixels (fixed seed), so encoded files are large enough that
    /// truncating or damaging them actually removes/destroys pixel data.
    /// </summary>
    /// <param name="width">The image width, in pixels.</param>
    /// <param name="height">The image height, in pixels.</param>
    /// <returns>The noise bitmap.</returns>
    public static SKBitmap Noise(int width, int height)
    {
        var random = new Random(42);
        var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255));
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Encodes a bitmap into the given format (the quality parameter applies to lossy formats).
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <param name="format">The target image format.</param>
    /// <param name="quality">Encoder quality (1-100).</param>
    /// <returns>The encoded image bytes.</returns>
    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 90)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>
    /// Encodes a bitmap as lossless WebP.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <returns>The encoded WebP bytes.</returns>
    public static byte[] EncodeLosslessWebp(SKBitmap bitmap)
    {
        var options = new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100);
        using var pixmap = bitmap.PeekPixels();
        using var stream = new MemoryStream();
        SKWebpEncoder.Encode(stream, pixmap, options);
        return stream.ToArray();
    }
}
