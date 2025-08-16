using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Basisklasse für alle Medieneinträge (Quelle, Verzeichnis, Datei).
    /// </summary>
    public abstract class MediaEntry
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
        /// Pfad oder Verbindungsinformation.
        /// </summary>
        public string Path { get; set; } = string.Empty;

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
}