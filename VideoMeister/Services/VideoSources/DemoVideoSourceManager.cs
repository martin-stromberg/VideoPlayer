using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Database;

namespace VideoMeister.Services.VideoSources
{
    internal class DemoVideoSourceManager : VideoSourceManager
    {
        public DemoVideoSourceManager(VideoSourceManagerSettings settings, MediaLibraryDatabase database)
            : base(settings,database)
        {
            var CacheFolder = new DirectoryInfo($"{FileSystem.Current.AppDataDirectory}\\Cache");
            if (!CacheFolder.Exists)
                CacheFolder.Create();
            Add(new CachedSmbShareVideoSource()
            {
                Settings = new SmbShareVideoSource.SmbShareConfiguration()
                {
                    Password = "Hi1TvM!nav",
                    Username = "mstro",
                    Path = "\\\\raspberrypi\\FileServer\\Filme",
                },                
                Name = "Filme",
                LocalPath = $"{CacheFolder.FullName}\\Filme"
            });
            Add(new CachedSmbShareVideoSource()
            {
                Settings = new SmbShareVideoSource.SmbShareConfiguration()
                {
                    Password = "Hi1TvM!nav",
                    Username = "mstro",
                    Path = "\\\\raspberrypi\\FileServer\\Serien",
                },
                Name = "Serien",
                LocalPath = $"{CacheFolder.FullName}\\Serien"
            });
            Add(new CachedSmbShareVideoSource()
            {
                Settings = new SmbShareVideoSource.SmbShareConfiguration()
                {
                    Password = "Hi1TvM!nav",
                    Username = "mstro",
                    Path = "\\\\raspberrypi\\FileServer\\Serien1",
                },
                Name = "Serien1",
                LocalPath = $"{CacheFolder.FullName}\\Serien1"
            });
        }
    }
}
