using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a genre for movies or TV shows.
    /// </summary>
    /// <summary>
    /// Represents a genre within a media source.
    /// </summary>
    public class Genre
    {
        /// <summary>
        /// Gets or sets the genre identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the genre name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the owning media source identifier.
        /// </summary>
        public long MediaSourceId { get; set; }

        /// <summary>
        /// Gets or sets the owning media source.
        /// </summary>
        public MediaSource MediaSource { get; set; }

        /// <summary>
        /// Gets or sets the optional start date for visibility.
        /// </summary>
        public DateTime? StartDate { get; set; } // optional, null = immer sichtbar

        /// <summary>
        /// Gets or sets the optional end date for visibility.
        /// </summary>
        public DateTime? EndDate { get; set; } // optional, null = immer sichtbar

        /// <summary>
        /// Gets or sets the alternate names for the genre.
        /// </summary>
        public ICollection<GenreName> AlternateNames { get; set; }

        /// <summary>
        /// Gets or sets the movie genre link entries.
        /// </summary>
        public ICollection<MovieGenre> MovieGenres { get; set; } // Hinzugefügt für die Beziehung zu MovieGenre

        /// <summary>
        /// Gets or sets the TV show genre link entries.
        /// </summary>
        public ICollection<TVShowGenre> TVShowGenres { get; set; } // Optional: für die Beziehung zu TVShowGenre

        /// <summary>
        /// Gets or sets a value indicating whether the genre is hidden.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Returns the display representation of the genre.
        /// </summary>
        /// <returns>A string combining id and name.</returns>
        public override string ToString()
        {
            return $"{Id} {Name}";
        }
    }
}