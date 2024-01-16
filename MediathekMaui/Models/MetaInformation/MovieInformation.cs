using System;
using System.Linq;

namespace Mediathek.Models.MetaInformation
{
    public class MovieInformation: MediaInformation
    {

        public string Genre { get; set; }

        public string Plot { get; set; }

        public DateTime ReleaseDate { get; set; }

        public int Year { get; set; }

        public string[] Genres { get; set; }

        public string Language { get; set; }

        public DateTime PremieredAt { get; set; }

        public string[] Countries { get; set; }

    }
}
