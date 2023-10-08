using System;
using System.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShowEpisode))]
    public class TVShowEpisode : BaseModel
    {
        public long SeasonId { get; set; }
        public string EpisodeNo { get; set; }
        public long[] MediaItems { get; set; }

        internal TVShowEpisode SetMediaItems(IEnumerable<TVShowEpisodeMediaItem> mediaItems)
        {
            MediaItems = mediaItems.Select(mi => mi.Id).ToArray();
            return this;
        }
    }
}
