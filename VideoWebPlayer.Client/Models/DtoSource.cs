namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// A media source (library root), as returned to the client.
    /// </summary>
    public class DtoMediaSource : DtoMediaEntry
    {
        // Id and Name are inherited from DtoMediaEntry (same type/shape) - previously
        // redeclared here, which only hid the base members (CS0108) without adding anything.
        /// <summary>
        /// Erstellungszeitpunkt.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Zeitpunkt der letzten Klassifizierung
        /// </summary>
        public DateTime? ClassifiedAt { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the source has changed since it was last
        /// scanned or classified.
        /// </summary>
        public bool Changed { get; set; }
        /// <summary>
        /// Gets or sets the timestamp of the last scan of the source, or <c>null</c> if it has
        /// never been scanned.
        /// </summary>
        public DateTime? LastScannedAt { get; set; }

        /// <summary>
        /// Optional uploaded icon image (stored in `MediaSourceIcons` table).
        /// </summary>
        public long? IconPictureId { get; set; }
    }
    /// <summary>
    /// The genres associated with the media of a single source.
    /// </summary>
    public class SourceGenresDto
    {
        /// <summary>
        /// Gets or sets the identifier of the source.
        /// </summary>
        public long SourceId { get; set; }

        /// <summary>
        /// Gets or sets the name of the source.
        /// </summary>
        public string SourceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the genres found in the source's media.
        /// </summary>
        /// <value>The list of genres.</value>
        public List<GenreDto> Genres { get; set; } = new();
    }

    /// <summary>
    /// A genre, as returned to the client.
    /// </summary>
    public class GenreDto
    {
        /// <summary>
        /// Gets or sets the genre's unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the genre's name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL of the genre's icon image, or <c>null</c> if none is set.
        /// </summary>
        public string? IconUrl { get; set; }
    }
}
