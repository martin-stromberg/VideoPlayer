using SkiaSharp;

namespace VideoWebPlayer.Services.DemoData;

/// <summary>
/// Generates small but visually appealing JPEG artwork (poster, fanart, banner,
/// episode stills) for demo media entries at runtime using SkiaSharp.
/// </summary>
public sealed class DemoMediaImageGenerator
{
    private const int JpegQuality = 80;

    /// <summary>
    /// Creates a poster image (2:3 aspect, 800x1200) for a movie or TV show.
    /// </summary>
    /// <param name="title">The title to draw on the image.</param>
    /// <returns>JPEG-encoded image bytes.</returns>
    public byte[] CreatePoster(string title) => Create(title, 800, 1200);

    /// <summary>
    /// Creates a fanart/backdrop image (16:9, 1280x720) for a movie or TV show.
    /// </summary>
    /// <param name="title">The title to draw on the image.</param>
    /// <returns>JPEG-encoded image bytes.</returns>
    public byte[] CreateFanart(string title) => Create(title, 1280, 720);

    /// <summary>
    /// Creates a wide banner image (1000x185) for a TV show.
    /// </summary>
    /// <param name="title">The title to draw on the image.</param>
    /// <returns>JPEG-encoded image bytes.</returns>
    public byte[] CreateBanner(string title) => Create(title, 1000, 185);

    /// <summary>
    /// Creates a still-frame style image (16:9, 640x360) for an episode.
    /// </summary>
    /// <param name="title">The title to draw on the image.</param>
    /// <returns>JPEG-encoded image bytes.</returns>
    public byte[] CreateEpisodeImage(string title) => Create(title, 640, 360);

    /// <summary>
    /// Creates a JPEG image with a deterministic two-tone gradient derived from the
    /// title, a dark bottom scrim and the centered title text.
    /// </summary>
    /// <param name="title">The title to draw on the image.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>JPEG-encoded image bytes.</returns>
    public byte[] Create(string title, int width, int height)
    {
        var palette = PaletteFor(title);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        using (var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, height),
            palette,
            null,
            SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = shader, IsAntialias = true })
        {
            canvas.DrawRect(0, 0, width, height, paint);
        }

        // Dezente Diagonal-Struktur, damit das Bild nicht flach wirkt.
        using (var stripePaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 18),
            IsAntialias = true
        })
        {
            var stripeWidth = width / 8f;
            for (var x = (float)-height; x < width; x += stripeWidth * 2)
            {
                using var path = new SKPath();
                path.MoveTo(x, 0);
                path.LineTo(x + stripeWidth, 0);
                path.LineTo(x + stripeWidth + height, height);
                path.LineTo(x + height, height);
                path.Close();
                canvas.DrawPath(path, stripePaint);
            }
        }

        // Dunkler Verlauf unten für lesbaren Titeltext.
        using (var scrim = SKShader.CreateLinearGradient(
            new SKPoint(0, height * 0.55f),
            new SKPoint(0, height),
            new[] { SKColors.Black.WithAlpha(0), SKColors.Black.WithAlpha(190) },
            null,
            SKShaderTileMode.Clamp))
        using (var scrimPaint = new SKPaint { Shader = scrim, IsAntialias = true })
        {
            canvas.DrawRect(0, 0, width, height, scrimPaint);
        }

        DrawTitle(canvas, title, width, height);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }

    /// <summary>
    /// Computes a deterministic start/end color pair from the title so that each
    /// demo entry gets a distinct, stable palette.
    /// </summary>
    /// <param name="title">The title the palette is derived from.</param>
    /// <returns>The gradient start and end colors (index 0 = start, index 1 = end).</returns>
    private static SKColor[] PaletteFor(string title)
    {
        var hash = 0;
        foreach (var c in title)
            hash = unchecked(hash * 31 + c);

        var hue = (float)((hash & int.MaxValue) % 360);
        var start = SKColor.FromHsl(hue, 62f, 42f);
        var end = SKColor.FromHsl((hue + 40f) % 360f, 70f, 24f);
        return new[] { start, end };
    }

    private static void DrawTitle(SKCanvas canvas, string title, int width, int height)
    {
        var text = string.IsNullOrWhiteSpace(title) ? "Demo" : title.Trim();
        var maxTextWidth = width * 0.86f;
        var maxFontSize = height * 0.16f;

        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, maxFontSize);
        while (font.Size > 10f && font.MeasureText(text, paint) > maxTextWidth)
            font.Size -= 2f;

        var textWidth = font.MeasureText(text, paint);
        var x = (width - textWidth) / 2f;
        var y = height - Math.Max(14f, height * 0.07f);
        canvas.DrawText(text, x, y, font, paint);
    }
}
