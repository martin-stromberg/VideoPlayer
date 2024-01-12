using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class TVShowEpisodeMediaItem: BaseDataModel
    {

        public long EpisodeId { get; set; }

        public long MediaItemId { get; set; }

    }
}
