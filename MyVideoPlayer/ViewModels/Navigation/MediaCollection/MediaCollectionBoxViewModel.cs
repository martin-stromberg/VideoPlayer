using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.MediaCollection
{
    public class MediaCollectionBoxViewModel : BaseMediaElementBoxViewModel
    {
        public MediaCollectionBoxViewModel(LibraryScannerSettings settings)
            : base(settings)
        {
        }
        public MediaSource Source
        {
            get { return GetProperty<MediaSource>(); }
            set
            {
                SetProperty<MediaSource>(value);
                IsDownloadable = (Collection != null) && (ParentCollection != null) && (Source != null);
            }
        }
        public MediaItemCollection ParentCollection
        {
            get { return GetProperty<MediaItemCollection>(); }
            set
            {
                SetProperty<MediaItemCollection>(value);
                IsDownloadable = (Collection != null) && (ParentCollection != null) && (Source != null);
            }
        }
        public MediaItemCollection Collection
        {
            get { return GetProperty<MediaItemCollection>(); }
            set
            {
                SetProperty<MediaItemCollection>(value);
                Picture = value?.Picture;
                IsDownloadable = (Collection != null) && (ParentCollection != null) && (Source != null);
            }
        }
        public override bool IsPlayable
        {
            get { return false; }
            set { base.IsPlayable = value; }
        }
    }
}
