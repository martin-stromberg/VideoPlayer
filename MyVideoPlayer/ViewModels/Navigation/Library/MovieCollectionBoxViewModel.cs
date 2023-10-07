using MyVideoPlayer.Helper.LibraryScan;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class MovieCollectionBoxViewModel : BaseMediaElementBoxViewModel
    {
        public MovieCollectionBoxViewModel(LibraryScannerSettings settings) 
            : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }

        public MovieCollection Collection
        {
            get { return GetProperty<MovieCollection>(); }
            set
            {
                SetProperty<MovieCollection>(value);
                Picture = value?.Picture;
            }
        }
    }
}
