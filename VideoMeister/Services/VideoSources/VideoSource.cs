using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.Services.VideoSources
{
    public abstract class VideoSource
    {
        public VideoSource()
        {
        }

        public string Name { get; internal set; }

        public abstract MediaItemCollection MediaItemCollection { get; }
        public MediaItemCollection[] Folders => MediaItemCollection.Folders;
        public MediaItem[] Files => MediaItemCollection.Files;
        public abstract string ConfigurationString { get; }
        public abstract void LoadConfiguration(string configuration);
        public abstract MediaSource CreateMediaSource(MediaItem item);
        public abstract string GetText(MediaItem item);
        internal abstract void Download(MediaItem nfoFile, string destFile);
    }
}
