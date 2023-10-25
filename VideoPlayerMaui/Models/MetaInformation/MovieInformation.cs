using System;
using System.Linq;

namespace VideoPlayer.Models.MetaInformation
{
    public class MovieInformation : MediaInformation
    {
        public string Genre { get; set; }
        public string Plot { get; set; }
    }
}
