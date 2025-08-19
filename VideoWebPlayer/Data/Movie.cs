using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VideoWebPlayer.Data
{
    public class Movie : MediaBaseEntry
    {
        public long? MovieCollectionId { get; set; }
        public MovieCollection? MovieCollection { get; set; }

        // Eigenschaften aus der NFO-Datei
        public string? OriginalTitle { get; set; }
        public string? Language { get; set; }
        public int? Year { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public DateTime? PremieredAt { get; set; }
        public string? Country { get; set; }
        public string? Studios { get; set; } // Kommagetrennt, falls mehrere Studios
        public string? Director { get; set; }
        public string? Credits { get; set; } // Kommagetrennt, falls mehrere
        public string? Plot { get; set; }

        public ICollection<MovieMediaItem> MovieMediaItems { get; set; } = new List<MovieMediaItem>();
        public string? GenreNames { get; set; } // Kommagetrennt, falls mehrere Genres
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();

        // Optional: Komfort-Property für direkten Zugriff auf die MediaItems
        public IEnumerable<MediaItem> MediaItems => MovieMediaItems.Select(mmi => mmi.MediaItem);

        // Weitere Felder wie Actors können als separate Entität modelliert werden

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