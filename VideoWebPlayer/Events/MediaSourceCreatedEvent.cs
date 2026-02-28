using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    /// <summary>
    /// Event raised when a media source is created.
    /// </summary>
    public class MediaSourceCreatedEvent
    {
        /// <summary>
        /// Gets or sets the created media source.
        /// </summary>
        public MediaSource Source { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceCreatedEvent"/> class.
        /// </summary>
        /// <param name="source">The created media source.</param>
        public MediaSourceCreatedEvent(MediaSource source) => Source = source;
    }
}