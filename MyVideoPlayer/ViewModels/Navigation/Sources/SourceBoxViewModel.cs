using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    public class SourceBoxViewModel : BaseMediaElementBoxViewModel
    {
        public SourceBoxViewModel(LibraryScannerSettings settings)
            : base(settings)
        {
        }

        public MediaSource Source { get; internal set; }
    }
}
