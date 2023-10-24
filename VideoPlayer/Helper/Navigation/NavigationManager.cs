using System;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Views.MediaLists;

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

        protected void NavigateToRoute(string route, Dictionary<string, object> args = null)
        {
            if (args == null)
                Shell.Current.GoToAsync(route);
            else
                Shell.Current.GoToAsync(route, args);
        }

        public void OpenTVShow(TVShow show)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Show", show }
            };
            NavigateToRoute($"tvshows", navigationParameter);
        }

        public void OpenTVShowSeason(TVShowSeason tVShowSeason)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Season", tVShowSeason }
            };
            NavigateToRoute($"tvshows", navigationParameter);
        }

        public void OpenTVShowEpisode(TVShowEpisode tVShowEpisode)
        {
            throw new NotImplementedException();
        }

        public void OpenMovieCollection(MovieCollection movieCollection)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Collection", movieCollection }
            };
            NavigateToRoute($"movies", navigationParameter);
        }

        void INavigationManager.OpenMovie(Movie movie) { }

    }
}
