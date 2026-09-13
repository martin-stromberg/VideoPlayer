namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Represents a single entry of the current user's "Continue Watching" list.
    /// </summary>
    public class ContinueWatchingDto
    {
        /// <summary>
        /// Gets or sets the media type of the entry.
        /// </summary>
        public string MediaType { get; set; } = "";

        /// <summary>
        /// Gets or sets the media entry.
        /// </summary>
        public DtoMediaEntry Entry { get; set; } = null!;

        /// <summary>
        /// Gets or sets the last known playback position, in seconds.
        /// </summary>
        public long PositionSeconds { get; set; }

        /// <summary>
        /// Gets or sets the total duration of the media, in seconds, or <c>null</c> if unknown.
        /// </summary>
        public long? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the title of the entry.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Gets or sets the id of the poster picture, or <c>null</c> if none is set.
        /// </summary>
        public long? PosterPictureId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp the entry was last watched at, or <c>null</c> if unknown.
        /// </summary>
        public DateTime? WatchedAt { get; set; }
    }
}
