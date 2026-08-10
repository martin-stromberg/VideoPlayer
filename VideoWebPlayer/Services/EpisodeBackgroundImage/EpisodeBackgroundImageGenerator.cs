using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.EpisodeBackgroundImage
{
    /// <summary>
    /// Performs the technical image processing required to generate an episode background image:
    /// loading, resizing, dominant color extraction, canvas creation and tint overlay application.
    /// </summary>
    public class EpisodeBackgroundImageGenerator
    {
        private const int DominantColorGridSize = 8;

        private readonly EpisodeBackgroundImageOptions _options;
        private readonly ILogger<EpisodeBackgroundImageGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeBackgroundImageGenerator"/> class.
        /// </summary>
        /// <param name="options">The episode background image options.</param>
        /// <param name="logger">Logger instance.</param>
        public EpisodeBackgroundImageGenerator(IOptions<EpisodeBackgroundImageOptions> options, ILogger<EpisodeBackgroundImageGenerator> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Generates the full background image for an episode from raw fanart image data.
        /// </summary>
        /// <param name="episode">The episode the background image is generated for.</param>
        /// <param name="fanartData">The raw fanart image bytes.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated <see cref="Picture"/>, or <c>null</c> if generation failed.</returns>
        public Task<Picture?> GenerateBackgroundImageAsync(TVShowEpisode episode, byte[] fanartData, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resized = ResizeImage(fanartData, _options.MaxWidth, _options.MaxHeight);
                var dominantColor = GetDominantColor(fanartData);
                var canvas = CreateCanvasWithScaledImage(resized, _options.MaxWidth, _options.MaxHeight, dominantColor);
                var tintColor = Color.ParseHex(_options.TintColor);
                var tinted = ApplyTintOverlay(canvas, tintColor, _options.TintOpacity);
                var jpegBytes = EncodeAsJpeg(tinted, _options.JpegQuality);

                var picture = new Picture
                {
                    Type = "background",
                    IsGeneratedBackground = true,
                    Data = jpegBytes,
                    ContentType = "image/jpeg",
                    Width = _options.MaxWidth,
                    Height = _options.MaxHeight,
                    Description = $"Generiertes Hintergrundbild für Episode {episode.Id}"
                };

                return Task.FromResult<Picture?>(picture);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (_options.EnableLogging)
                    _logger.LogError(ex, "Fehler bei der Generierung des Hintergrundbilds für Episode {EpisodeId}.", episode.Id);
                return Task.FromResult<Picture?>(null);
            }
        }

        /// <summary>
        /// Resizes the given image data proportionally so that it fits within the given maximum dimensions.
        /// </summary>
        /// <param name="imageData">The source image bytes.</param>
        /// <param name="maxWidth">The maximum width.</param>
        /// <param name="maxHeight">The maximum height.</param>
        /// <returns>The resized image, encoded as PNG.</returns>
        public byte[] ResizeImage(byte[] imageData, int maxWidth, int maxHeight)
        {
            using var image = Image.Load(imageData);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, maxHeight)
            }));
            return EncodeAsPng(image);
        }

        /// <summary>
        /// Calculates the dominant color of the image using an 8x8 sampling grid histogram.
        /// </summary>
        /// <param name="imageData">The source image bytes.</param>
        /// <returns>The dominant color.</returns>
        public Color GetDominantColor(byte[] imageData)
        {
            using var image = Image.Load<Rgba32>(imageData);

            var buckets = new Dictionary<int, (long R, long G, long B, int Count)>();

            for (var gx = 0; gx < DominantColorGridSize; gx++)
            {
                for (var gy = 0; gy < DominantColorGridSize; gy++)
                {
                    var x = Math.Clamp((int)((gx + 0.5) * image.Width / DominantColorGridSize), 0, image.Width - 1);
                    var y = Math.Clamp((int)((gy + 0.5) * image.Height / DominantColorGridSize), 0, image.Height - 1);
                    var pixel = image[x, y];

                    var bucketKey = ((pixel.R >> 4) << 8) | ((pixel.G >> 4) << 4) | (pixel.B >> 4);
                    buckets.TryGetValue(bucketKey, out var aggregate);
                    buckets[bucketKey] = (aggregate.R + pixel.R, aggregate.G + pixel.G, aggregate.B + pixel.B, aggregate.Count + 1);
                }
            }

            var dominant = buckets.Values.OrderByDescending(v => v.Count).First();
            return Color.FromRgb(
                (byte)(dominant.R / dominant.Count),
                (byte)(dominant.G / dominant.Count),
                (byte)(dominant.B / dominant.Count));
        }

        /// <summary>
        /// Creates a canvas of the given size filled with the background color and centers the source image on it.
        /// </summary>
        /// <param name="sourceImage">The already resized source image bytes.</param>
        /// <param name="canvasWidth">The canvas width.</param>
        /// <param name="canvasHeight">The canvas height.</param>
        /// <param name="backgroundColor">The background fill color.</param>
        /// <returns>The composed canvas, encoded as PNG.</returns>
        public byte[] CreateCanvasWithScaledImage(byte[] sourceImage, int canvasWidth, int canvasHeight, Color backgroundColor)
        {
            using var source = Image.Load(sourceImage);
            using var canvas = new Image<Rgba32>(canvasWidth, canvasHeight, backgroundColor.ToPixel<Rgba32>());

            var location = new Point((canvasWidth - source.Width) / 2, (canvasHeight - source.Height) / 2);
            canvas.Mutate(ctx => ctx.DrawImage(source, location, 1f));

            return EncodeAsPng(canvas);
        }

        /// <summary>
        /// Applies a translucent tint overlay across the entire image.
        /// </summary>
        /// <param name="imageData">The source image bytes.</param>
        /// <param name="tintColor">The tint color.</param>
        /// <param name="opacity">The tint opacity (0.0-1.0).</param>
        /// <returns>The tinted image, encoded as PNG.</returns>
        public byte[] ApplyTintOverlay(byte[] imageData, Color tintColor, float opacity)
        {
            using var image = Image.Load<Rgba32>(imageData);
            using var overlay = new Image<Rgba32>(image.Width, image.Height, tintColor.ToPixel<Rgba32>());
            image.Mutate(ctx => ctx.DrawImage(overlay, Math.Clamp(opacity, 0f, 1f)));
            return EncodeAsPng(image);
        }

        private static byte[] EncodeAsPng(Image image)
        {
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        private static byte[] EncodeAsJpeg(byte[] imageData, int quality)
        {
            using var image = Image.Load(imageData);
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) });
            return stream.ToArray();
        }
    }
}
