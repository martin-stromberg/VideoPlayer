using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a media source is updated.
    /// </summary>
    public class MediaSourceUpdatedEvent
    {
        /// <summary>
        /// Gets or sets the updated media source.
        /// </summary>
        public MediaSource Source { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceUpdatedEvent"/> class.
        /// </summary>
        /// <param name="source">The updated media source.</param>
        public MediaSourceUpdatedEvent(MediaSource source) => Source = source;
    }
}