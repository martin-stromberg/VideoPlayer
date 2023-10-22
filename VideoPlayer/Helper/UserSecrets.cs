using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;
using VideoPlayer.Services.MediaLibrary.Demo;

namespace VideoPlayer.Helper
{

    public class UserSecrets: IUserSecrets
    {

        public UserSecrets()
            : base() { }

        private JObject Configuration { get; } = GetConfiguration("secrets.json");

        private static JObject GetConfiguration(string filePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string assemblyName = assembly.GetName().Name;

            var ressources = assembly.GetManifestResourceNames();

            Stream resourceStream = assembly.GetManifestResourceStream($"{assemblyName}.{filePath}");
            try
            {
                if (resourceStream == null)
                {
                    resourceStream = FindFile(assembly, $"{filePath}");
                    if (resourceStream == null)
                        throw new ApplicationException($"Embedded Ressource \"{assemblyName}.{filePath}\" was not found.");
                }
                using (StreamReader sr = new(resourceStream))
                {
                    string resourceContent = sr.ReadToEnd();
                    return JObject.Parse(resourceContent);
                }
            }
            finally
            {
                if (resourceStream != null)
                    resourceStream.Close();
            }
        }

        private static Stream FindFile(Assembly assembly, string fileName)
        {
            var folder = Path.GetDirectoryName(assembly.Location);
            var filePath = Path.Combine(folder, fileName);
            if (!File.Exists(filePath))
                return null;
            return new FileStream(filePath, FileMode.Open);
        }

        public string SyncfusionLicenseKey => Configuration?.Value<string>(nameof(SyncfusionLicenseKey)) ?? throw new NullReferenceException(nameof(SyncfusionLicenseKey));

        public string RespberryPiPassword => Configuration?.Value<string>(nameof(RespberryPiPassword)) ?? throw new NullReferenceException(nameof(RespberryPiPassword));

    }
}
