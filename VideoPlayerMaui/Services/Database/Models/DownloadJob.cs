using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.Database.Models
{
    public class DownloadJob: BaseDataModel
    {

        public long MediaItemId { get; set; }

        public long SourceId { get; set; }

        public DateTime EntryTime { get; set; }
        public MediaItemCopyType CopyType { get; set; }
        public bool Failed { get; set; }
    }
}
