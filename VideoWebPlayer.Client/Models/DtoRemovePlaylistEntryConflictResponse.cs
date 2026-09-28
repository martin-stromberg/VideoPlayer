namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response returned when removing an entry from a playlist would affect a continue-watching
    /// (Weiterschauen) entry bound to the same playlist and requires the caller to confirm.
    /// </summary>
    public class DtoRemovePlaylistEntryConflictResponse
    {
        /// <summary>
        /// Gets or sets whether the caller must confirm the removal, because a continue-watching entry
        /// referencing this playlist entry (bound to the same playlist) exists.
        /// </summary>
        public bool IsContinueWatchingConfirmationRequired { get; set; }
    }
}
