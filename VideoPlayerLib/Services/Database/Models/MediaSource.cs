using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public class MediaSource: BaseDataModel
    {
        public string Type { get; set; }
        [AffectsEqualAttribute]
        public string Configuration { get; set; }
        public DateTime LastScan { get; set; }
    }
}
