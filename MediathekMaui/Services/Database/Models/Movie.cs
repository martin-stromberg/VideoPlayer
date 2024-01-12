using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class Movie: BaseDataModel
    {

        public string Genre { get; set; }

        public string Plot { get; set; }

        public DateTime Date { get; set; }

        public string PicturePath { get; set; }

        public long CollectionId { get; set; }

        public long TrailerMediaItemId { get; set; }

        public bool IsSingleCollectionMovie { get; set; }

    }
}
