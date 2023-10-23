using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class SettingsViewModel: BaseManagementContentViewModel
    {

        public SettingsViewModel(IStatusPublisher statusPublisher, INavigationManager navigationManager)
            : base(statusPublisher, navigationManager)
        {
            Title = $"Einstellungen";
        }

    }
}
