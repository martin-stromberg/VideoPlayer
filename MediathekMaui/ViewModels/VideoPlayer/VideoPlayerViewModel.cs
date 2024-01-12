using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using Mediathek.Common;
using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.PlaybackHistory;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.ComponentModel;
using System.Linq;

namespace Mediathek.ViewModels.VideoPlayer
{
    public class VideoPlayerViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly IPlaylistManager _PlaylistManager;
        private readonly IPlaybackHistoryManager _PlaybackHistoryManager;

        public VideoPlayerViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            IPlaybackHistoryManager playbackHistoryManager,
            ISettingsService settings)
            : base(statusPublisher, navigationManager, settings)
        {
            _PlaybackHistoryManager = playbackHistoryManager;
            _PlaylistManager = playlistManager;
            _MediaLibrary = mediaLibrary;
            Navigate = new Command((arg) => DoNavigate((string)arg));
            ToggleFullScreen = new Command(() => ExecuteToggleFullScreen());
            UpdatePlayerControlStyle();
            IsWindowMode = true;
        }

        private void ExecuteToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;
        }

        protected override void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.SettingsPropertyChanged(sender, e);
            switch (e.PropertyName)
            {
                case nameof(Settings.Current.Player_ControlStyle):
                    UpdatePlayerControlStyle();
                    break;
            }
        }

        private void UpdatePlayerControlStyle()
        {
            switch (Settings.Current.Player_ControlStyle)
            {
                case Models.Settings.ControlStyle.Own:
                    DefaultPlaybackControlsActive = false;
                    OwnPlaybackControlsActive = true;
                    break;
                default:
                    DefaultPlaybackControlsActive = true;
                    OwnPlaybackControlsActive = false;
                    break;
            }
        }

        private bool IsRecentlySet = false;

        public MediaSource VideoSource
        {
            get
            {
                return GetProperty<MediaSource>();
            }
            set
            {
                if (value != null)
                    SetProperty<MediaSource>(null);
                ItemDuration = TimeSpan.Zero;
                IsRecentlySet = true;
                SetProperty<MediaSource>(value);
            }
        }

        public BaseModel TypedItem
        {
            get
            {
                return GetProperty<BaseModel>();
            }
            set
            {
                SetProperty<BaseModel>(value);
                Title = value?.Name ?? Item?.Name;
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
                Title = TypedItem?.Name ?? Item?.Name;
            }
        }

        public TimeSpan ItemDuration
        {
            get
            {
                return GetProperty<TimeSpan>();
            }
            set
            {
                SetProperty<TimeSpan>(value);
            }
        }

        private DownloadSource _Download = null;

        public float DownloadProgress
        {
            get
            {
                return GetProperty<float>();
            }
            set
            {
                SetProperty<float>(value);
                IsDownloadProgressVisible = (value > 0) && (value < 100);
            }
        }

        public bool IsDownloadProgressVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        private DownloadSource Download
        {
            get
            {
                return _Download;
            }
            set
            {
                if (_Download != null)
                {
                    _Download.SourceChanged -= _Download_SourceChanged;
                    _Download.Error -= _Download_Error;
                    _Download.ProgressChanged -= _Download_ProgressChanged;
                }
                _Download = value;
                _Download_SourceChanged(this, new MediaSourceEventArgs(_Download?.Source));
                if (_Download != null)
                {
                    _Download.SourceChanged += _Download_SourceChanged;
                    _Download.Error += _Download_Error;
                    _Download.ProgressChanged += _Download_ProgressChanged;
                }
            }
        }

        private void _Download_ProgressChanged(object sender, ProgressEventArgs e)
        {
            DownloadProgress = e.Progress;
        }

        private void _Download_Error(object sender, ExceptionEventArgs e) { }

        private void _Download_SourceChanged(object sender, MediaSourceEventArgs e)
        {
            Item = Download?.Item;
            TypedItem = Download?.TypedItem;
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

        public void ProcessMediaOpened(TimeSpan duration)
        {
            ItemDuration = duration;
            MaximumPosition = duration.TotalMicroseconds;
            ProcessOpened();
        }

        public async void ProcessMediaEnded()
        {
            if (Item == null)
                return;
            await SaveMediaItemPosition(TimeSpan.Zero);
            Download = _PlaylistManager.ProcessMediaEnded(Item);
            if (VideoSource == null)
                NavigationManager.NavigateBack();
        }

        public string ErrorMessage
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public void ProcessMediaFailed(string errorMessage)
        {
            _PlaylistManager.ProcessMediaFailed(Item);
            ErrorMessage = errorMessage;
        }

        public void ProcessSeekCompleted(TimeSpan position) { }

        public void ProcessPositionChanged(TimeSpan position)
        {
            IsPlaying = true;
            UpdateCurrentPosition(position);
            CheckSaveMediaItemPosition(position);
        }

        public double MaximumPosition
        {
            get
            {
                return GetProperty<double>();
            }
            private set
            {
                SetProperty<double>(value);
            }
        }

        private bool selfUpdatingPosition = false;

        public bool IsPlayable
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsPlaying
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsPlayable = false;
            }
        }

        public double CurrentPosition
        {
            get
            {
                return GetProperty<double>();
            }
            set
            {
                if (!selfUpdatingPosition)
                    OnSeekRequest(TimeSpan.FromMicroseconds(value));
            }
        }

        public TimeSpan CurrentPositionTime
        {
            get
            {
                return GetProperty<TimeSpan>();
            }
            set
            {
                SetProperty<TimeSpan>(value);
                RemainingPositionTime = ItemDuration - CurrentPositionTime;
            }
        }

        public TimeSpan RemainingPositionTime
        {
            get
            {
                return GetProperty<TimeSpan>();
            }
            private set
            {
                SetProperty<TimeSpan>(value);
            }
        }

        public Command Navigate { get; set; }

        public Command ToggleFullScreen { get; }

        private void UpdateCurrentPosition(TimeSpan position)
        {
            selfUpdatingPosition = true;
            CurrentPositionTime = position;
            SetProperty<double>(position.TotalMicroseconds, nameof(CurrentPosition));
            selfUpdatingPosition = false;
        }

        private TimeSpan LastSavedPosition = TimeSpan.Zero;

        private bool savingPosition = false;

        private async void CheckSaveMediaItemPosition(TimeSpan position)
        {
            if (Item == null)
                return;
            if (savingPosition)
                return;
            savingPosition = true;
            try
            {
                if (position.TotalSeconds < Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoStart)
                    return;

                var Duration = position - LastSavedPosition;
                if (Math.Abs(Duration.TotalSeconds) < Settings.Current.PlaybackHistory_SavePositionIntervallSeconds)
                    return;
                LastSavedPosition = position;

                if ((ItemDuration != TimeSpan.Zero)
                    && (ItemDuration.Subtract(TimeSpan.FromSeconds(Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoEnding)) < position))
                    position = TimeSpan.Zero;

                await SaveMediaItemPosition(position);
                await StoreInHistory(position);
            }
            finally
            {
                savingPosition = false;
            }
        }

        private async Task StoreInHistory(TimeSpan position)
        {
            if (position == TimeSpan.Zero)
                await _PlaybackHistoryManager.Finish(Item, TypedItem);
            else
                await _PlaybackHistoryManager.Add(Item, TypedItem);
        }

        private async Task SaveMediaItemPosition(TimeSpan position)
        {
            if (Item == null)
                return;
            if (Item.LastPlaybackPosition == position)
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
                case MediaElementState.Paused:
                    ProcessPaused();
                    break;
                case MediaElementState.Stopped:
                    ProcessStopped();
                    break;
                case MediaElementState.Opening:
                    ProcessOpening();
                    break;
                case MediaElementState.Buffering:
                    ProcessBuffering();
                    break;
                case MediaElementState.Failed:
                    ProcessFailed();
                    break;
            }
        }

        private void ProcessFailed()
        {
            IsPlaying = false;
            IsPlayable = false;
        }

        private void ProcessBuffering()
        {
            IsPlaying = false;
            IsPlayable = false;
        }

        private void ProcessOpening()
        {
            IsPlaying = false;
            IsPlayable = false;
        }

        private void ProcessOpened()
        {
            IsPlayable = true;
            IsPlaying = false;
        }

        private void ProcessPlaying()
        {
            IsPlaying = true;
            if (IsRecentlySet && (Item != null) && (Item.LastPlaybackPosition != TimeSpan.Zero))
                OnSeekRequest(Item.LastPlaybackPosition);
            IsRecentlySet = false;
        }

        private void ProcessStopped()
        {
            IsPlaying = false;
            IsPlayable = true;
        }

        private void ProcessPaused()
        {
            IsPlaying = false;
            IsPlayable = true;
        }

        private void DoNavigate(string arg)
        {
            switch (arg)
            {
                case "back":
                    NavigationManager.NavigateBack();
                    break;
            }
        }

        public bool PlaybackControlsVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public void TogglePlaybackControls()
        {
            PlaybackControlsVisible = !PlaybackControlsVisible && OwnPlaybackControlsActive;
        }

        public void ShowPlaybackControls(bool value)
        {
            PlaybackControlsVisible = OwnPlaybackControlsActive;
        }

        public void SeekToEndAsync(TimeSpan duration)
        {
            OnSeekRequest(duration.Subtract(TimeSpan.FromSeconds(5)));
        }

        public bool OwnPlaybackControlsActive
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public bool DefaultPlaybackControlsActive
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsFullScreen
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsWindowMode = false;
                else if (!IsWindowMode)
                    IsWindowMode = true;
            }
        }

        public bool IsWindowMode
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
                if (value)
                    IsFullScreen = false;
            }
        }

    }
}
