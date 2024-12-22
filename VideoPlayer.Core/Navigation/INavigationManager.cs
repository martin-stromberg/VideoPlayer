using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.Navigation
{
    public interface INavigationManager
    {

        #region Overviews
        void OpenMovies();
        void OpenTVShows();
        void OpenActorsOverview();
        #endregion

        #region Detail Card
        void OpenCard(BaseListItem vm, bool autoPlay);
        void CloseCurrentPage();
        #endregion

        //void OpenMovieCollection(MovieCollection movieCollection);

        //void OpenMovie(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        //void OpenTVShowCollection(TVShowCollection tVShowCollection);

        //void OpenTVShow(TVShow show, TVShowSeason season, TVShowEpisode tVShowEpisode);

        //void OpenTVShowSeason(TVShowSeason tVShowSeason);

        //Task OpenTVShowEpisodeAsync(TVShowEpisode tVShowEpisode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        //Task OpenMediaItemAsync(MediaItem mediaItem);

        //Task OpenMediaItemDetailsAsync(MediaItem mediaItem);

        //void NavigateBack();

        //Task OpenPlaylistPlaybackAsync();

        //Task OpenPlaylistPlaybackAsync(Playlist playlist, TVShowEpisode tVShowEpisode);

        //void OpenUncategrized();

        //void OpenDownloads();

    }
}
