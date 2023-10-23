using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Views;

namespace VideoPlayer.Helper.Navigation
{
    internal class NavigationManager: INavigationManager
    {

        public NavigationManager()
        {
            Routing.RegisterRoute("movies", typeof(MoviesPage));
            Routing.RegisterRoute("tvshows", typeof(TVShowsPage));
        }

        public void OpenMovies()
        {
            NavigateToRoute("movies");
        }

        public void OpenTVShows()
        {
            NavigateToRoute("tvshows");
        }

        protected void NavigateToRoute(string route)
        {
            Shell.Current.GoToAsync(route);
        }

    }
}
