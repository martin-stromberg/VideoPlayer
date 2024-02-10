using Mediathek.Services.MediaLibrary.Demo;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;

namespace MediaPlayer.Helper
{

    public class UserSecrets: IUserSecrets
    {

        public UserSecrets()
            : base() { }

        public async Task Initialize()
        {
            Configuration = await GetConfigurationAsync("secrets.json");
        }

        protected JObject Configuration { get; private set; } = new JObject();

        private static async Task<JObject> GetConfigurationAsync(string filePath)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("secrets.json");
                using var reader = new StreamReader(stream);
                var contents = reader.ReadToEnd();
                return JObject.Parse(contents);
            }
            catch { }

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
            catch
            {
                return null;
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
