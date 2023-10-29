using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Homepage
{
    public class LatestViewsViewModel: BaseViewModel
    {

        public LatestViewsViewModel(IStatusPublisher statusPublisher, INavigationManager navigationManager)
            : base(statusPublisher, navigationManager) { }

    }
}
