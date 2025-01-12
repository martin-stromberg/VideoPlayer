using Newtonsoft.Json;
using System.Text;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(MediaDataItemCollection))]
    public class MediaCollection: BaseServiceModel
    {

        public MediaCollection()
            : this(null) { }

        public MediaCollection(MediaDataItemCollection dataModel)
            : base(dataModel)
        {
            if (DataModel is not null)
            {
                ParentId = ((MediaDataItemCollection)DataModel).ParentId;
                SourceId = ((MediaDataItemCollection)DataModel).SourceId;
                Path = ((MediaDataItemCollection)DataModel).Path;
                LastAccess = ((MediaDataItemCollection)DataModel).LastAccess;
                Classified = ((MediaDataItemCollection)DataModel).Classified;
                if (!string.IsNullOrWhiteSpace(((MediaDataItemCollection)DataModel).MetaInformation))
                    MetaInformation = JsonConvert.DeserializeObject<MediaInformation.MediaInformation>(((MediaDataItemCollection)DataModel).MetaInformation, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                //MetaInformation = JsonConvert.DeserializeObject(((MediaDataItemCollection)DataModel).MetaInformation) as MediaInformation.MediaInformation;
                LastMetaInformationUpdate = ((MediaDataItemCollection)DataModel).LastMetaInformationUpdate;
                LastScanCompleted = ((MediaDataItemCollection)DataModel).LastScanCompleted;
            }
        }

        public long ParentId
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

        public long SourceId
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

        public MediaInformation.MediaInformation MetaInformation {
            get
            {
                return GetProperty<MediaInformation.MediaInformation>();
            }
            set
            {
                SetProperty(value);
            }
        }
        public DateTime LastMetaInformationUpdate {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public bool LastScanCompleted {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty(value);
            }
        }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            ((MediaDataItemCollection)DataModel).ParentId = ParentId;
            ((MediaDataItemCollection)DataModel).SourceId = SourceId;
            ((MediaDataItemCollection)DataModel).Path = Path;
            ((MediaDataItemCollection)DataModel).LastAccess = LastAccess;
            ((MediaDataItemCollection)DataModel).Classified = Classified;
            //((MediaDataItemCollection)DataModel).MetaInformation = JsonConvert.SerializeObject(MetaInformation);
            ((MediaDataItemCollection)DataModel).MetaInformation = JsonConvert.SerializeObject(MetaInformation, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            ((MediaDataItemCollection)DataModel).LastMetaInformationUpdate = LastMetaInformationUpdate;
            ((MediaDataItemCollection)DataModel).LastScanCompleted = LastScanCompleted;
        }

    }
}
