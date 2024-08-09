using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public enum MediaSourceType
    {

        Undefined,
        Http

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

    }
}
