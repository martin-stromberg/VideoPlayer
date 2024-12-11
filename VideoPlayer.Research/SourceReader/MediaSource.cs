using Newtonsoft.Json;
using System;
using System.Linq;

namespace VideoPlayer.Service.Library.Models
{
    public class MediaSource: BaseServiceModel
    {

        public MediaSource()
            : base() { }

        

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
