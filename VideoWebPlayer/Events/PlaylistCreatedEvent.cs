using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a playlist is created.
    /// </summary>
    public class PlaylistCreatedEvent
    {
        /// <summary>
        /// Gets the created playlist.
        /// </summary>
        public Playlist Playlist { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCreatedEvent"/> class.
        /// </summary>
        /// <param name="playlist">The created playlist.</param>
        public PlaylistCreatedEvent(Playlist playlist) => Playlist = playlist;
    }
}
