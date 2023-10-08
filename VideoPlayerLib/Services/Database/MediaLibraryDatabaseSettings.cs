using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database
{
    public class MediaLibraryDatabaseSettings
    {
        private string filePath = string.Empty;
        public string FilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, "VideoMeister");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    filePath = Path.Combine(folder, "MediaLibrary.db3");
                }
                return filePath;
            }
        }
        public SQLite.SQLiteOpenFlags OpenFlags =
            // open the database in read/write mode
            SQLite.SQLiteOpenFlags.ReadWrite |
            // create the database if it doesn't exist
            SQLite.SQLiteOpenFlags.Create |
            // enable multi-threaded database access
            SQLite.SQLiteOpenFlags.SharedCache |
            SQLite.SQLiteOpenFlags.FullMutex;
    }
}
