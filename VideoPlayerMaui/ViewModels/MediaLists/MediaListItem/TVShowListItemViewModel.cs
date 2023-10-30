using System;
using System.Linq;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowListItemViewModel: MediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public TVShowListItemViewModel(
            TVShow show,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager,
            ISettingsService settingsService)
            : base(show, statusPublisher, navigationManager, settingsService)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShow(Item as TVShow);
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
