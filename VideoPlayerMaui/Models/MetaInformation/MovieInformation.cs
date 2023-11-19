using System;
using System.Linq;

namespace VideoPlayer.Models.MetaInformation
{
    public class MovieInformation: MediaInformation
    {

        public string Genre { get; set; }

        public string Plot { get; set; }

        public DateTime ReleaseDate { get; set; }

        public int Year { get; set; }

    }
}
