namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response format of the playlist Next/Previous/Advance navigation endpoints (<c>/play/next</c>,
    /// <c>/play/previous</c>, <c>/play/advance</c>): the resolved entry together with its actual
    /// 1-based position in the playlist's current sort order, analogous to
    /// <see cref="DtoPlaylistPlaybackStart.CurrentPosition"/>. Carrying the position explicitly lets the
    /// client display the correct position even when the navigation skipped one or more non-playable or
    /// inaccessible entries to reach <see cref="Entry"/>.
    /// </summary>
    public class DtoPlaylistNavigationResult
    {
        /// <summary>
        /// Gets or sets the entry resolved by the navigation operation.
        /// </summary>
        public DtoPlaylistEntry Entry { get; set; } = null!;

        /// <summary>
        /// Gets or sets the 1-based position of <see cref="Entry"/> in the playlist's current sort order.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Gets or sets the playback position, in seconds, to resume <see cref="Entry"/>'s media at,
        /// populated from the matching <c>ContinueWatchingEntry.Position</c> (<c>0</c> if none exists).
        /// Carrying this directly - rather than requiring a separate roundtrip once the client has applied
        /// <see cref="Entry"/> - lets the target entry's stream URL and start position be applied to the
        /// video player together, synchronously, avoiding an intermediate render with mismatched state.
        /// </summary>
        public long StartPositionSeconds { get; set; }
    }
}
