using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.Database.Models
{
    public class TVShowEpisodeMediaItem: BaseDataModel
    {
        public long EpisodeId { get; set; }
        public long MediaItemId { get; set; }
    }
}
