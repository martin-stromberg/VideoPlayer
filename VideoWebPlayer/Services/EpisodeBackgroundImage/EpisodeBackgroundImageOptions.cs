namespace VideoWebPlayer.Services.EpisodeBackgroundImage
{
    /// <summary>
    /// Strongly typed configuration options for the episode background image generation feature.
    /// </summary>
    public sealed class EpisodeBackgroundImageOptions
    {
        /// <summary>
        /// Gets or sets the maximum width of the generated canvas.
        /// </summary>
        public int MaxWidth { get; set; } = 1920;
        /// <summary>
        /// Gets or sets the maximum height of the generated canvas.
        /// </summary>
        public int MaxHeight { get; set; } = 1080;
        /// <summary>
        /// Gets or sets the tint overlay color as a hex string.
        /// </summary>
        public string TintColor { get; set; } = "#000000";
        /// <summary>
        /// Gets or sets the tint overlay opacity (0.0-1.0).
        /// </summary>
        public float TintOpacity { get; set; } = 0.4f;
        /// <summary>
        /// Gets or sets the duration in minutes generated image ids remain in the in-memory cache.
        /// </summary>
        public int CacheDurationMinutes { get; set; } = 60;
        /// <summary>
        /// Gets or sets the JPEG compression quality (0-100).
        /// </summary>
        public int JpegQuality { get; set; } = 85;
        /// <summary>
        /// Gets or sets a value indicating whether generation errors and warnings are logged.
        /// </summary>
        public bool EnableLogging { get; set; } = true;
    }
}
