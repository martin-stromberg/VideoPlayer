using CommunityToolkit.Maui.Views;
using System;
using System.Linq;

namespace MyVideoPlayer.Helper.Navigation
{
    public class MediaSourceEventArgs : EventArgs
    {
        public MediaSourceEventArgs(MediaSource mediaSource)
            : base()
        {
            MediaSource = mediaSource;
        }

        public MediaSource MediaSource { get; }
    }
}
