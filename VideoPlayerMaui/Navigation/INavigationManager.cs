using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
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

        void OpenTVShow(TVShow show, TVShowSeason season, TVShowEpisode tVShowEpisode);

        void OpenTVShowSeason(TVShowSeason tVShowSeason);

        Task OpenTVShowEpisodeAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task OpenMediaItemAsync(MediaItem mediaItem);

        Task OpenMediaItemDetailsAsync(MediaItem mediaItem);

        void NavigateBack();

        Task OpenPlaylistPlaybackAsync();

        void OpenUncategrized();

        void OpenDownloads();

    }
}
