using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.HomeBackgroundImage
{
    /// <summary>
    /// Generates a composite hero background image from the continue-watching list.
    /// </summary>
    public class HomeBackgroundImageGenerator
    {
        private const int DefaultMaxStrips = 5;

        private readonly ContinueWatchingService _continueWatchingService;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HomeBackgroundImageGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeBackgroundImageGenerator"/> class.
        /// </summary>
        public HomeBackgroundImageGenerator(
            ContinueWatchingService continueWatchingService,
            ApplicationDbContext db,
            ILogger<HomeBackgroundImageGenerator> logger)
        {
            _continueWatchingService = continueWatchingService;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Generates a JPEG composite background from the current user's continue-watching posters.
        /// </summary>
        public async Task<byte[]?> GenerateAsync(
            ClaimsPrincipal user,
            int targetWidth = 1600,
            int targetHeight = 520,
            int transition = 32,
            int quality = 85,
            CancellationToken cancellationToken = default)
        {
            var list = await _continueWatchingService.GetListAsync(user, cancellationToken);
            var pictureIds = list
                .Where(x => x.PosterPictureId.HasValue)
                .Take(DefaultMaxStrips)
                .Select(x => x.PosterPictureId!.Value)
                .ToList();

            if (pictureIds.Count == 0)
                return null;

            var pictures = await _db.Pictures
                .AsNoTracking()
                .Where(p => pictureIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Data, cancellationToken);

            var orderedData = pictureIds
                .Select(id => pictures.TryGetValue(id, out var data) ? data : null)
                .Where(data => data is not null && data.Length > 0)
                .ToList();

            if (orderedData.Count == 0)
                return null;

            try
            {
                return await Task.Run(
                    () => Compose(orderedData, targetWidth, targetHeight, transition, quality),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Generierung des Hero-Hintergrundbilds.");
                return null;
            }
        }

        private static byte[] Compose(List<byte[]> imageData, int targetWidth, int targetHeight, int transition, int quality)
        {
            var count = imageData.Count;
            var stripWidths = Distribute(targetWidth, count);
            if (count == 1)
                transition = 0;
            else
                transition = Math.Min(transition, stripWidths.Min() / 2);

            // Determine where each strip is drawn and how wide it is.
            var drawX = new int[count];
            var drawW = new int[count];
            var prefix = 0;
            for (var i = 0; i < count; i++)
            {
                drawX[i] = i == 0 ? 0 : prefix - transition;
                drawW[i] = stripWidths[i] + (i == 0 || i == count - 1 ? transition : 2 * transition);
                if (drawW[i] > targetWidth - drawX[i])
                    drawW[i] = targetWidth - drawX[i];
                prefix += stripWidths[i];
            }

            // Load and crop a center strip for each source.
            var strips = new List<Image<Rgba32>>(count);
            for (var i = 0; i < count; i++)
            {
                var strip = Image.Load<Rgba32>(imageData[i]);
                strip.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                    Size = new Size(drawW[i], targetHeight)
                }));
                strips.Add(strip);
            }

            using var canvas = new Image<Rgba32>(targetWidth, targetHeight);

            for (var y = 0; y < targetHeight; y++)
            {
                for (var x = 0; x < targetWidth; x++)
                {
                    var r = 0f;
                    var g = 0f;
                    var b = 0f;
                    var weight = 0f;

                    for (var i = 0; i < count; i++)
                    {
                        var lx = x - drawX[i];
                        if (lx < 0 || lx >= drawW[i])
                            continue;

                        var w = GetWeight(i, count, lx, drawW[i], transition);
                        if (w <= 0)
                            continue;

                        var source = strips[i][lx, y];
                        r += source.R * w;
                        g += source.G * w;
                        b += source.B * w;
                        weight += w;
                    }

                    if (weight > 0)
                    {
                        var inv = 1f / weight;
                        canvas[x, y] = new Rgba32(
                            (byte)Math.Clamp(r * inv, 0, 255),
                            (byte)Math.Clamp(g * inv, 0, 255),
                            (byte)Math.Clamp(b * inv, 0, 255),
                            255);
                    }
                    else
                    {
                        canvas[x, y] = new Rgba32(0, 0, 0, 255);
                    }
                }
            }

            foreach (var strip in strips)
                strip.Dispose();

            using var stream = new MemoryStream();
            canvas.SaveAsJpeg(stream, new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) });
            return stream.ToArray();
        }

        private static float GetWeight(int index, int count, int lx, int drawW, int transition)
        {
            if (transition <= 0)
                return 1f;

            var first = index == 0;
            var last = index == count - 1;

            if (first)
            {
                // First strip: opaque on the left, fade out on the right.
                var fadeStart = drawW - 2 * transition;
                if (lx < fadeStart)
                    return 1f;
                return (drawW - lx) / (2f * transition);
            }

            if (last)
            {
                // Last strip: fade in on the left, opaque on the right.
                if (lx < 2 * transition)
                    return lx / (2f * transition);
                return 1f;
            }

            // Interior strip: fade in on the left, fade out on the right.
            var rightFadeStart = drawW - 2 * transition;
            if (lx < 2 * transition)
                return lx / (2f * transition);
            if (lx >= rightFadeStart)
                return (drawW - lx) / (2f * transition);
            return 1f;
        }

        private static int[] Distribute(int total, int count)
        {
            var result = new int[count];
            var baseWidth = total / count;
            var remainder = total % count;
            for (var i = 0; i < count; i++)
                result[i] = baseWidth + (i < remainder ? 1 : 0);
            return result;
        }
    }
}
