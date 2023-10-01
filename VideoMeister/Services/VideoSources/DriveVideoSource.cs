using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.Services.VideoSources
{
    public class DriveVideoSource : VideoSource
    {
        public override MediaItemCollection MediaItemCollection => _rootItem ?? InitRootItem();

        public string Path { get; internal set; }

        public override string ConfigurationString => Path;
        public override void LoadConfiguration(string configuration)
        {
            Path = configuration;
        }

        private MediaItemCollection _rootItem = null;
        private MediaItemCollection InitRootItem()
        {
            _rootItem = new DriveMediaItemCollection(this, Path);
            return _rootItem;
        }
        private MediaSource source = null;
        public override MediaSource CreateMediaSource(MediaItem item)
        {
            if (source == null)
                source = MediaSource.FromFile(item.URI);
            return source;
        }

        public MediaItem FindItem(string relativeURI)
        {
            relativeURI = relativeURI.Replace("\\", "/");
            MediaItemCollection folder = MediaItemCollection;
            folder.Refresh();
            var parts = relativeURI.Split('/');
            while (parts.Length > 1)
            {
                folder = folder.Folders.FirstOrDefault(f => true);
                folder.Refresh();
            }
            return folder.Files.FirstOrDefault(f => f.URI.EndsWith(parts[0]));
        }

        internal DriveMediaItemCollection CreateFolder(string name)
        {
            return MediaItemCollection.CreateFolder(name);
        }

        public override string GetText(MediaItem item)
        {
            throw new NotImplementedException();
        }

        internal override void Download(MediaItem nfoFile, string destFile)
        {
            throw new NotImplementedException();
        }
    }
}
