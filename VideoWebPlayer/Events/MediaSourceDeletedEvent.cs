using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a media source is deleted.
    /// </summary>
    public class MediaSourceDeletedEvent
    {
        /// <summary>
        /// Gets or sets the deleted media source.
        /// </summary>
        public MediaSource Source { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceDeletedEvent"/> class.
        /// </summary>
        /// <param name="source">The deleted media source.</param>
        public MediaSourceDeletedEvent(MediaSource source) => Source = source;
    }
}