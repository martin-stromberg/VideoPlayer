using Newtonsoft.Json;
using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Sources
{
    public class MediaSource : BaseServiceModel
    {

        public MediaSource()
            : this(null) { }

        public MediaSource(MediaDataSource dataModel)
            : base(dataModel)
        {
            if (dataModel is not null)
            {
                LastScan = dataModel.LastScan;
                Deleted = ((MediaDataSource)DataModel).Deleted;
                Tenant = ((MediaDataSource)DataModel).Tenant;
            }
        }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((MediaDataSource)DataModel).Configuration = JsonConvert.SerializeObject(this);
                ((MediaDataSource)DataModel).LastScan = LastScan;
                ((MediaDataSource)DataModel).Deleted = Deleted;
                ((MediaDataSource)DataModel).Tenant = Tenant;
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

        public bool Deleted
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

        public string Tenant {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }
    }
}
