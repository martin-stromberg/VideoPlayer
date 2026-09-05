namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a playlist is deleted.
    /// </summary>
    public class PlaylistDeletedEvent
    {
        /// <summary>
        /// Gets the identifier of the deleted playlist.
        /// </summary>
        public long PlaylistId { get; }

        /// <summary>
        /// Gets the identifier of the owning user.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistDeletedEvent"/> class.
        /// </summary>
        /// <param name="playlistId">The identifier of the deleted playlist.</param>
        /// <param name="userId">The identifier of the owning user.</param>
        public PlaylistDeletedEvent(long playlistId, string userId)
        {
            PlaylistId = playlistId;
            UserId = userId;
        }
    }
}
