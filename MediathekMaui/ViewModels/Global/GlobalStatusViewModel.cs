using Mediathek.Navigation;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.Global
{
    public class GlobalStatusViewModel: BaseViewModel
    {

        public GlobalStatusViewModel(
            IStatusSubscriber statusSubscriber,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(null, navigationManager, settingsService)
        {
            statusSubscriber.StatusChanged += (sender, e) => { StatusMessage = e.Message; };
        }

        public string StatusMessage
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

    }
}
