using System;
using System.Linq;
using System.Reflection;

namespace VideoPlayer.Services.MediaLibrary
{
    public class MediaLibraryEnvironment
    {

        public MediaLibraryEnvironment(string resourcesPath)
        {
            RessourcePath = resourcesPath;
        }

        private string cacheRootPath = string.Empty;
        private string cacheFolderPath = string.Empty;
        private string tempFolderPath = string.Empty;
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

        public string CacheRootPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(cacheRootPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, ProductName);
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
                if (string.IsNullOrWhiteSpace(cacheFolderPath))
                {
                    var folder = Path.Combine(CacheRootPath, "Cache");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    cacheFolderPath = folder;
                }
                return cacheFolderPath;
            }
        }

        public string TempFolderPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(tempFolderPath))
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    folder = Path.Combine(folder, ProductName, "Temp");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    tempFolderPath = folder;
                }
                return tempFolderPath;
            }
        }

        public string RessourcePath { get; private set; }

    }
}
