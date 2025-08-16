using VideoWebPlayer.Data;

namespace VideoWebPlayer.Events
{
    public class MediaSourceDeletedEvent
    {
        public MediaSource Source { get; set; }
        public MediaSourceDeletedEvent(MediaSource source) => Source = source;
    }
}