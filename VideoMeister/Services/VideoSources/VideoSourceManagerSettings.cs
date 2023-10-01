namespace VideoMeister.Services.VideoSources
{
    public class VideoSourceManagerSettings
    {
        private string libraryPath = string.Empty;
        public string LibraryPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(libraryPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    var LibraryFolder = TryGetFolder(folder, "Database");
                    if (LibraryFolder == null)
                        LibraryFolder = TryGetFolder(FileSystem.Current.AppDataDirectory, "Library");
                    if (LibraryFolder == null)
                        LibraryFolder = TryGetFolder(FileSystem.Current.CacheDirectory, "Library");
                    libraryPath = LibraryFolder.FullName;
                }
                return libraryPath;
            }
        }
        private DirectoryInfo TryGetFolder(string rootPath, string Name)
        {
            var Folder = new DirectoryInfo($"{rootPath}\\{Name}");
            if (Folder.Exists)
                return Folder;
            try
            {
                Folder.Create();
                return Folder;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
