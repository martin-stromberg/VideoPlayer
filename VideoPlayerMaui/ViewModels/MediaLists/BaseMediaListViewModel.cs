using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class BaseMediaListViewModel: BaseViewModel
    {

        public BaseMediaListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IMediaDownloader mediaDownloader)
            : base(statusPublisher, navigationManager, settingsService)
        {
            MediaDownloader = mediaDownloader;
            PlaylistManager = playlistManager;
            MediaLibrary = mediaLibrary;
        }

        protected IMediaLibrary MediaLibrary { get; }

        public IPlaylistManager PlaylistManager { get; }

        public IMediaDownloader MediaDownloader { get; }

        public ObservableCollection<BaseMediaListItemViewModel> Items { get; } = new ObservableCollection<BaseMediaListItemViewModel>();

    }
}
