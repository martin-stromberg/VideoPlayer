using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Playlists;
using VideoPlayer.ViewModels.VideoPlayer;

namespace VideoPlayer.Views.VideoPlayer
{
    [QueryProperty(nameof(MediaItem), "MediaItem")]
    [QueryProperty(nameof(VideoSource), "VideoSource")]
    [QueryProperty(nameof(DownloadSource), "DownloadSource")]
    public partial class VideoPlayerPage: ContentPage
    {

        public VideoPlayerPage()
        {
            InitializeComponent();
            BindingContext = ViewModel = App.GetService<VideoPlayerViewModel>();
        }

        public VideoPlayerViewModel ViewModel { get; }

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
                }
                _DownloadSource = value;
                if (_DownloadSource != null)
                {
                    _DownloadSource.SourceChanged += _DownloadSource_SourceChanged;
                }
            }
        }

        private void _DownloadSource_SourceChanged(object sender, MediaSourceEventArgs e)
        {
            ViewModel.Item = DownloadSource.Item;
            ViewModel.VideoSource = e.Source;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!string.IsNullOrWhiteSpace(VideoSource))
                ViewModel.VideoSource = MediaSource.FromFile(VideoSource);
            ViewModel.OnAppeared();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ViewModel.OnDisappeared(true);
        }

        private void Video_MediaOpened(object sender, EventArgs e)
        {
            ViewModel.ProcessMediaOpened();
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
            ViewModel.ProcessPositionChanged(e.Position);
        }

    }
}