using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists
{
    public class BaseMediaListViewModel: BaseViewModel
    {

        public BaseMediaListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, settingsService)
        {
            DownloadManager = downloadManager;
            PlaylistManager = playlistManager;
            MediaLibrary = mediaLibrary;
            MediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;

            AddCollection = new Command(() => ExecuteAddCollection());
        }

        private void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            var mediaItem = e.Element as MediaItem;
            if (mediaItem != null)
                ProcessMediaItemRemoved(mediaItem);
            var tvshow = e.Element as TVShow;
            if (tvshow is not null)
                ProcessTVShowRemoved(tvshow);
            var tvshowcollection = e.Element as TVShowCollection;
            if (tvshowcollection is not null)
                ProcessTVShowCollectionRemoved(tvshowcollection);
        }

        protected virtual void ProcessTVShowCollectionRemoved(TVShowCollection collection) { }

        protected virtual void ProcessTVShowRemoved(TVShow show) { }

        protected virtual void ProcessMediaItemRemoved(MediaItem mediaItem) { }

        protected IMediaLibrary MediaLibrary { get; }

        public IPlaylistManager PlaylistManager { get; }

        public IDownloadManager DownloadManager { get; }

        public ObservableCollection<BaseMediaListItemViewModel> Items { get; } = new ObservableCollection<BaseMediaListItemViewModel>();

        public Command AddCollection { get; set; }

        protected virtual void ExecuteAddCollection() { }

    }
}
