using Newtonsoft.Json;
using System;
using System.Linq;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.MediaItems
{
    public enum MediaItemCopyType
    {

        None,
        Cache

    }

    [DataModelReference(typeof(Services.Database.Models.MediaItem))]
    public class MediaItem: BaseModel
    {

        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public long ParentCollectionId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public MediaInformation MetaInfo
        {
            get
            {
                return GetProperty<MediaInformation>();
            }
            set
            {
                SetProperty<MediaInformation>(value);
                MetaInfoChanged = true;
            }
        }

        public bool MetaInfoChanged
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public string PicturePath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public ImageSource Picture
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public DateTime MetaDataTime
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public long OriginalMediaItemId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public MediaItemCopyType CopyType
        {
            get
            {
                return GetProperty<MediaItemCopyType>();
            }
            set
            {
                SetProperty<MediaItemCopyType>(value);
            }
        }

        public DateTime LastConfirmation
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}
