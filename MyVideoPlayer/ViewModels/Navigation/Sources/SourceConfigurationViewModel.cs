using System;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    internal class SourceConfigurationViewModel: NavigationContentViewModel
    {

        private MediaSource newSource;

        public SourceConfigurationViewModel(
            MediaSource newSource,
            IMediaLibrary mediaLibrary,
            IServiceProvider serviceProvider)
            : base(mediaLibrary, serviceProvider)
        {
            this.newSource = newSource;
        }

    }
}
