using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a TV show entry and its metadata.
    /// </summary>
    public class TVShow : MediaBaseEntry, IAgeRated
    {
        // Bisherige Eigenschaften
        /// <summary>
        /// Gets or sets the seasons for the show.
        /// </summary>
        public ICollection<TVShowSeason> Seasons { get; set; } = new List<TVShowSeason>();

        // Neue Eigenschaften aus tvshow.nfo
        /// <summary>
        /// Gets or sets the original name.
        /// </summary>
        public string? OriginalName { get; set; }
        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public string? Language { get; set; }
        /// <summary>
        /// Gets or sets the plot summary.
        /// </summary>
        public string? Plot { get; set; }
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string? Status { get; set; }
        /// <summary>
        /// Gets or sets the studio.
        /// </summary>
        public string? Studio { get; set; }
        /// <summary>
        /// Gets or sets the genre names as a comma-separated list.
        /// </summary>
        public string? GenreNames { get; set; } // Kommagetrennt, falls mehrere Genre    
        /// <summary>
        /// Gets or sets the normalized age rating for display (e.g. "FSK 12" or "PG-13").
        /// </summary>
        public string? AgeRating { get; set; }
        /// <summary>
        /// Gets or sets the unmodified age rating source value (e.g. "Rated PG-13" or "DE:16").
        /// </summary>
        public string? AgeRatingRaw { get; set; }
        /// <summary>
        /// Gets or sets the comparable minimum age derived from the rating, if known.
        /// </summary>
        public int? AgeRatingMinAge { get; set; }
        /// <summary>
        /// Gets or sets a value indicating when (or whether) the age rating has been classified for this show.
        /// </summary>
        public DateTime? AgeRatingClassifiedAt { get; set; }

        /// <summary>
        /// Gets or sets the TV show genre link entries.
        /// </summary>
        public ICollection<TVShowGenre> TVShowGenres { get; set; } = new List<TVShowGenre>();

        /// <summary>
        /// Loads metadata from a tvshow NFO XML document.
        /// </summary>
        /// <param name="xml">The XML element containing show metadata.</param>
        public void LoadFromXml(XElement xml)
        {
            OriginalName = GetElementValue(xml, "title");
            Language = GetElementValue(xml, "language");
            Plot = GetElementValue(xml, "plot");
            Status = GetElementValue(xml, "status");
            Studio = GetElementValue(xml, "studio");
            GenreNames = string.Join(",", xml.Elements("genre").Select(g => g.Value));
            PremieredAt = DateTime.TryParse(GetElementValue(xml, "premiered"), out var dt) ? dt : null;

            var (ageRatingRaw, ageRating) = AgeRatingNormalizer.ExtractFromXml(xml);
            AgeRatingRaw = ageRatingRaw;
            AgeRating = ageRating.DisplayValue;
            AgeRatingMinAge = ageRating.MinAge;
        }

        /// <summary>
        /// Gets a child element value or an empty string when missing.
        /// </summary>
        /// <param name="xml">The parent element.</param>
        /// <param name="elementName">The child element name.</param>
        /// <returns>The element value.</returns>
        private string GetElementValue(XElement xml, string elementName)
        {
            return xml.Element(elementName)?.Value ?? string.Empty;
        }
    }
}