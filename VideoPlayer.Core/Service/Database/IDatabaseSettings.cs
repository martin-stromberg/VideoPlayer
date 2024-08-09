using System;
using System.Linq;

namespace VideoPlayer.Service.Database
{
    public class DatabaseSettings: IDatabaseSettings
    {

        public string DatabasePath
        {
            get
            {
                return Path.Combine(FileSystem.Current.AppDataDirectory, "FinanceManager.db");
            }
        }

    }

    public interface IDatabaseSettings
    {

        string DatabasePath { get; }

    }
}
