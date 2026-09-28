namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Result of adding media to a playlist.
    /// </summary>
    public class DtoPlaylistAddResult
    {
        /// <summary>
        /// Gets or sets the top-level entry that was added, e.g. the season or show entry that
        /// cascaded into <see cref="AddedEntries"/>.
        /// </summary>
        public DtoPlaylistEntry? TopLevelEntry { get; set; }

        /// <summary>
        /// Gets or sets the entries that were actually added to the playlist.
        /// </summary>
        public DtoPlaylistEntry[] AddedEntries { get; set; } =
            Array.Empty<DtoPlaylistEntry>();

        /// <summary>
        /// Gets or sets the number of entries that were skipped because they were already in the playlist.
        /// </summary>
        public int SkippedDuplicateCount { get; set; }

        /// <summary>
        /// Gets or sets a human-readable summary of the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
