using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public class Movie: BaseDataModel
    {

        public string Genre { get; set; }

        public string Plot { get; set; }

        public string PicturePath { get; set; }

        public long CollectionId { get; set; }

    }
}
