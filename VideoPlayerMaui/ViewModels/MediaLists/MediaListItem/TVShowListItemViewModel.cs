using System;
using System.Linq;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public TVShowListItemViewModel(
            TVShow show,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(show, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShow(Item as TVShow, null, null);
        }

        public override void OpenCategory()
        {
            NavigationManager.OpenTVShow(Item as TVShow, null, null);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override async void ExecuteStartPlayback()
        {
            await _PlaylistManager.StartTVShowPlaylistAsync(Item as TVShow);
            await NavigationManager.OpenPlaylistPlaybackAsync();
        }

    }
}
