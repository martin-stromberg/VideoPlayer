namespace VideoWebPlayer.Controllers.Models
{
    /// <summary>
    /// Daten-Transfer-Objekt fuer MediaEntry.
    /// </summary>
    public class MediaEntryDto
    {
        /// <summary>
        /// Gets or sets the media type (e.g. movie or TV show collection).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the id of the media entry.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the title of the media entry.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the media entry.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of the media entry.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp of the media entry.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the id of the poster picture, or <c>null</c> if none is set.
        /// </summary>
        public long? PictureId { get; set; } // oder PosterPictureId

        /// <summary>
        /// Gets or sets the number of items contained in the media entry.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp the media entry was last watched at, or <c>null</c> if it was never watched.
        /// </summary>
        public DateTime? WatchedAt { get; set; }

    }
}
