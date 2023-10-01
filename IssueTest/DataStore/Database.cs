using IssueTest.DataStore.Model;
using SQLite;
using System.Diagnostics;

namespace IssueTest.DataStore
{
    internal class Database
    {
        public Database()
        {
        }

        private string filePath = string.Empty;
        public string FilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, "MyApp");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    filePath = Path.Combine(folder, "MyApp.db3");
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
        private SQLiteAsyncConnection connection;
        protected SQLiteAsyncConnection Connection
        {
            get
            {
                if (connection == null)
                    connection = new SQLiteAsyncConnection(FilePath, OpenFlags);
                return connection;
            }
        }

        public async Task<AsyncTableQuery<Item>> GetItemsAsync()
        {
            try
            {
                await InitOrUpgradeAsync();
                return Connection.Table<Item>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw new ApplicationException("-", ex);
            }
        }

        private async Task InitOrUpgradeAsync()
        {
            var result = await Connection.CreateTableAsync<Item>();
        }

        internal async Task AddItemAsync(Item item)
        {
            await Connection.InsertAsync(item);
        }
    }
}