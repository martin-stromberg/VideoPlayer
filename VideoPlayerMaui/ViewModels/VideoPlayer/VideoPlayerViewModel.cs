using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.Playlists;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.VideoPlayer
{
    public class VideoPlayerViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly IPlaylistManager _PlaylistManager;

        public VideoPlayerViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager)
            : base(statusPublisher, navigationManager)
        {
            _PlaylistManager = playlistManager;
            _MediaLibrary = mediaLibrary;
        }

        public MediaSource VideoSource
        {
            get
            {
                return GetProperty<MediaSource>();
            }
            set
            {
                SetProperty<MediaSource>(value);
            }
        }

        public MediaItem Item
        {
            get
            {
                return GetProperty<MediaItem>();
            }
            set
            {
                SetProperty<MediaItem>(value);
                Title = value?.Name;
            }
        }

        private DownloadSource _Download = null;

        private DownloadSource Download
        {
            get
            {
                return _Download;
            }
            set
            {
                if (_Download != null)
                    _Download.SourceChanged -= _Download_SourceChanged;
                _Download = value;
                _Download_SourceChanged(this, new MediaSourceEventArgs(_Download?.Source));
                if (_Download != null)
                    _Download.SourceChanged += _Download_SourceChanged;
            }
        }

        private void _Download_SourceChanged(object sender, MediaSourceEventArgs e)
        {
            Item = Download?.Item;
            VideoSource = e.Source;
        }

        private void LoadFirstPlaylistVideo()
        {
            Download = _PlaylistManager.GetFirstVideoSource();
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            if (VideoSource == null)
                LoadFirstPlaylistVideo();
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
            VideoSource = null;
        }

        public void ProcessMediaOpened() { }

        public void ProcessMediaEnded()
        {
            Download = _PlaylistManager.ProcessMediaEnded(Item);
            if (VideoSource == null)
                NavigationManager.NavigateBack();
        }

        public void ProcessMediaFailed(string errorMessage) { }

        public void ProcessSeekCompleted(TimeSpan position) { }

        public void ProcessPositionChanged(TimeSpan position)
        {
            SaveMediaItemPosition(position);
        }

        private TimeSpan LastSavedPosition = TimeSpan.Zero;

        private bool savingPosition = false;

        private async void SaveMediaItemPosition(TimeSpan position)
        {
            if (Item == null)
                return;
            if (savingPosition)
                return;
            savingPosition = true;
            try
            {
                var Duration = position - LastSavedPosition;
                if (Duration.TotalSeconds < 5)
                    return;
                Item.LastPlaybackPosition = position;
                await _MediaLibrary.UpdateMediaItemAsync(Item, false);

                if (Item.CopyType != MediaItemCopyType.None)
                {
                    var OriginalItem = await _MediaLibrary.GetOriginalMediaItemsAsync(Item);
                    OriginalItem.LastPlaybackPosition = position;
                    await _MediaLibrary.UpdateMediaItemAsync(OriginalItem, false);
                }
            }
            finally
            {
                savingPosition = false;
            }
        }

        public event EventHandler<TimeSpanEventArgs> SeekRequest;

        protected void OnSeekRequest(TimeSpan position)
        {
            SeekRequest?.Invoke(this, new TimeSpanEventArgs(position));
        }

        public void ProcessStateChanged(MediaElementState previousState, MediaElementState newState)
        {
            switch (newState)
            {
                case MediaElementState.Playing:
                    ProcessPlaying();
                    break;
            }
        }

        private void ProcessPlaying()
        {
            if ((Item != null) && (Item.LastPlaybackPosition != TimeSpan.Zero))
                OnSeekRequest(Item.LastPlaybackPosition);
        }

    }
}
