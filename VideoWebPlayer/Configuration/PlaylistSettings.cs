namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Strongly typed configuration options for the playlist feature.
    /// </summary>
    public class PlaylistSettings
    {
        /// <summary>
        /// Gets or sets the maximum number of playlists a single user may create. <c>null</c> means unlimited.
        /// </summary>
        public int? MaxPlaylistsPerUser { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries a single playlist may contain. <c>null</c> means unlimited.
        /// Enforced in <see cref="VideoWebPlayer.Services.PlaylistService.AddMediaToPlaylistAsync"/>, counting cascade entries.
        /// </summary>
        public int? MaxPlaylistItemCount { get; set; }

        /// <summary>
        /// Gets or sets the default page size used for paginated retrieval of playlist entries.
        /// </summary>
        public int DefaultPageSize { get; set; } = 20;

        /// <summary>
        /// Gets or sets the maximum page size allowed for paginated retrieval of playlist entries.
        /// </summary>
        public int MaxPageSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the interval, in minutes, at which <see cref="Services.PlaylistBackfillWorker"/> checks
        /// whether new content (e.g. a new season, episode or movie) needs to be backfilled into playlists
        /// that contain a complete TV show, season or movie collection. Values below 1 are treated as 1.
        /// </summary>
        public int BackfillIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets how many playlists <see cref="Services.PlaylistBackfillWorker"/> examines per run of
        /// <see cref="Services.PlaylistBackfillService.RunBatchAsync"/>. Kept deliberately small and bounded
        /// (rather than processing every eligible playlist in one run) so a single run stays short and does
        /// not noticeably affect ongoing operation; playlists are visited round-robin across successive runs
        /// so every playlist is eventually re-checked. Values below 1 are treated as 1.
        /// </summary>
        public int BackfillBatchSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the comma-separated list of MIME types accepted for playlist cover uploads
        /// (<see cref="Services.PlaylistCover.PlaylistCoverValidator"/>).
        /// </summary>
        public string AllowedCoverImageFormats { get; set; } = "image/jpeg,image/png,image/webp";

        /// <summary>
        /// Gets or sets the maximum accepted file size, in bytes, for playlist cover uploads.
        /// </summary>
        public long MaxCoverImageSizeBytes { get; set; } = 5 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum accepted width, in pixels, of an uploaded playlist cover image. Checked
        /// against the image header (before the image is fully decoded), so an image with absurd dimensions
        /// (decompression bomb) is rejected without being decoded. A value of 0 or less disables the check.
        /// </summary>
        public int MaxCoverImageWidthPixels { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the maximum accepted height, in pixels, of an uploaded playlist cover image. Checked
        /// against the image header, like <see cref="MaxCoverImageWidthPixels"/>. A value of 0 or less
        /// disables the check.
        /// </summary>
        public int MaxCoverImageHeightPixels { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the maximum accepted total number of pixels (width times height) of an uploaded
        /// playlist cover image, bounding the memory needed to decode it even when the width and height
        /// limits are raised. Checked against the image header, like <see cref="MaxCoverImageWidthPixels"/>.
        /// A value of 0 or less disables the check.
        /// </summary>
        public long MaxCoverImageTotalPixels { get; set; } = 4096L * 4096L;

        /// <summary>
        /// Gets or sets the target width, in pixels, of an automatically generated playlist cover collage
        /// (<see cref="Services.PlaylistCover.PlaylistCoverImageGenerator"/>).
        /// </summary>
        public int GeneratedCoverWidthPixels { get; set; } = 1600;

        /// <summary>
        /// Gets or sets the target height, in pixels, of an automatically generated playlist cover collage.
        /// </summary>
        public int GeneratedCoverHeightPixels { get; set; } = 520;

        /// <summary>
        /// Gets or sets the JPEG encoding quality (0-100) used for an automatically generated playlist cover collage.
        /// </summary>
        public int GeneratedCoverJpegQuality { get; set; } = 85;
    }
}
