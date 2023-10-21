using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class BaseManagementContentViewModel: BaseViewModel
    {

        public BaseManagementContentViewModel(IStatusPublisher statusPublisher)
            : base(statusPublisher) { }

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
