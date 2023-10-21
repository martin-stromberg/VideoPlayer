using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Homepage
{
    public class HomePageViewModel: BaseViewModel
    {

        public HomePageViewModel(IStatusPublisher statusPublisher)
            : base(statusPublisher) { }

    }
}
