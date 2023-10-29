using VideoPlayer.Models.TVShows;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Playlists;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class TVShowSeasonListItemViewModel: MediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public TVShowSeasonListItemViewModel(
            TVShowSeason season,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager)
            : base(season, statusPublisher, navigationManager)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShowSeason(Item as TVShowSeason);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override async void ExecuteStartPlayback()
        {
            await _PlaylistManager.StartTVShowPlaylistAsync(Item as TVShowSeason);
            await NavigationManager.OpenPlaylistPlaybackAsync();
        }

    }
}
