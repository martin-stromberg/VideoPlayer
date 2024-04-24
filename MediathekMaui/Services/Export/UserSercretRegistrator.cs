using Mediathek.Services.MediaLibrary.Demo;
using Syncfusion.Licensing;
using System;
using System.Linq;

namespace Mediathek.Services.Export
{
    public class UserSercretRegistrator
    {

        private static bool syncfusionRegistered = false;
        private readonly IUserSecrets _UserSecrets;

        private static void RegisterSyncfusion(string key)
        {
            if (syncfusionRegistered)
                return;
            SyncfusionLicenseProvider.RegisterLicense(key);
            syncfusionRegistered = true;
        }

        public UserSercretRegistrator(IUserSecrets userSecrets)
        {
            _UserSecrets = userSecrets;
        }

        public async void RunAsync()
        {
            try
            {
                await _UserSecrets.Initialize();

                RegisterSyncfusion(_UserSecrets.SyncfusionLicenseKey);
            }
            catch { }
        }

    }
}
