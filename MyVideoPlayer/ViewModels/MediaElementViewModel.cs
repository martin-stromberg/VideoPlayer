using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyVideoPlayer.ViewModels
{
    public class MediaElementViewModel : BaseViewModel, IMediaElementViewModel
    {
        public void MediaEnded()
        {
            VideoSource = null;
        }
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
