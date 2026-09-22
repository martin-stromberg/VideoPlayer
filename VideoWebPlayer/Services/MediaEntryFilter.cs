namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Gemeinsame Filterregeln für Verzeichniseinträge der MediaSource-Reader.
    /// </summary>
    internal static class MediaEntryFilter
    {
        /// <summary>
        /// Prüft, ob ein Verzeichniseintrag beim Einlesen übergangen wird (Navigationseinträge und versteckte Einträge wie '.actors').
        /// </summary>
        public static bool IsIgnoredEntry(string name)
        {
            return name.StartsWith('.');
        }
    }
}
