using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a collection of movies.
    /// </summary>
    public class MovieCollection : MediaBaseEntry
    {
        /// <summary>
        /// Gets or sets the movies in the collection.
        /// </summary>
        public ICollection<Movie> Movies { get; set; } = new List<Movie>();
    }
}