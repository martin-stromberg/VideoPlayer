using SQLite;
using System;
using System.Linq;

namespace Mediathek.Services.Database
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
                    folder = Path.Combine(folder, "VideoPlayer");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    filePath = Path.Combine(folder, "MediaLibrary.db3");
                }
                return filePath;
            }
        }

        public SQLiteOpenFlags OpenFlags =
 // open the database in read/write mode
 SQLiteOpenFlags.ReadWrite |

            // create the database if it doesn't exist
            SQLiteOpenFlags.Create |

            // enable multi-threaded database access
            SQLiteOpenFlags.SharedCache |
            SQLiteOpenFlags.FullMutex;

    }
}
