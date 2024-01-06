using CommunityToolkit.Maui.Views;
using System;
using System.Linq;
using VideoPlayer.Common;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.Playlists
{
    public class DownloadSource
    {

        public MediaItem Item { get; private set; }

        public BaseModel TypedItem { get; private set; }

        public MediaSource Source { get; private set; }
        public string ErrorMessage { get; private set; }

        public event EventHandler<MediaSourceEventArgs> SourceChanged;
        public event EventHandler<ExceptionEventArgs> Error;

        protected void OnSourceChanged(MediaSourceEventArgs e)
        {
            SourceChanged?.Invoke(this, e);
        }
        protected void OnError(ExceptionEventArgs e)
        {
            Error?.Invoke(this, e);
        }
        public void SetMediaSource(MediaItem item, BaseModel typedItem, MediaSource mediaSource)
        {
            Item = item;
            TypedItem = typedItem;
            Source = mediaSource;
            OnSourceChanged(new MediaSourceEventArgs(mediaSource));
        }
        internal void SetError(string errorMessage)
        {
            ErrorMessage = errorMessage;
            OnError(new ExceptionEventArgs(new ApplicationException(errorMessage)));
        }
    }
}
