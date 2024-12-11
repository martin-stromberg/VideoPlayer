using Newtonsoft.Json;

namespace VideoPlayer.Service.Library.Models
{

    public enum MediaItemCopyType
    {

        Original = 0,
        Trailer = 100,
        Cache = 200,
        Download = 201

    }

    public class MediaItem: BaseServiceModel
    {
        private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };
        public MediaItem()
            : base() { }


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
        
        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public DateTime LastAccess
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public bool Classified
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty(value);
            }
        }


        public DateTime LastMetaInformationUpdate
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public DateTime LastClassificationTry {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public DateTime DueDate {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public bool NeedsPictureUpdate {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public DateTime LastPictureUpdateTry {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public TimeSpan LastPosition {
            get
            {
                return GetProperty<TimeSpan>();
            }
            set
            {
                SetProperty(value);
            }
        }

        

    }
}
