namespace MyVideoPlayer.Helper.LibraryScan
{
    public class LibraryScannerSettings
    {

        private string tempFolderPath = string.Empty;

        public string TempFolderPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(tempFolderPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, "VideoMeister", "Temp");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    tempFolderPath = folder;
                }
                return tempFolderPath;
            }
        }

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

        public string CacheFolderPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(tempFolderPath))
                {
                    var folder = Path.Combine(CacheRootPath, "Cache");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    tempFolderPath = folder;
                }
                return tempFolderPath;
            }
        }

    }
}