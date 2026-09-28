using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// Validates a playlist cover image upload against the configured MIME type allowlist
    /// (<see cref="PlaylistSettings.AllowedCoverImageFormats"/>), maximum file size
    /// (<see cref="PlaylistSettings.MaxCoverImageSizeBytes"/>), maximum pixel dimensions
    /// (<see cref="PlaylistSettings.MaxCoverImageWidthPixels"/>, <see cref="PlaylistSettings.MaxCoverImageHeightPixels"/>,
    /// <see cref="PlaylistSettings.MaxCoverImageTotalPixels"/>) and whether the file is a genuine, completely
    /// decodable image of an allowed format.
    /// </summary>
    public class PlaylistCoverValidator
    {
        private const string NotAnImageMessage = "Datei ist kein gültiges Bild.";

        // Kurzform je MIME-Type fuer verstaendliche Fehlermeldungen (z. B. "BMP wird nicht unterstuetzt.").
        // Unbekannte MIME-Types fallen auf den Teil nach dem "/" in Grossbuchstaben zurueck.
        private static readonly Dictionary<string, string> FriendlyFormatNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "JPEG",
            ["image/jpg"] = "JPEG",
            ["image/png"] = "PNG",
            ["image/webp"] = "WebP",
            ["image/bmp"] = "BMP",
            ["image/gif"] = "GIF"
        };

        private readonly PlaylistSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCoverValidator"/> class.
        /// </summary>
        /// <param name="settings">Playlist configuration, providing the allowed formats, maximum file size and pixel limits.</param>
        public PlaylistCoverValidator(IOptions<PlaylistSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <summary>
        /// Validates an uploaded playlist cover image. The checks run from cheap to expensive, so a hostile
        /// upload is rejected as early as possible: (1) the MIME type reported by the client against the
        /// allowlist, (2) the file size, (3) the image header via <see cref="Image.Identify(ReadOnlySpan{byte})"/> -
        /// which yields the format that is actually contained in the bytes (checked against the same
        /// allowlist, instead of trusting the client-supplied MIME type) and the pixel dimensions, (4) the
        /// pixel limits - still purely on the header, so a decompression bomb is never decoded - and only
        /// then (5) a full, strict decode of the image, so truncated or corrupted files are rejected too.
        /// </summary>
        /// <param name="fileContent">The raw uploaded file bytes.</param>
        /// <param name="contentType">The MIME type reported for the upload.</param>
        /// <param name="fileSize">The size, in bytes, of <paramref name="fileContent"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A successful <see cref="PlaylistCoverValidationResult"/> (carrying the image's dimensions and its
        /// actual MIME type) when every rule passes; otherwise a failed result with a user-facing German
        /// error message.
        /// </returns>
        public Task<PlaylistCoverValidationResult> ValidateUploadAsync(byte[] fileContent, string? contentType, long fileSize, CancellationToken cancellationToken = default)
        {
            var allowedFormats = GetAllowedFormats();

            if (string.IsNullOrWhiteSpace(contentType) || !allowedFormats.Contains(contentType.Trim()))
                return Task.FromResult(UnsupportedFormat(contentType, allowedFormats));

            if (fileSize > _settings.MaxCoverImageSizeBytes)
            {
                var maxMegabytes = _settings.MaxCoverImageSizeBytes / (1024.0 * 1024.0);
                return Task.FromResult(PlaylistCoverValidationResult.Failure(
                    $"Datei zu groß, max. {maxMegabytes:0.#} MB erlaubt."));
            }

            if (fileContent is null || fileContent.Length == 0)
                return Task.FromResult(PlaylistCoverValidationResult.Failure(NotAnImageMessage));

            var info = TryIdentify(fileContent);
            if (info is null || info.Width <= 0 || info.Height <= 0)
                return Task.FromResult(PlaylistCoverValidationResult.Failure(NotAnImageMessage));

            // The client-supplied MIME type is only a claim: what counts is the format the bytes really
            // contain (a GIF sent as "image/png" must not pass just because "image/png" is allowed).
            var actualMimeType = info.Metadata.DecodedImageFormat?.DefaultMimeType;
            if (string.IsNullOrWhiteSpace(actualMimeType) || !allowedFormats.Contains(actualMimeType))
                return Task.FromResult(UnsupportedFormat(actualMimeType, allowedFormats));

            var dimensionError = CheckDimensions(info.Width, info.Height);
            if (dimensionError is not null)
                return Task.FromResult(PlaylistCoverValidationResult.Failure(dimensionError));

            // ImageSharp's JPEG decoder silently renders truncated/destroyed JPEGs as partly grey images
            // instead of failing, so JPEGs additionally get a strict integrity check (memory-free, it does
            // not reconstruct pixels). The PNG decoder already fails on incomplete/damaged data; the WebP
            // decoder does NOT fail on a truncated file, so WebP is checked against the length its RIFF
            // header declares.
            var jpegCorrupt = string.Equals(actualMimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                              && JpegIntegrityChecker.Check(fileContent) == JpegIntegrity.Corrupt;
            var webpTruncated = string.Equals(actualMimeType, "image/webp", StringComparison.OrdinalIgnoreCase)
                                && IsRiffTruncated(fileContent);
            if (jpegCorrupt || webpTruncated || !CanBeDecoded(fileContent))
            {
                return Task.FromResult(PlaylistCoverValidationResult.Failure(
                    "Datei ist beschädigt oder unvollständig und kann nicht als Bild gelesen werden."));
            }

            return Task.FromResult(PlaylistCoverValidationResult.Success(info.Width, info.Height, actualMimeType));
        }

        /// <summary>
        /// Whether a RIFF container (WebP) is shorter than the size its own header declares, i.e. was cut off.
        /// Trailing bytes after the declared end are tolerated; only a file that is too short is rejected.
        /// </summary>
        /// <param name="data">The raw uploaded file bytes.</param>
        /// <returns><see langword="true"/> if the file is shorter than its RIFF header declares.</returns>
        private static bool IsRiffTruncated(byte[] data)
        {
            if (data.Length < 12)
                return true;

            var declaredPayloadSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
            return (long)declaredPayloadSize + 8 > data.Length;
        }

        private HashSet<string> GetAllowedFormats()
            => (_settings.AllowedCoverImageFormats ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static PlaylistCoverValidationResult UnsupportedFormat(string? mimeType, HashSet<string> allowedFormats)
        {
            var friendlyName = GetFriendlyFormatName(mimeType);
            var allowedList = string.Join(", ", allowedFormats.Select(GetFriendlyFormatName));
            return PlaylistCoverValidationResult.Failure(
                $"Format {friendlyName} wird nicht unterstützt. Erlaubte Formate: {allowedList}.");
        }

        private static string GetFriendlyFormatName(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return "unbekannt";

            if (FriendlyFormatNames.TryGetValue(contentType.Trim(), out var friendlyName))
                return friendlyName;

            var slashIndex = contentType.LastIndexOf('/');
            var suffix = slashIndex >= 0 && slashIndex < contentType.Length - 1 ? contentType[(slashIndex + 1)..] : contentType;
            return suffix.ToUpperInvariant();
        }

        /// <summary>
        /// Checks the header dimensions against the configured pixel limits.
        /// </summary>
        /// <param name="width">The image width, in pixels, as read from the header.</param>
        /// <param name="height">The image height, in pixels, as read from the header.</param>
        /// <returns>A user-facing error message, or <see langword="null"/> when the dimensions are within the limits.</returns>
        private string? CheckDimensions(int width, int height)
        {
            var maxWidth = _settings.MaxCoverImageWidthPixels;
            var maxHeight = _settings.MaxCoverImageHeightPixels;
            var maxTotal = _settings.MaxCoverImageTotalPixels;

            if ((maxWidth > 0 && width > maxWidth) || (maxHeight > 0 && height > maxHeight))
            {
                var widthLimit = maxWidth > 0 ? maxWidth.ToString() : "unbegrenzt viele";
                var heightLimit = maxHeight > 0 ? maxHeight.ToString() : "unbegrenzt viele";
                return $"Bild zu groß ({width} x {height} Pixel). Erlaubt sind höchstens {widthLimit} x {heightLimit} Pixel. " +
                       "Bitte verkleinern Sie das Bild.";
            }

            if (maxTotal > 0 && (long)width * height > maxTotal)
            {
                var maxMegapixels = maxTotal / 1_000_000.0;
                return $"Bild zu groß ({width} x {height} Pixel). Erlaubt sind höchstens {maxMegapixels:0.#} Megapixel. " +
                       "Bitte verkleinern Sie das Bild.";
            }

            return null;
        }

        /// <summary>
        /// Whether <paramref name="ex"/>, thrown while reading untrusted upload bytes, means "these bytes are
        /// not a decodable image". Deliberately broad: ImageSharp signals damaged input via several unrelated
        /// exception types (unknown format, invalid content, index/overflow errors of a lenient decoder, ...),
        /// and every one of them must lead to a clean rejection instead of a 500. Only cancellation is
        /// let through.
        /// </summary>
        /// <param name="ex">The exception thrown while identifying or decoding the upload.</param>
        /// <returns><see langword="true"/> if the exception is to be treated as "not a valid image".</returns>
        private static bool IsImageDecodingFailure(Exception ex)
            => ex is not OperationCanceledException;

        /// <summary>
        /// Reads only the image header.
        /// </summary>
        /// <param name="fileContent">The raw uploaded file bytes.</param>
        /// <returns>The image info, or <see langword="null"/> when the bytes are not a recognisable image.</returns>
        private static ImageInfo? TryIdentify(byte[] fileContent)
        {
            try
            {
                return Image.Identify(fileContent);
            }
            catch (Exception ex) when (IsImageDecodingFailure(ex))
            {
                // A header ImageSharp cannot parse (unknown format, invalid dimensions, ...): not an image.
                return null;
            }
        }

        /// <summary>
        /// Fully decodes the (first frame of the) image, strictly - corrupted segments/checksums are not
        /// tolerated - to make sure a truncated or damaged file is not accepted. Must only be called after
        /// the pixel limits were checked against the header, since decoding allocates memory for every pixel.
        /// </summary>
        /// <param name="fileContent">The raw uploaded file bytes.</param>
        /// <returns><see langword="true"/> if the image decoded completely; otherwise <see langword="false"/>.</returns>
        private static bool CanBeDecoded(byte[] fileContent)
        {
            try
            {
                var options = new DecoderOptions
                {
                    MaxFrames = 1,
                    SkipMetadata = true
                };
                using var image = Image.Load(options, fileContent);
                return true;
            }
            catch (Exception ex) when (IsImageDecodingFailure(ex))
            {
                return false;
            }
        }
    }
}
