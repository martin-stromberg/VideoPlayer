using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.PlaybackHistory
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
