using System;
using System.Linq;
using VideoPlayer.Services.Database;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Models.TVShows
{
    [DataModelReference(typeof(Services.Database.Models.TVShowEpisode))]
    public class TVShowEpisode: BaseModel
    {

        public string ShowName { get; set; }

        public long SeasonId { get; set; }

        public string SeasonName { get; set; }

        public string EpisodeNo { get; set; }

        public long[] MediaItems { get; set; }

        internal TVShowEpisode SetMediaItems(IEnumerable<TVShowEpisodeMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.MediaItemId).ToArray();
            return this;
        }

    }
}
