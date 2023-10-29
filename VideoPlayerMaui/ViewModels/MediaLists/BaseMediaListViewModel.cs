using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.Playlists;
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
            IPlaylistManager playlistManager)
            : base(statusPublisher, navigationManager)
        {
            PlaylistManager = playlistManager;
            MediaLibrary = mediaLibrary;
        }

        protected IMediaLibrary MediaLibrary { get; }

        public IPlaylistManager PlaylistManager { get; }

        public ObservableCollection<MediaListItemViewModel> Items { get; } = new ObservableCollection<MediaListItemViewModel>();

    }
}
