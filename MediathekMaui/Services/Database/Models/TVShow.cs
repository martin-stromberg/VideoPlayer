using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class TVShow: BaseDataModel
    {

        public string PicturePath { get; set; }

        public string BannerPath { get; set; }

        public DateTime PremieredAt { get; set; }

        public string Genres { get; set; }

        public string Language { get; set; }

    }
}
