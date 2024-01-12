using Mediathek.Navigation;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.Management
{
    public class BaseManagementContentViewModel: BaseViewModel
    {

        public BaseManagementContentViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService) { }

        public bool Visible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

    }
}
