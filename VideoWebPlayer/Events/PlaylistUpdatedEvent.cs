using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a playlist is updated.
    /// </summary>
    public class PlaylistUpdatedEvent
    {
        /// <summary>
        /// Gets the updated playlist.
        /// </summary>
        public Playlist Playlist { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistUpdatedEvent"/> class.
        /// </summary>
        /// <param name="playlist">The updated playlist.</param>
        public PlaylistUpdatedEvent(Playlist playlist) => Playlist = playlist;
    }
}
