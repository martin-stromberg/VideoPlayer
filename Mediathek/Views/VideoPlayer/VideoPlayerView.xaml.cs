using Mediathek.Common;
using Mediathek.Models;
using Mediathek.Services.Playlists;
using Mediathek.ViewModels.VideoPlayer;
using System.ComponentModel;

namespace Mediathek.Views.VideoPlayer
{
    public partial class VideoPlayerView: ContentView
    {

        public VideoPlayerView()
        {
            InitializeComponent();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            ViewModel = BindingContext as VideoPlayerViewModel;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.DefaultPlaybackControlsActive):
                    Video.ShouldShowPlaybackControls = ViewModel.DefaultPlaybackControlsActive;
                    Shell.SetNavBarIsVisible(this, ViewModel.DefaultPlaybackControlsActive);
                    break;
            }
        }

        private TimeSpan positionToSeek = TimeSpan.Zero;

        private void ViewModel_SeekRequest(object sender, TimeSpanEventArgs e)
        {
            Video.SeekTo(e.Position);
            positionToSeek = e.Position;
        }

        private VideoPlayerViewModel viewModel;

        public VideoPlayerViewModel ViewModel
        {
            get
            {
                return viewModel;
            }
            set
            {
                if (viewModel != null)
                {
                    viewModel.SeekRequest -= ViewModel_SeekRequest;
                    viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }
                viewModel = value;
                if (viewModel != null)
                {
                    viewModel.SeekRequest += ViewModel_SeekRequest;
                    viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(viewModel.DefaultPlaybackControlsActive)));
                }
            }
        }

        public string VideoSource { get; set; }

        private DownloadSource _DownloadSource;

        public DownloadSource DownloadSource
        {
            get
            {
                return _DownloadSource;
            }
            set
            {
                if (_DownloadSource != null)
                {
                    _DownloadSource.SourceChanged -= _DownloadSource_SourceChanged;
                    _DownloadSource.Error -= _DownloadSource_Error;
                }
                _DownloadSource = value;
                if (_DownloadSource != null)
                {
                    _DownloadSource.SourceChanged += _DownloadSource_SourceChanged;
                    _DownloadSource.Error += _DownloadSource_Error;
                }
            }
        }

        private void _DownloadSource_Error(object sender, ExceptionEventArgs e)
        {
            ViewModel.ProcessMediaFailed(e.Error.Message);
        }

        private void _DownloadSource_SourceChanged(object sender, MediaSourceEventArgs e)
        {
            ViewModel.Item = DownloadSource.Item;
            ViewModel.VideoSource = e.Source;
        }

        // protected override void OnAppearing()
        // {
        // base.OnAppearing();
        // if (!string.IsNullOrWhiteSpace(VideoSource))
        // ViewModel.VideoSource = MediaSource.FromFile(VideoSource);
        // ViewModel.OnAppeared();
        // }

        // protected override void OnDisappearing()
        // {
        // base.OnDisappearing();
        // Shell.SetNavBarIsVisible(this, true);
        // ViewModel.OnDisappeared(true);
        // }

        private void Video_MediaOpened(object sender, EventArgs e)
        {
            ViewModel.ProcessMediaOpened(Video.Duration);
        }

        private void Video_MediaEnded(object sender, EventArgs e)
        {
            ViewModel.ProcessMediaEnded();
        }

        private void Video_MediaFailed(object sender, MediaFailedEventArgs e)
        {
            ViewModel.ProcessMediaFailed(e.ErrorMessage);
        }

        private void Video_SeekCompleted(object sender, EventArgs e)
        {
            ViewModel.ProcessSeekCompleted(Video.Position);
        }

        private void Video_PositionChanged(object sender, MediaPositionChangedEventArgs e)
        {
            if (positionToSeek != TimeSpan.Zero)
            {
                Video.SeekTo(positionToSeek);
                positionToSeek = TimeSpan.Zero;
            }
            if (ViewModel.ItemDuration != Video.Duration)
                ViewModel.ProcessMediaOpened(Video.Duration);
            if (Video.CurrentState == MediaElementState.Playing)
                ViewModel.ProcessPositionChanged(e.Position);
        }

        private void Video_StateChanged(object sender, MediaStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case MediaElementState.Playing:
                    ViewModel.ProcessMediaOpened(Video.Duration);
                    break;
            }
            ViewModel.ProcessStateChanged(e.PreviousState, e.NewState);
        }

        private void OnNavigateButtonClicked(object sender, EventArgs e)
        {
            if (ViewModel.Navigate.CanExecute("back"))
                ViewModel.Navigate.Execute("back");
        }

        private TimeSpan SeekDuration = TimeSpan.FromSeconds(30);

        private void OnPlaybackButtonClicked(object sender, EventArgs e)
        {
            switch ((sender as ImageButton).CommandParameter)
            {
                case "previous":
                    Video.SeekTo(TimeSpan.Zero);
                    break;
                case "left":
                    Video.SeekTo(Video.Position.Subtract(SeekDuration));
                    break;
                case "play":
                    Video.Play();
                    break;
                case "pause":
                    Video.Pause();
                    break;
                case "right":
                    Video.SeekTo(Video.Position.Add(SeekDuration));
                    break;
                case "next":
                    ViewModel.SeekToEndAsync(Video.Duration);
                    break;
            }
        }

        private void OnTapGestureRecognizerTapped(object sender, TappedEventArgs e)
        {
            ViewModel.TogglePlaybackControls();
        }

        private void OnTapGestureRecognizerDoubleTapped(object sender, TappedEventArgs e)
        {
            ViewModel.ShowPlaybackControls(true);
            switch (Video.CurrentState)
            {
                case MediaElementState.Playing:
                    Video.Pause();
                    break;
                case MediaElementState.Paused:
                    Video.Play();
                    break;
            }
        }

        private void TouchRoutingEffect_TouchAction(object sender, Helper.Touch.TouchActionEventArgs args)
        {
        }

    }
}