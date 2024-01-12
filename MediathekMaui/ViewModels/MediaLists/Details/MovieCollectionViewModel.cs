using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.Details
{
    public class MovieCollectionViewModel: BaseMediaListViewModel
    {

        public MovieCollectionViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager) { }

    }
}
