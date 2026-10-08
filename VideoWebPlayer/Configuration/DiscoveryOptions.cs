namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Strongly typed configuration options for the UDP discovery response (the
    /// <c>Discovery</c> configuration section).
    /// </summary>
    public sealed class DiscoveryOptions
    {
        /// <summary>
        /// Gets or sets the public base URL the server reports in
        /// <c>VIDEOWEBPLAYER_SERVER</c> discovery answers — complete with scheme, host, port
        /// and path (e.g. <c>https://example.com/videoplayer/</c>). <c>null</c> derives the
        /// address at runtime; the admin-maintained value
        /// <c>Setup.DiscoveryPublicBaseUrl</c> takes precedence when set.
        /// </summary>
        public string? PublicBaseUrl { get; set; }
    }
}
