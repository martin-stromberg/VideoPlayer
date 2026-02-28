using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a single episode of a TV show, including metadata such as the episode number,
    /// plot summary, and associated media items.
    /// </summary>
    /// <summary>
    /// Represents an episode of a TV show.
    /// </summary>
    public class TVShowEpisode : MediaBaseEntry
    {
        /// <summary>
        /// Gets or sets the episode number.
        /// </summary>
        public int Number { get; set; }
        /// <summary>
        /// Gets or sets the owning TV show season identifier.
        /// </summary>
        public long TVShowSeasonId { get; set; }
        /// <summary>
        /// Gets or sets the owning TV show season.
        /// </summary>
        public TVShowSeason TVShowSeason { get; set; } = null!;        
        /// <summary>
        /// Gets or sets the plot summary.
        /// </summary>
        public string? Plot { get; set; }

        /// <summary>
        /// Gets or sets the media item link entries.
        /// </summary>
        public ICollection<TVShowEpisodeMediaItem> TVShowEpisodeMediaItems { get; set; } = new List<TVShowEpisodeMediaItem>();

        // Komfort-Property für direkten Zugriff auf die MediaItems
        /// <summary>
        /// Gets the media items for the episode.
        /// </summary>
        public IEnumerable<MediaItem> MediaItems => TVShowEpisodeMediaItems.Select(ei => ei.MediaItem);

        // Beispiel für das Setzen der Eigenschaften aus XML
        /// <summary>
        /// Loads metadata from an episode NFO XML document.
        /// </summary>
        /// <param name="xml">The XML element containing episode metadata.</param>
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