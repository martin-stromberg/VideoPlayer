using Newtonsoft.Json;
using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    public class MediaSource: BaseServiceModel
    {

        public MediaSource()
            : this(null) { }

        public MediaSource(MediaDataSource dataModel)
            : base(dataModel) { }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
                ((MediaDataSource)DataModel).Configuration = JsonConvert.SerializeObject(this);
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
