using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Models
{
    public abstract class MediaItemCollection
    {
        public MediaItemCollection(VideoSource source) 
        {
            Source = source;
        }

        public abstract MediaItemCollection[] Folders { get; }
        public abstract MediaItem[] Files { get; }
        public VideoSource Source { get; }
        public abstract string Name { get; }

        public abstract void Refresh();

        public abstract DriveMediaItemCollection CreateFolder(string name);
    }
}
