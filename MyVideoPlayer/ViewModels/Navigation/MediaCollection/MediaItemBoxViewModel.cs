using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.MediaCollection
{
    internal class MediaItemBoxViewModel : BaseMediaElementBoxViewModel
    {
        public MediaItemBoxViewModel(LibraryScannerSettings settings)
            : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }
        public MediaItem Item
        {
            get { return GetProperty<MediaItem>(); }
            set
            {
                SetProperty<MediaItem>(value);
                Picture = value?.Picture;
            }
        }
        public MediaSource Source
        {
            get { return GetProperty<MediaSource>(); }
            set { SetProperty<MediaSource>(value); }
        }
        public MediaItemCollection Collection
        {
            get { return GetProperty<MediaItemCollection>(); }
            set { SetProperty<MediaItemCollection>(value); }
        }
    }
}
