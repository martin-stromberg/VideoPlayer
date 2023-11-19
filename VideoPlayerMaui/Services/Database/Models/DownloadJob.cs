using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public class DownloadJob: BaseDataModel
    {

        public long MediaItemId { get; set; }

        public long SourceId { get; set; }

        public DateTime EntryTime { get; set; }

    }
}
