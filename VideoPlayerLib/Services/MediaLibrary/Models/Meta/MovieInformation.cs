using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib.Services.MediaLibrary.Models.Meta
{
    public class MediaInformation
    {
        public string Title { get; set; }
    }
    public class MovieInformation: MediaInformation
    {        
        public string Genre { get; set; }
        public string Plot { get; set; }
    }
    public class EpisodeInformation: MediaInformation
    {
        public string ShowName { get; set; }
        public string Season { get; set; }
        public string Episode { get; set; }
    }
    public class TVShowInformation: MediaInformation
    {
        public string Plot { get; set; }
    }
}
