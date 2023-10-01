namespace VideoMeister.Services.Database
{
    public class DatabaseSettings
    {
        private string filePath = string.Empty;
        public string FilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    var LibraryFolder = TryGetFolder(folder, "Database", "VideoMeister.db3");
                    if (LibraryFolder == null)
                        LibraryFolder = TryGetFolder(FileSystem.Current.AppDataDirectory, "Database", "VideoMeister.db3");
                    if (LibraryFolder == null)
                        LibraryFolder = TryGetFolder(FileSystem.Current.CacheDirectory, "Database", "VideoMeister.db3");
                    filePath = Path.Combine(LibraryFolder.FullName, "VideoMeister.db3");
                }
                return filePath;
            }
        }
        public SQLite.SQLiteOpenFlags Flags =
            // open the database in read/write mode
            SQLite.SQLiteOpenFlags.ReadWrite |
            // create the database if it doesn't exist
            SQLite.SQLiteOpenFlags.Create |
            // enable multi-threaded database access
            SQLite.SQLiteOpenFlags.SharedCache | 
            SQLite.SQLiteOpenFlags.FullMutex;

        private DirectoryInfo TryGetFolder(string rootPath, string Name, string testFileName)
        {
            var Folder = new DirectoryInfo(Path.Combine(rootPath, Name));
            if (!Folder.Exists)
                try
                {
                    Folder.Create();
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            try
            {
                FileInfo testFile = new FileInfo(Path.Combine(Folder.FullName, testFileName));
                if (testFile.Exists)
                    testFile.Delete();
                testFile.Create().Close();
                testFile.Refresh();
                if (testFile.Exists)
                    testFile.Delete();
            }
            catch(Exception ex)
            {
                return null;
            }
            return Folder;
        }
    }
}
