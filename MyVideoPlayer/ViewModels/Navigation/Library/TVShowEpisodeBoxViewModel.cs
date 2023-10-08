using MyVideoPlayer.Helper.LibraryScan;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class TVShowEpisodeBoxViewModel : BaseMediaElementBoxViewModel
    {
        public TVShowEpisodeBoxViewModel(LibraryScannerSettings settings)
           : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }

        public TVShowEpisode Item
        {
            get { return GetProperty<TVShowEpisode>(); }
            set
            {
                SetProperty<TVShowEpisode>(value);
                //Picture = value?.Picture;
            }
        }

        public MediaItem MediaItem { get; internal set; }
        public MediaItemCollection Collection { get; internal set; }
        public MediaSource Source { get; internal set; }
    }
}
