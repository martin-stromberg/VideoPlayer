using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VideoWebPlayer.Data
{
    public class TVShow : MediaBaseEntry
    {
        // Bisherige Eigenschaften
        public ICollection<TVShowSeason> Seasons { get; set; } = new List<TVShowSeason>();

        // Neue Eigenschaften aus tvshow.nfo
        public string? OriginalName { get; set; }
        public string? Language { get; set; }
        public string? Plot { get; set; }
        public string? Status { get; set; }
        public string? Studio { get; set; }
        public string? GenreNames { get; set; } // Kommagetrennt, falls mehrere Genre    
        public ICollection<TVShowGenre> TVShowGenres { get; set; } = new List<TVShowGenre>();

        public void LoadFromXml(XElement xml)
        {
            OriginalName = GetElementValue(xml, "title");
            Language = GetElementValue(xml, "language");
            Plot = GetElementValue(xml, "plot");
            Status = GetElementValue(xml, "status");
            Studio = GetElementValue(xml, "studio");
            GenreNames = string.Join(",", xml.Elements("genre").Select(g => g.Value));
            PremieredAt = DateTime.TryParse(GetElementValue(xml, "premiered"), out var dt) ? dt : null;            
        }

        private string GetElementValue(XElement xml, string elementName)
        {
            return xml.Element(elementName)?.Value ?? string.Empty;
        }
    }
}