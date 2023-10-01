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
        public string CacheFolderPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(tempFolderPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, "VideoMeister", "Cache");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    tempFolderPath = folder;
                }
                return tempFolderPath;
            }
        }
    }
}