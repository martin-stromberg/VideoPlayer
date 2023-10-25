using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Global
{
    public class GlobalStatusViewModel: BaseViewModel
    {

        public GlobalStatusViewModel(IStatusSubscriber statusSubscriber, INavigationManager navigationManager)
            : base(null, navigationManager)
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
