using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a season within a TV show.
    /// </summary>
    public class TVShowSeason : MediaBaseEntry
    {
        /// <summary>
        /// Gets or sets the owning TV show identifier.
        /// </summary>
        public long TVShowId { get; set; }
        /// <summary>
        /// Gets or sets the owning TV show.
        /// </summary>
        public TVShow TVShow { get; set; } = null!;
        /// <summary>
        /// Gets or sets the episodes within the season.
        /// </summary>
        public ICollection<TVShowEpisode> Episodes { get; set; } = new List<TVShowEpisode>();
    }
}