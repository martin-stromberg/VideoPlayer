using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class SettingsViewModel: BaseManagementContentViewModel
    {

        public SettingsViewModel(IStatusPublisher statusPublisher)
            : base(statusPublisher)
        {
            Title = $"Einstellungen";
        }

    }
}
