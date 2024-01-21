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
    public class MovieCollectionListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly IPlaylistManager _PlaylistManager;

        public MovieCollectionListItemViewModel(
            MovieCollection movieCollection,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(movieCollection, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _PlaylistManager = playlistManager;
        }

        public override void OpenDetails()
        {
            NavigationManager.OpenMovieCollection(Item as MovieCollection);
        }

        public override void OpenCategory()
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
