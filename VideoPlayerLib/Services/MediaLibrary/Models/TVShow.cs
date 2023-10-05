using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.TVShow))]
    public class TVShow : BaseModel
    {
        public TVShowSeason[] Seasons { get; set; }
    }
}
