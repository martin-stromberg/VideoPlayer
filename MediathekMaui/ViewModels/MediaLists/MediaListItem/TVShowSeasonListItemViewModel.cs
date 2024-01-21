using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
{
    public class TVShowSeasonListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public TVShowSeasonListItemViewModel(
            TVShowSeason season,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(season, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenTVShowSeason(Item as TVShowSeason);
        }

        public override async void OpenCategory()
        {
            var show = await MediaLibrary.GetTVShow((Item as TVShowSeason).ShowId);
            NavigationManager.OpenTVShow(show, Item as TVShowSeason, null);
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

        protected override void ExecuteSaveNewItem()
        {
            throw new NotImplementedException();
        }

        protected override void ExecuteCancelNewItem()
        {
            throw new NotImplementedException();
        }

    }
}
