using Newtonsoft.Json;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    
    public enum MediaItemCopyType
    {

        Original = 0,
        Trailer = 100,
        Cache = 200,
        Download = 201

    }

    [DataModelReference(typeof(MediaDataItem))]
    public class MediaItem: BaseServiceModel
    {
        private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };
        public MediaItem()
            : this(null) { }

        public MediaItem(MediaDataItem dataItem)
            : base(dataItem)
        {
            if (DataModel is not null)
            {
                ParentCollectionId = ((MediaDataItem)DataModel).ParentCollectionId;
                Path = ((MediaDataItem)DataModel).Path;
                LastAccess = ((MediaDataItem)DataModel).LastAccess;
                Classified = ((MediaDataItem)DataModel).Classified;
                if (!string.IsNullOrWhiteSpace(((MediaDataItem)DataModel).MetaInformation))
                    MetaInformation = JsonConvert.DeserializeObject(((MediaDataItem)DataModel).MetaInformation, jsonSettings) as MediaInformation.MediaInformation;
                LastMetaInformationUpdate = ((MediaDataItem)DataModel).LastMetaInformationUpdate;
                var offset = Enum.GetValues(typeof(DataMediaItemCopyType)).IndexOf(((MediaDataItem)DataModel).CopyType);
                var value = Enum.GetValues(typeof(MediaItemCopyType)).Cast<MediaItemCopyType>().Skip(offset).FirstOrDefault();
                CopyType = value;
                LastClassificationTry = ((MediaDataItem)DataModel).LastClassificationTry;
                OriginalMediaItemId = ((MediaDataItem)DataModel).OriginalMediaItemId;
                DueDate = ((MediaDataItem)DataModel).DueDate;
                NeedsPictureUpdate = ((MediaDataItem)DataModel).NeedsPictureUpdate;
                LastPictureUpdateTry = ((MediaDataItem)DataModel).LastPictureUpdateTry;
                LastPosition = ((MediaDataItem)DataModel).LastPosition;
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

        public MediaInformation.MediaInformation MetaInformation
        {
            get
            {
                return GetProperty<MediaInformation.MediaInformation>();
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

        protected override void AssignChanges()
        {
            base.AssignChanges();
            ((MediaDataItem)DataModel).ParentCollectionId = ParentCollectionId;
            ((MediaDataItem)DataModel).Path = Path;
            ((MediaDataItem)DataModel).LastAccess = LastAccess;
            ((MediaDataItem)DataModel).Classified = Classified;
            ((MediaDataItem)DataModel).MetaInformation = JsonConvert.SerializeObject(MetaInformation, jsonSettings);
            ((MediaDataItem)DataModel).LastMetaInformationUpdate = LastMetaInformationUpdate;
            ((MediaDataItem)DataModel).LastClassificationTry = LastClassificationTry;
            ((MediaDataItem)DataModel).OriginalMediaItemId = OriginalMediaItemId;
            var offset = Enum.GetValues(typeof(MediaItemCopyType)).IndexOf(CopyType);
            var value = Enum.GetValues(typeof(DataMediaItemCopyType)).Cast<DataMediaItemCopyType>().Skip(offset).FirstOrDefault();
            ((MediaDataItem)DataModel).CopyType = value;
            ((MediaDataItem)DataModel).DueDate = DueDate;
            ((MediaDataItem)DataModel).NeedsPictureUpdate = NeedsPictureUpdate;
            ((MediaDataItem)DataModel).LastPictureUpdateTry = LastPictureUpdateTry;
            ((MediaDataItem)DataModel).LastPosition = LastPosition;
        }

    }
}
