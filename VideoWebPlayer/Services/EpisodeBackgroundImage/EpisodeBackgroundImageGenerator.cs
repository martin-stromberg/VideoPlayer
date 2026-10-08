using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
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
        /// Generates the full background image for an episode from raw source image data
        /// (the episode's fanart, or its poster as a fallback when no fanart is available).
        /// </summary>
        /// <param name="episode">The episode the background image is generated for.</param>
        /// <param name="sourceImageData">The raw fanart or poster image bytes.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The generated <see cref="Picture"/>, or <c>null</c> if generation failed.</returns>
        public Task<Picture?> GenerateBackgroundImageAsync(TVShowEpisode episode, byte[] sourceImageData, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resized = ResizeImage(sourceImageData, _options.MaxWidth, _options.MaxHeight);
                var dominantColor = GetDominantColor(sourceImageData);
                var canvas = CreateCanvasWithScaledImage(resized, _options.MaxWidth, _options.MaxHeight, dominantColor);
                var tintColor = SKColor.Parse(_options.TintColor);
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
            using var image = Decode(imageData);

            var scale = Math.Min(maxWidth / (double)image.Width, maxHeight / (double)image.Height);
            var targetWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));

            using var resized = image.Resize(
                new SKImageInfo(targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            return EncodeAsPng(resized);
        }

        /// <summary>
        /// Calculates the dominant color of the image using an 8x8 sampling grid histogram.
        /// </summary>
        /// <param name="imageData">The source image bytes.</param>
        /// <returns>The dominant color.</returns>
        public SKColor GetDominantColor(byte[] imageData)
        {
            using var image = Decode(imageData);

            var buckets = new Dictionary<int, (long R, long G, long B, int Count)>();

            for (var gx = 0; gx < DominantColorGridSize; gx++)
            {
                for (var gy = 0; gy < DominantColorGridSize; gy++)
                {
                    var x = Math.Clamp((int)((gx + 0.5) * image.Width / DominantColorGridSize), 0, image.Width - 1);
                    var y = Math.Clamp((int)((gy + 0.5) * image.Height / DominantColorGridSize), 0, image.Height - 1);
                    var pixel = image.GetPixel(x, y);

                    var bucketKey = ((pixel.Red >> 4) << 8) | ((pixel.Green >> 4) << 4) | (pixel.Blue >> 4);
                    buckets.TryGetValue(bucketKey, out var aggregate);
                    buckets[bucketKey] = (aggregate.R + pixel.Red, aggregate.G + pixel.Green, aggregate.B + pixel.Blue, aggregate.Count + 1);
                }
            }

            var dominant = buckets.Values.OrderByDescending(v => v.Count).First();
            return new SKColor(
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
        public byte[] CreateCanvasWithScaledImage(byte[] sourceImage, int canvasWidth, int canvasHeight, SKColor backgroundColor)
        {
            using var source = Decode(sourceImage);
            using var canvasBitmap = new SKBitmap(canvasWidth, canvasHeight);
            canvasBitmap.Erase(backgroundColor);

            using var canvas = new SKCanvas(canvasBitmap);
            var x = (canvasWidth - source.Width) / 2;
            var y = (canvasHeight - source.Height) / 2;
            canvas.DrawBitmap(source, x, y, new SKSamplingOptions(SKCubicResampler.Mitchell));

            return EncodeAsPng(canvasBitmap);
        }

        /// <summary>
        /// Applies a translucent tint overlay across the entire image.
        /// </summary>
        /// <param name="imageData">The source image bytes.</param>
        /// <param name="tintColor">The tint color.</param>
        /// <param name="opacity">The tint opacity (0.0-1.0).</param>
        /// <returns>The tinted image, encoded as PNG.</returns>
        public byte[] ApplyTintOverlay(byte[] imageData, SKColor tintColor, float opacity)
        {
            using var image = Decode(imageData);
            using var canvas = new SKCanvas(image);

            var alpha = (byte)Math.Clamp(Math.Round(opacity * 255f), 0, 255);
            using var paint = new SKPaint { Color = tintColor.WithAlpha(alpha) };
            canvas.DrawRect(0, 0, image.Width, image.Height, paint);

            return EncodeAsPng(image);
        }

        /// <summary>
        /// Decodes the given image bytes, throwing when the data is not a decodable image.
        /// </summary>
        /// <param name="imageData">The encoded image bytes.</param>
        /// <returns>The decoded bitmap.</returns>
        /// <exception cref="InvalidDataException">The bytes are not a decodable image.</exception>
        private static SKBitmap Decode(byte[] imageData)
            => SKBitmap.Decode(imageData)
               ?? throw new InvalidDataException("Die Bilddaten konnten nicht dekodiert werden.");

        private static byte[] EncodeAsPng(SKBitmap bitmap)
            => Encode(bitmap, SKEncodedImageFormat.Png, 100);

        private static byte[] EncodeAsJpeg(byte[] imageData, int quality)
        {
            using var image = Decode(imageData);
            return Encode(image, SKEncodedImageFormat.Jpeg, quality);
        }

        private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, Math.Clamp(quality, 1, 100));
            return data.ToArray();
        }
    }
}
