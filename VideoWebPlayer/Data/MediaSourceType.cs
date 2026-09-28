namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Diskriminator für den Typ einer Medienquelle.
    /// </summary>
    public enum MediaSourceType
    {
        /// <summary>
        /// Entfernter SFTP-Server (Default für Bestandsdaten).
        /// </summary>
        Sftp = 0,

        /// <summary>
        /// Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade).
        /// </summary>
        LocalDirectory = 1
    }
}
