using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShowSeason))]
    public class TVShowSeason : BaseModel
    {
        public long ShowId { get; set; }
        public TVShowEpisode[] Episodes { get; set; }
    }
}
