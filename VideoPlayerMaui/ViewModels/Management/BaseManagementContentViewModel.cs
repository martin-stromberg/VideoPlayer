using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class BaseManagementContentViewModel: BaseViewModel
    {

        public BaseManagementContentViewModel(IStatusPublisher statusPublisher, INavigationManager navigationManager)
            : base(statusPublisher, navigationManager) { }

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
