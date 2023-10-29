using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.PlaybackHistory
{
    [DataModelReference(typeof(Services.Database.Models.PlaybackHistoryEntry))]
    public class HistoryEntry: BaseModel
    {

        public long MediaItemId { get; set; }

        public long TypedItemId { get; set; }

        public string Type { get; set; }

        public MediaItem Item { get; set; }

        public BaseModel TypedItem { get; set; }

    }
}
