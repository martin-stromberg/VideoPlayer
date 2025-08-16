using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    public class MovieCollection : MediaBaseEntry
    {
        public ICollection<Movie> Movies { get; set; } = new List<Movie>();
    }
}