using MyVideoPlayer.Helper.LibraryScan;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class TVShowSeasonBoxViewModel : BaseMediaElementBoxViewModel
    {
        public TVShowSeasonBoxViewModel(LibraryScannerSettings settings)
            : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }

        public TVShowSeason Item
        {
            get { return GetProperty<TVShowSeason>(); }
            set
            {
                SetProperty<TVShowSeason>(value);
                Picture = value?.Picture;
            }
        }
    }
}
