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
        /// Gets or sets how many playlists <see cref="Services.PlaylistBackfillCoordinator"/> processes per
        /// work unit (block) - both for the marker-driven backfill (only playlists that contain a marked
        /// collection medium) and for the daily safety sweep. Kept deliberately small and bounded so a single
        /// unit stays short and even a large catch-up does not cause a load spike; between two blocks the
        /// coordinator pauses for <see cref="BackfillBlockPauseSeconds"/>. Values below 1 are treated as 1.
        /// </summary>
        public int BackfillBatchSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the pause, in seconds, between two blocks of <see cref="BackfillBatchSize"/> playlists
        /// (marker-driven backfill and safety sweep alike). Values below 0 are treated as 0 (no pause).
        /// </summary>
        public int BackfillBlockPauseSeconds { get; set; } = 2;

        /// <summary>
        /// Gets or sets how long, in seconds, <see cref="Services.PlaylistBackfillWorker"/> waits after it was
        /// woken (end of a scan, or media created outside a scan) before it processes the markers, so a burst
        /// of changes is handled in one go. Values below 0 are treated as 0.
        /// </summary>
        public int BackfillSettleSeconds { get; set; } = 10;

        /// <summary>
        /// Gets or sets the interval, in hours, of the daily safety sweep: a full check of every playlist
        /// that contains a TV show, season or movie collection, as a net against a forgotten marker (for
        /// example media created by a path that bypasses <c>SaveChanges</c>). The time of the last sweep is
        /// stored persistently, so it runs at most once per interval even across restarts, and an overdue
        /// sweep is caught up shortly after start. 0 (or less) switches the sweep off. Default: 24.
        /// </summary>
        public int BackfillSafetySweepIntervalHours { get; set; } = 24;

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
