using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
