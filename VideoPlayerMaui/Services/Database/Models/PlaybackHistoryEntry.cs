using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public  class PlaybackHistoryEntry: BaseDataModel
    {

        public long MediaItemId { get; set; }

        public long TypedItemId { get; set; }

        public string Type { get; set; }

        public bool Deleted { get; set; }

    }
}
