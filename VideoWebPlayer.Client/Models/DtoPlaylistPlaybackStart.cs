namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response format of the playlist playback start endpoint (<c>POST /api/playlists/{id}/play</c>):
    /// the resolved start entry together with its stream information and its position within the
    /// playlist's current sort order.
    /// </summary>
    public class DtoPlaylistPlaybackStart
    {
        /// <summary>
        /// Gets or sets the id of the playlist.
        /// </summary>
        public long PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the name of the playlist.
        /// </summary>
        public string PlaylistName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of (non-orphaned) entries of the playlist.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the 1-based position of the start entry in the playlist's current sort order.
        /// </summary>
        public int CurrentPosition { get; set; }

        /// <summary>
        /// Gets or sets the id of the resolved start entry.
        /// </summary>
        public long CurrentEntryId { get; set; }

        /// <summary>
        /// Gets or sets the resolved start entry.
        /// </summary>
        public DtoPlaylistEntry CurrentEntry { get; set; } = null!;

        /// <summary>
        /// Gets or sets the relative stream URL of the start entry's media, without an
        /// <c>access_token</c> query parameter (the client appends its own token before use).
        /// </summary>
        public string StreamUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start entry's media type in video player convention (e.g. <c>"movie"</c> or
        /// <c>"episode"</c>).
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the id of the start entry's media.
        /// </summary>
        public long MediaId { get; set; }

        /// <summary>
        /// Gets or sets the playback position, in seconds, to resume the start entry's media at, populated
        /// from the matching <c>ContinueWatchingEntry.Position</c> (<c>0</c> if none exists).
        /// </summary>
        public long StartPositionSeconds { get; set; }
    }
}
