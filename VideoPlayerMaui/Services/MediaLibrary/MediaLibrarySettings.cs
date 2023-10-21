using System;
using System.Linq;

namespace VideoPlayer.Services.MediaLibrary
{
    public class MediaLibrarySettings
    {

        private string cacheRootPath = string.Empty;

        public string CacheRootPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(cacheRootPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, "VideoMeister");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    cacheRootPath = folder;
                }
                return cacheRootPath;
            }
        }

    }
}
