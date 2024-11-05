using SQLite;
using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public enum DataMediaItemCopyType
    {

        Original = 0,
        Trailer = 100,
        Cache = 200,
        Download = 201

    }

    public class MediaDataItem: BaseDataModel
    {

        [Indexed]
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

        public long OriginalMediaItemId {
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

        [Indexed]
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

        public string MetaInformation
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

        public DataMediaItemCopyType CopyType
        {
            get
            {
                return GetProperty<DataMediaItemCopyType>();
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
    }
}
