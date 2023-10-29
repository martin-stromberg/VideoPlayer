using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public class MediaItem: BaseDataModel
    {

        [AffectsEqualAttribute]
        public string Path { get; set; }

        public long ParentCollectionId { get; set; }

        public string MetaInfoJson { get; set; }

        public string PicturePath { get; set; }

        public DateTime MetaDataTime { get; set; }

        public bool MetaInfoChanged { get; set; }

        public long OriginalMediaItemId { get; set; }

        public int CopyType { get; set; }

        public DateTime LastConfirmation { get; set; }

        public TimeSpan LastPlaybackPosition { get; set; }

    }
}
