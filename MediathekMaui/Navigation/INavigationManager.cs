using System;
using System.Linq;

namespace Mediathek.Navigation
{
    public interface INavigationManager
    {

        void OpenMovies();

        void OpenMovieCollection(MovieCollection movieCollection);

        void OpenMovie(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        void OpenTVShows();

        void OpenTVShowCollection(TVShowCollection tVShowCollection);

        void OpenTVShow(TVShow show, TVShowSeason season, TVShowEpisode tVShowEpisode);

        void OpenTVShowSeason(TVShowSeason tVShowSeason);

        Task OpenTVShowEpisodeAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task OpenMediaItemAsync(MediaItem mediaItem);

        Task OpenMediaItemDetailsAsync(MediaItem mediaItem);

        void NavigateBack();

        Task OpenPlaylistPlaybackAsync();

        Task OpenPlaylistPlaybackAsync(Playlist playlist, TVShowEpisode tVShowEpisode);

        void OpenUncategrized();

        void OpenDownloads();

    }
}
