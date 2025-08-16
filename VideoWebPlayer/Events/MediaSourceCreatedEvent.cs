using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    public class MediaSourceCreatedEvent
    {
        public MediaSource Source { get; set; }
        public MediaSourceCreatedEvent(MediaSource source) => Source = source;
    }
}