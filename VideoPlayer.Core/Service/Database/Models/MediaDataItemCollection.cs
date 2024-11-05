using SQLite;
using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public class MediaDataItemCollection: BaseDataModel
    {

        [Indexed]
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

        public string MetaInformation {
            get
            {
                return GetProperty<string>();
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
    }
}
