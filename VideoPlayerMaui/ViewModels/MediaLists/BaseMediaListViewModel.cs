using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.TVShows;
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
            MediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
        }

        private void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            var mediaItem = e.Element as MediaItem;
            if (mediaItem != null)
                ProcessMediaItemRemoved(mediaItem);
            var tvshow = e.Element as TVShow;
            if (tvshow is not null)
                ProcessTVShowRemoved(tvshow);
        }

        protected virtual void ProcessTVShowRemoved(TVShow show) { }
        protected virtual void ProcessMediaItemRemoved(MediaItem mediaItem) { }

        protected IMediaLibrary MediaLibrary { get; }

        public IPlaylistManager PlaylistManager { get; }

        public IMediaDownloader MediaDownloader { get; }

        public ObservableCollection<BaseMediaListItemViewModel> Items { get; } = new ObservableCollection<BaseMediaListItemViewModel>();

    }
}
