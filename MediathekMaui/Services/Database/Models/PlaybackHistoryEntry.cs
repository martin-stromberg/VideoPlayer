using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public  class PlaybackHistoryEntry: BaseDataModel
    {

        public long MediaItemId { get; set; }

        public long TypedItemId { get; set; }

        public long PlaylistId { get; set; }

        public string Type { get; set; }

        public bool Deleted { get; set; }

    }
}
