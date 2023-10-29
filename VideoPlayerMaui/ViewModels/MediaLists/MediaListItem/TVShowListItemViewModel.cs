using System;
using System.Linq;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Playlists;
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
            IPlaylistManager playlistManager)
            : base(show, statusPublisher, navigationManager)
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
