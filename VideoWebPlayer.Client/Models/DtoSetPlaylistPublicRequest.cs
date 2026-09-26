namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request DTO for setting or clearing a playlist's "public" flag (Entwicklungsschritt 11).
    /// </summary>
    public class DtoSetPlaylistPublicRequest
    {
        /// <summary>
        /// Gets or sets the new value of the flag: <see langword="true"/> makes the playlist visible and
        /// playable (read-only) for every authorized user, <see langword="false"/> makes it private again.
        /// </summary>
        public bool IsPublic { get; set; }
    }
}
