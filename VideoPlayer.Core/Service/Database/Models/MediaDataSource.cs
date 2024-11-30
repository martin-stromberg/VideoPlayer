using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public enum MediaSourceType
    {

        Undefined,
        Http,
        Smb,
        SFTP
    }

    public class MediaDataSource: BaseDataModel
    {

        public MediaSourceType Type
        {
            get
            {
                return GetProperty<MediaSourceType>();
            }
            set
            {
                if (value == MediaSourceType.Undefined)
                    value = MediaSourceType.Smb;
                SetProperty(value);
            }
        }

        public string Configuration
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

        public DateTime LastScan
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

        public bool Deleted {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty(value);
            }
        }
    }
}
