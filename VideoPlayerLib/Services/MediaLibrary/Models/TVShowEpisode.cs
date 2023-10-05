using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShowEpisode))]
    public class TVShowEpisode : BaseModel
    {
        public long SeasonId { get; set; }
        public string EpisodeNo { get; set; }
        public long[] MediaItems { get; set; }
    }
}
