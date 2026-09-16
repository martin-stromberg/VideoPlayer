using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// Validates a playlist cover image upload against the configured MIME type allowlist
    /// (<see cref="PlaylistSettings.AllowedCoverImageFormats"/>), maximum file size
    /// (<see cref="PlaylistSettings.MaxCoverImageSizeBytes"/>) and whether the file is a genuine,
    /// decodable image.
    /// </summary>
    public class PlaylistCoverValidator
    {
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
        /// <param name="settings">Playlist configuration, providing the allowed formats and maximum file size.</param>
        public PlaylistCoverValidator(IOptions<PlaylistSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <summary>
        /// Validates an uploaded playlist cover image: MIME type, file size and whether the bytes decode
        /// as a genuine image.
        /// </summary>
        /// <param name="fileContent">The raw uploaded file bytes.</param>
        /// <param name="contentType">The MIME type reported for the upload.</param>
        /// <param name="fileSize">The size, in bytes, of <paramref name="fileContent"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A successful <see cref="PlaylistCoverValidationResult"/> (carrying the image's dimensions) when
        /// every rule passes; otherwise a failed result with a user-facing German error message.
        /// </returns>
        public Task<PlaylistCoverValidationResult> ValidateUploadAsync(byte[] fileContent, string? contentType, long fileSize, CancellationToken cancellationToken = default)
        {
            var allowedFormats = GetAllowedFormats();

            if (string.IsNullOrWhiteSpace(contentType) || !allowedFormats.Contains(contentType.Trim()))
            {
                var friendlyName = GetFriendlyFormatName(contentType);
                var allowedList = string.Join(", ", allowedFormats.Select(GetFriendlyFormatName));
                return Task.FromResult(PlaylistCoverValidationResult.Failure(
                    $"Format {friendlyName} wird nicht unterstützt. Erlaubte Formate: {allowedList}."));
            }

            if (fileSize > _settings.MaxCoverImageSizeBytes)
            {
                var maxMegabytes = _settings.MaxCoverImageSizeBytes / (1024.0 * 1024.0);
                return Task.FromResult(PlaylistCoverValidationResult.Failure(
                    $"Datei zu groß, max. {maxMegabytes:0.#} MB erlaubt."));
            }

            if (fileContent is null || fileContent.Length == 0 || !TryGetImageDimensions(fileContent, out var width, out var height))
                return Task.FromResult(PlaylistCoverValidationResult.Failure("Datei ist kein gültiges Bild."));

            return Task.FromResult(PlaylistCoverValidationResult.Success(width, height));
        }

        private HashSet<string> GetAllowedFormats()
            => (_settings.AllowedCoverImageFormats ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        private static bool TryGetImageDimensions(byte[] fileContent, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                var info = Image.Identify(fileContent);
                if (info is null)
                    return false;

                width = info.Width;
                height = info.Height;
                return true;
            }
            catch (UnknownImageFormatException)
            {
                return false;
            }
            catch (InvalidImageContentException)
            {
                return false;
            }
        }
    }
}
