using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.Services.VideoSources
{
    public class SmbShareVideoSource : VideoSource
    {
        public class SmbShareConfiguration
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Path { get; set; }
        }
        public SmbShareConfiguration Settings { get; set; } = new SmbShareConfiguration();

        private MediaItemCollection _rootItem = null;
        private MediaItemCollection InitRootItem()
        {
            _rootItem = new SmbShareMediaItemCollection(this, Settings.Path, Settings.Username, Settings.Password);
            return _rootItem;
        }

        private MediaSource source = null;
        public override MediaSource CreateMediaSource(MediaItem item)
        {
            if (source == null)
                source = MediaSource.FromFile(item.URI);
            return source;
        }

        public override string GetText(MediaItem item)
        {
            throw new NotImplementedException();
        }

        internal override void Download(MediaItem sourceFile, string destFile)
        {
            ((SmbShareMediaItem)sourceFile).Download(destFile);
        }

        public override MediaItemCollection MediaItemCollection => _rootItem ?? InitRootItem();
        public override string ConfigurationString => Newtonsoft.Json.JsonConvert.SerializeObject(Settings);
        public override void LoadConfiguration(string configuration)
        {
            var config = JsonConvert.DeserializeObject<SmbShareConfiguration>(configuration);
            Settings = config;
        }

    }
}
