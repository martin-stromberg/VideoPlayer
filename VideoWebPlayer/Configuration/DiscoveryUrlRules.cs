namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Shared rule for the public base URL reported in discovery responses. The same rule
    /// applies to the file/environment configuration (<see cref="DiscoveryOptions"/>), the
    /// admin input on <c>/admin/program-settings</c> and the server-side persistence check.
    /// </summary>
    internal static class DiscoveryUrlRules
    {
        /// <summary>
        /// The shared wording of the rule violation message ("…muss eine absolute
        /// http- oder https-URL sein…" including the example). Call sites prepend their
        /// context-specific subject (configuration key, field name).
        /// </summary>
        /// <value>The message fragment without leading subject.</value>
        public const string PublicBaseUrlRuleText =
            "muss eine absolute http- oder https-URL sein (z. B. \"https://videos.example.com/videoplayer/\").";

        /// <summary>
        /// Returns whether the given value is a valid public base URL: <c>null</c> or empty
        /// is allowed (it means "derive the address automatically"), any other value must be
        /// an absolute <c>http</c>/<c>https</c> URI.
        /// </summary>
        /// <param name="value">The configured or entered URL.</param>
        /// <returns><see langword="true"/> when the value is empty or an absolute HTTP(S) URL.</returns>
        public static bool IsValidPublicBaseUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// Normalizes a public base URL value: trims whitespace and maps empty input to
        /// <c>null</c>.
        /// </summary>
        /// <param name="value">The configured or entered URL.</param>
        /// <returns>The trimmed URL, or <c>null</c> when the input was empty.</returns>
        public static string? NormalizePublicBaseUrl(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
