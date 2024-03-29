using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class OverviewElement: BaseDataModel
    {

        public string Type { get; set; }

        public long OriginalId { get; set; }

        public int Year { get; set; }

        public string Genre1 { get; set; }

        public string Genre2 { get; set; }

        public string Genre3 { get; set; }

        public string Genre4 { get; set; }

        public string Genre5 { get; set; }

        public string PicturePath { get; set; }

        public DateTime LastUpdate { get; set; }

    }
}
