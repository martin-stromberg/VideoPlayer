using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class MovieBoxViewModel: BaseMediaElementBoxViewModel
    {
        public MovieBoxViewModel(LibraryScannerSettings settings) : base(settings)
        {
            IsPlayable = true;
            IsDownloadable = true;
        }

        public Movie Item
        {
            get { return GetProperty<Movie>(); }
            set
            {
                SetProperty<Movie>(value);
                Picture = null;// value?.Picture;
            }
        }

        public MediaItem MediaItem { get; set; }
        public MediaItemCollection Collection { get; set; }
        public MediaSource Source { get; set; }
    }
}
