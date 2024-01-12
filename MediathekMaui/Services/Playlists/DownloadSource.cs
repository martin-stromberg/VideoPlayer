using CommunityToolkit.Maui.Views;
using Mediathek.Common;
using System;
using System.Linq;

namespace Mediathek.Services.Playlists
{
    public class DownloadSource
    {

        public MediaItem Item { get; private set; }

        public BaseModel TypedItem { get; private set; }

        public MediaSource Source { get; private set; }

        public string ErrorMessage { get; private set; }

        public event EventHandler<MediaSourceEventArgs> SourceChanged;

        public event EventHandler<ExceptionEventArgs> Error;

        public event EventHandler<ProgressEventArgs> ProgressChanged;

        protected void OnSourceChanged(MediaSourceEventArgs e)
        {
            SourceChanged?.Invoke(this, e);
        }

        protected void OnError(ExceptionEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        protected void OnProgressChanged(ProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        public void SetMediaSource(MediaItem item, BaseModel typedItem, MediaSource mediaSource)
        {
            Item = item;
            TypedItem = typedItem;
            Source = mediaSource;
            OnSourceChanged(new MediaSourceEventArgs(mediaSource));
            SetProgress(0);
        }

        internal void SetError(string errorMessage)
        {
            ErrorMessage = errorMessage;
            OnError(new ExceptionEventArgs(new ApplicationException(errorMessage)));
        }

        internal void SetProgress(float progress)
        {
            OnProgressChanged(new ProgressEventArgs(progress));
        }

    }
}
