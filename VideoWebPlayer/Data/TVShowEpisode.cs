using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VideoWebPlayer.Data
{
    public class TVShowEpisode : MediaBaseEntry
    {
        public int Number { get; set; }
        public long TVShowSeasonId { get; set; }
        public TVShowSeason TVShowSeason { get; set; } = null!;        
        public string? Plot { get; set; }

        public ICollection<TVShowEpisodeMediaItem> TVShowEpisodeMediaItems { get; set; } = new List<TVShowEpisodeMediaItem>();

        // Komfort-Property für direkten Zugriff auf die MediaItems
        public IEnumerable<MediaItem> MediaItems => TVShowEpisodeMediaItems.Select(ei => ei.MediaItem);

        // Beispiel für das Setzen der Eigenschaften aus XML
        public void LoadFromXml(XElement xml)
        {
            Number = int.TryParse(xml.Element("episode")?.Value, out var n) ? n : 0;
            ReleaseDate = DateTime.TryParse(xml.Element("aired")?.Value, out var aired) ? aired : null;
            PremieredAt = DateTime.TryParse(xml.Element("premiered")?.Value, out var prem) ? prem : null;
            Plot = xml.Element("plot")?.Value;
            // Weitere Felder nach Bedarf ergänzen
        }
    }
}