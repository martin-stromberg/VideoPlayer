using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.VideoPlayer
{
    public class VideoPlayerViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;

        public VideoPlayerViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager)
        {
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
            }
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
            if (closing && (Item != null) && (Item.CopyType == MediaItemCopyType.Cache))
                _MediaLibrary.RemoveMediaItemAsync(Item);
        }

        public void ProcessMediaOpened() { }

        public void ProcessMediaEnded()
        {
            NavigationManager.NavigateBack();
            VideoSource = null;
        }

        public void ProcessMediaFailed(string errorMessage) { }

        public void ProcessSeekCompleted(TimeSpan position) { }

        public void ProcessPositionChanged(TimeSpan position) { }

    }
}
