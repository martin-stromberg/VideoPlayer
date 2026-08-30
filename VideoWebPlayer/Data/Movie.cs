using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a movie entry and its metadata.
    /// </summary>
    public class Movie : MediaBaseEntry
    {
        /// <summary>
        /// Gets or sets the owning movie collection identifier.
        /// </summary>
        public long? MovieCollectionId { get; set; }
        /// <summary>
        /// Gets or sets the owning movie collection.
        /// </summary>
        public MovieCollection? MovieCollection { get; set; }

        // Eigenschaften aus der NFO-Datei
        /// <summary>
        /// Gets or sets the original title.
        /// </summary>
        public string? OriginalTitle { get; set; }
        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public string? Language { get; set; }
        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }
        /// <summary>
        /// Gets or sets the release date.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }
        /// <summary>
        /// Gets or sets the premiered date.
        /// </summary>
        public DateTime? PremieredAt { get; set; }
        /// <summary>
        /// Gets or sets the country.
        /// </summary>
        public string? Country { get; set; }
        /// <summary>
        /// Gets or sets the studios as a comma-separated list.
        /// </summary>
        public string? Studios { get; set; } // Kommagetrennt, falls mehrere Studios
        /// <summary>
        /// Gets or sets the director.
        /// </summary>
        public string? Director { get; set; }
        /// <summary>
        /// Gets or sets the credits as a comma-separated list.
        /// </summary>
        public string? Credits { get; set; } // Kommagetrennt, falls mehrere
        /// <summary>
        /// Gets or sets the plot summary.
        /// </summary>
        public string? Plot { get; set; }

        /// <summary>
        /// Gets or sets a value indicating when (or whether) actor metadata has been classified for this movie.
        /// </summary>
        public DateTime? ActorsClassifiedAt { get; set; }

        /// <summary>
        /// Gets the media item link entries for the movie.
        /// </summary>
        public ICollection<MovieMediaItem> MovieMediaItems { get; set; } = new List<MovieMediaItem>();
        /// <summary>
        /// Gets or sets the genre names as a comma-separated list.
        /// </summary>
        public string? GenreNames { get; set; } // Kommagetrennt, falls mehrere Genres
        /// <summary>
        /// Gets the genre link entries for the movie.
        /// </summary>
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
        /// <summary>
        /// Gets the actor link entries for the movie.
        /// </summary>
        public ICollection<MovieActor> MovieActors { get; set; } = new List<MovieActor>();

        /// <summary>
        /// Gets the media items associated with this movie.
        /// </summary>
        public IEnumerable<MediaItem> MediaItems => MovieMediaItems.Select(mmi => mmi.MediaItem);

        /// <summary>
        /// Loads metadata from an NFO XML document.
        /// </summary>
        /// <param name="xml">The XML element containing movie metadata.</param>
        public void LoadFromXml(XElement xml)
        {
            OriginalTitle = xml.Element("originaltitle")?.Value;
            Language = xml.Element("language")?.Value;
            Year = int.TryParse(xml.Element("year")?.Value, out var y) ? y : null;
            ReleaseDate = DateTime.TryParse(xml.Element("releasedate")?.Value, out var rd) ? rd : null;
            PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : null;
            EndedAt = ReleaseDate > PremieredAt ? ReleaseDate : PremieredAt;
            Country = xml.Element("country")?.Value;
            GenreNames = string.Join(",", xml.Elements("genre").Select(g => g.Value));
            Studios = string.Join(",", xml.Elements("studio").Select(s => s.Value));
            Director = xml.Element("director")?.Value;
            Credits = string.Join(",", xml.Elements("credits").Select(c => c.Value));
            Plot = xml.Element("plot")?.Value;
        }
    }
}
