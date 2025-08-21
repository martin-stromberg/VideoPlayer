namespace VideoWebPlayer.Client.Models
{
    public class DtoMediaEntry 
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
    }
    public class DtoMediaSource: DtoMediaEntry
    {
        public DateTime? LastScannedAt { get; set; }
    }
}
