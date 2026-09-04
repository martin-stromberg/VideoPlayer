namespace VideoWebPlayer.Client.Models
{
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
        public bool Changed { get; set; }
        public DateTime? LastScannedAt { get; set; }

        /// <summary>
        /// Optional uploaded icon image (stored in `MediaSourceIcons` table).
        /// </summary>
        public long? IconPictureId { get; set; }
    }
    public class SourceGenresDto
    {
        public long SourceId { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public List<GenreDto> Genres { get; set; } = new();
    }

    public class GenreDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
    }
}
