using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MediaSource: BaseDataModel
    {

        public string Type { get; set; }

        [AffectsEqualAttribute]
        public string Configuration { get; set; }

        public DateTime LastScan { get; set; }

        public DateTime LastScanStart { get; set; }

        public override void Update(BaseDataModel source)
        {
            base.Update(source);
            Configuration = ((MediaSource)source).Configuration;
        }

    }
}
