using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    public class TVShowSeason : MediaBaseEntry
    {
        public long TVShowId { get; set; }
        public TVShow TVShow { get; set; } = null!;
        public ICollection<TVShowEpisode> Episodes { get; set; } = new List<TVShowEpisode>();
    }
}