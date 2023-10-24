using System;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Navigation
{
    public interface INavigationManager
    {

        void OpenMovies();

        void OpenMovieCollection(MovieCollection movieCollection);

        void OpenMovie(Movie movie);

        void OpenTVShows();

        void OpenTVShow(TVShow tVShow);

        void OpenTVShowSeason(TVShowSeason tVShowSeason);

        void OpenTVShowEpisode(TVShowEpisode tVShowEpisode);

    }
}
