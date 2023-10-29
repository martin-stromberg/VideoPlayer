using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Homepage
{
    public class HomePageViewModel: BaseViewModel
    {

        public HomePageViewModel(IStatusPublisher statusPublisher, INavigationManager navigationManager)
            : base(statusPublisher, navigationManager)
        {
            LatestViews = new LatestViewsViewModel(StatusPublisher, navigationManager);
            OpenCategory = new Command((sender) => DoOpenCategory(sender));
        }

        public Command OpenCategory { get; }

        public LatestViewsViewModel LatestViews { get; }

        private void DoOpenCategory(object cmd)
        {
            switch ((string)cmd)
            {
                case "movies":
                    NavigationManager.OpenMovies();
                    break;
                case "tvshows":
                    NavigationManager.OpenTVShows();
                    break;
            }
        }

    }
}
