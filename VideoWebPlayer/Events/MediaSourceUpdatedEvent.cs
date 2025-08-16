using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    public class MediaSourceUpdatedEvent
    {
        public MediaSource Source { get; set; }
        public MediaSourceUpdatedEvent(MediaSource source) => Source = source;
    }
}