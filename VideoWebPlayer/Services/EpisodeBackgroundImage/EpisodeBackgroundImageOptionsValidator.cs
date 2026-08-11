using Microsoft.Extensions.Options;

namespace VideoWebPlayer.Services.EpisodeBackgroundImage
{
    /// <summary>
    /// Validates <see cref="EpisodeBackgroundImageOptions"/> after configuration binding.
    /// </summary>
    public sealed class EpisodeBackgroundImageOptionsValidator : IValidateOptions<EpisodeBackgroundImageOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, EpisodeBackgroundImageOptions options)
        {
            if (options.MaxWidth <= 0)
                return ValidateOptionsResult.Fail("MaxWidth muss größer als 0 sein.");
            if (options.MaxHeight <= 0)
                return ValidateOptionsResult.Fail("MaxHeight muss größer als 0 sein.");
            if (string.IsNullOrWhiteSpace(options.TintColor))
                return ValidateOptionsResult.Fail("TintColor darf nicht leer sein.");
            if (options.TintOpacity < 0f || options.TintOpacity > 1f)
                return ValidateOptionsResult.Fail("TintOpacity muss zwischen 0.0 und 1.0 liegen.");
            if (options.CacheDurationMinutes < 0)
                return ValidateOptionsResult.Fail("CacheDurationMinutes darf nicht negativ sein.");
            if (options.JpegQuality < 0 || options.JpegQuality > 100)
                return ValidateOptionsResult.Fail("JpegQuality muss zwischen 0 und 100 liegen.");

            return ValidateOptionsResult.Success;
        }
    }
}
