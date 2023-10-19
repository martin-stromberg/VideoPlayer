using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;

namespace MyVideoPlayer.Helper
{
    public class UserSecrets
    {

        public UserSecrets()
            : base() { }

        private JObject? Configuration { get; } = GetConfiguration("secrets.json");

        private static JObject? GetConfiguration(string filePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? assemblyName = assembly.GetName().Name;
            using Stream? fileStream = assembly.GetManifestResourceStream($"{assemblyName}.{filePath}");
            if (fileStream == null)
            {
                return null;
            }

            using StreamReader sr = new(fileStream);
            string fileContent = sr.ReadToEnd();
            return JObject.Parse(fileContent);
        }

        public string SyncfusionLicenseKey => Configuration?.Value<string>(nameof(SyncfusionLicenseKey)) ?? throw new NullReferenceException(nameof(SyncfusionLicenseKey));

        public string RespberryPiPassword => Configuration?.Value<string>(nameof(RespberryPiPassword)) ?? throw new NullReferenceException(nameof(RespberryPiPassword));

    }
}
