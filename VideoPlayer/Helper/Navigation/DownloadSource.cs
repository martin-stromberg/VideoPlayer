using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Helper.Navigation
{
    public  class DownloadSource
    {

        public void SetMediaSource(MediaItem item, MediaSource mediaSource)
        {
            Item = item;
            Source = mediaSource;
            OnSourceChanged(new MediaSourceEventArgs(mediaSource));
        }

        public MediaItem Item { get; private set; }

        public MediaSource Source { get; private set; }

        public event EventHandler<MediaSourceEventArgs> SourceChanged;

        protected void OnSourceChanged(MediaSourceEventArgs e)
        {
            SourceChanged?.Invoke(this, e);
        }

    }
}
