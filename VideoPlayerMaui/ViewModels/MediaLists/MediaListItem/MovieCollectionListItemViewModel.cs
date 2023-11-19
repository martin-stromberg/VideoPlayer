using System;
using System.Linq;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
{
    public class MovieCollectionListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public MovieCollectionListItemViewModel(
            MovieCollection movieCollection,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader)
            : base(movieCollection, statusPublisher, navigationManager, settingsService, mediaDownloader)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenMovieCollection(Item as MovieCollection);
        }

        protected override bool CanStartPlayback()
        {
            return true;
        }

        protected override async void ExecuteStartPlayback()
        {
            await _PlaylistManager.StartMoviePlaylistAsync(Item as MovieCollection);
            await NavigationManager.OpenPlaylistPlaybackAsync();
        }

    }
}
