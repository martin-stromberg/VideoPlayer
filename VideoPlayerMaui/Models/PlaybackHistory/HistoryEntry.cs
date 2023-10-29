using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Models.PlaybackHistory
{
    public class HistoryEntry
    {

        public MediaItem Item { get; set; }

        public BaseModel TypedItem { get; set; }

    }
}
