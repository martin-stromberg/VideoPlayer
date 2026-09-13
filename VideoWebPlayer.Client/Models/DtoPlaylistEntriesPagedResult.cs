namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Represents a single, sorted page of a playlist's entries.
    /// </summary>
    public class DtoPlaylistEntriesPagedResult
    {
        /// <summary>
        /// Gets or sets the entries of the requested page.
        /// </summary>
        public DtoPlaylistEntry[] Entries { get; set; } =
            Array.Empty<DtoPlaylistEntry>();

        /// <summary>
        /// Gets or sets the total number of entries of the playlist, across all pages.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets whether a further page follows this one.
        /// </summary>
        public bool HasNextPage { get; set; }

        /// <summary>
        /// Gets or sets the 1-based number of the returned page.
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entries per page.
        /// </summary>
        public int PageSize { get; set; }
    }
}
