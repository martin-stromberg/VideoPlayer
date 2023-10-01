using CommunityToolkit.Maui.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.Services.VideoSources
{
    public class CachedSmbShareVideoSource : SmbShareVideoSource
    {
        private class CachedSmbShareConfiguration
        {
            public string LocalPath { get; set; }
            public SmbShareConfiguration SmbSettings { get; set; }
        }

        public CachedSmbShareVideoSource()
            : base()
        {

        }
        public string LocalPath
        {
            get { return LocalSource?.Path; }
            set
            {
                LocalSource = new DriveVideoSource()
                {
                    Path = value
                };
            }
        }
        protected DriveVideoSource LocalSource { get; private set; }

        private MediaSource source = null;
        private MediaItem FindOrCreateLocalItem(SmbShareMediaItem item)
        {
            string relativeURI = item.URI.Remove(0, Settings.Path.Length + 1);
            var localItem = LocalSource.FindItem(relativeURI);
            if (localItem != null)
                return localItem;

            item.Download($"{LocalSource.Path}\\{relativeURI}");
            localItem = LocalSource.FindItem(relativeURI);
            if (localItem != null)
                return localItem;

            return item;
        }
        public override MediaSource CreateMediaSource(MediaItem item)
        {
            if (item is SmbShareMediaItem)
                item = FindOrCreateLocalItem((SmbShareMediaItem)item);
            if (source == null)
                source = MediaSource.FromFile(item.URI);
            return source;
        }
        public override string ConfigurationString => JsonConvert.SerializeObject(new CachedSmbShareConfiguration()
        {
            LocalPath = this.LocalPath,
            SmbSettings = base.Settings
        });
        public override void LoadConfiguration(string configuration)
        {
            var config = JsonConvert.DeserializeObject<CachedSmbShareConfiguration>(configuration);
            Settings = config.SmbSettings;
            LocalPath = config.LocalPath;
        }
    }
}
