namespace VideoWebPlayer.Client.Models
{
    public class DtoMediaSource: DtoMediaEntry
    {
        /// <summary>
        /// Eindeutige ID des Eintrags.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Anzeigename des Eintrags.
        /// </summary>
        public string Name { get; set; } = string.Empty;
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
    }
    public class SourceGenresDto
    {
        public long SourceId { get; set; }
        public string SourceName { get; set; }
        public List<GenreDto> Genres { get; set; } = new();
    }

    public class GenreDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }
}
