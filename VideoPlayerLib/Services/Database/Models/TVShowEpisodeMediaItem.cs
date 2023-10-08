using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database.Models
{
    public class TVShowEpisodeMediaItem : BaseDataModel
    {
        public long EpisodeId { get; set; }
        public long MediaItemId { get; set; }
    }
}
