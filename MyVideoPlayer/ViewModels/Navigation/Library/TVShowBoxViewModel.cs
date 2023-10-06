using MyVideoPlayer.Helper.LibraryScan;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class TVShowBoxViewModel : BaseMediaElementBoxViewModel
    {
        public TVShowBoxViewModel(LibraryScannerSettings settings) 
            : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }

        public TVShow Item
        {
            get { return GetProperty<TVShow>(); }
            set
            {
                SetProperty<TVShow>(value);
                Picture = value?.Picture;
            }
        }
    }
}
