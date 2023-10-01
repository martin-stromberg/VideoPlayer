using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Models
{
    public abstract class MediaItem
    {
        public MediaItem(MediaItemCollection collection)
        {
            Parent = collection;
            Source = collection.Source;
        }
        public abstract string URI { get; }
        public MediaItemCollection Parent { get; }
        public VideoSource Source { get; }
        public abstract string Name { get; }
        public MediaItem AlternateFile { get; set; }
    }
}
