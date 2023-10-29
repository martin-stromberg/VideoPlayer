using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Navigation
{
    public interface INavigationManager
    {

        void OpenMovies();

        void OpenMovieCollection(MovieCollection movieCollection);

        Task OpenMovie(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        void OpenTVShows();

        void OpenTVShow(TVShow tVShow);

        void OpenTVShowSeason(TVShowSeason tVShowSeason);

        Task OpenTVShowEpisodeAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        void NavigateBack();

        Task OpenPlaylistPlaybackAsync();

    }
}
