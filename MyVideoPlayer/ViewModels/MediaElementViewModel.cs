using CommunityToolkit.Maui.Views;
using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels
{
    public class MediaElementViewModel : BaseViewModel, IMediaElementViewModel
    {

        public void MediaEnded()
        {
            OnMediaEnded?.Invoke(this, VideoSource);
            VideoSource = null;
        }
        public event EventHandler<MediaSource> OnMediaEnded;

        public void MediaFailed(string errorMessage)
        {
            VideoSource = null;
        }
        public void MediaOpened()
        {

        }
        public void PositionChanged(TimeSpan position)
        {

        }
        public void SeekCompleted()
        {

        }

        public bool IsPlaying()
        {
            return VideoSource != null;
        }

        public void StopPlaying()
        {
            OnMediaEnded?.Invoke(this, VideoSource);
            VideoSource = null;
        }

        public void Play(MediaSource mediaSource)
        {
            VideoSource = mediaSource;
        }

        public bool VideoVisible
        {
            get { return GetProperty<bool>(); }
            set { SetProperty<bool>(value); }
        }
        public MediaSource VideoSource
        {
            get { return GetProperty<MediaSource>(); }
            set
            {
                SetProperty<MediaSource>(value);
                VideoVisible = value != null;
            }
        }
    }
}
