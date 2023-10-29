using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.Playlists
{
    public class DownloadSource
    {

        public void SetMediaSource(MediaItem item, BaseModel typedItem, MediaSource mediaSource)
        {
            Item = item;
            TypedItem = typedItem;
            Source = mediaSource;
            OnSourceChanged(new MediaSourceEventArgs(mediaSource));
        }

        public MediaItem Item { get; private set; }

        public BaseModel TypedItem { get; private set; }

        public MediaSource Source { get; private set; }

        public event EventHandler<MediaSourceEventArgs> SourceChanged;

        protected void OnSourceChanged(MediaSourceEventArgs e)
        {
            SourceChanged?.Invoke(this, e);
        }

    }
}
