using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class TVShowSeason: BaseDataModel
    {

        public long ShowId { get; set; }

        public string BannerPath { get; set; }

    }
}
