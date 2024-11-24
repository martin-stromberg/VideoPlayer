using System.Reflection;
using VideoPlayer.Service.Library.Models;
using static SQLite.SQLite3;

namespace VideoPlayer.Service.Download
{
    public class ApplicationEnvironment : IEnvironment
    {
        private string productName = string.Empty;
        public string ProductName
        {
            get
            {
                if (string.IsNullOrEmpty(productName))
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    productName = assembly.GetName().Name;
                }
                return productName;
            }
        }
        public string GetRootPath()
        {
            var folderPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        ProductName
                        );
            if (!Path.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            return folderPath;
        }
        public string GetPath(MediaItemCopyType copyType)
        {
            var folderPath = Path.Combine(
                        GetRootPath(),
                        copyType.ToString()
                        );
            if (!Path.Exists(folderPath)) 
                Directory.CreateDirectory(folderPath);
            return folderPath;
        }

        public string GetErrorLogPath()
        {
            var folderPath = Path.Combine(
                        GetRootPath(),
                        "errors"
                        );
            if (!Path.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }
}
