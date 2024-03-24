using Mediathek.Services.Database;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Mediathek.Models.MediaItems
{
    public enum MediaItemCopyType
    {

        None,
        Cache,
        Download,
        Trailer

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

        public string PictureThumbnailPath
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

        public DateTime PictureTime
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

        [Path(nameof(PictureThumbnailPath))]
        [Path(nameof(PicturePath))]
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
                if (value == MediaItemCopyType.Download)
                    HasDownload = true;
            }
        }

        public DateTime DueDate
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                if (CopyType == MediaItemCopyType.Download)
                    SetProperty<DateTime>(value);
            }
        }

        public bool HasDownload
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

        public TimeSpan LastPlaybackPosition
        {
            get
            {
                return GetProperty<TimeSpan>();
            }
            set
            {
                SetProperty<TimeSpan>(value);
            }
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public MediaItem UpdatePath(string rootPath)
        {
            if (!Path.StartsWith(rootPath))
                Path = $"{rootPath}{Path}";
            return this;
        }

    }
}
