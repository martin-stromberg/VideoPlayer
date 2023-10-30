using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class SettingsViewModel: BaseManagementContentViewModel
    {

        public SettingsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService)
        {
            Title = $"Einstellungen";
        }

    }
}
