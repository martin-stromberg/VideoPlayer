using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
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
