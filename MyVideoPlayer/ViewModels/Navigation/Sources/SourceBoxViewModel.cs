using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.ComponentModel;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    public class SourceBoxViewModel: BaseMediaElementBoxViewModel
    {

        public SourceBoxViewModel(LibraryScannerSettings settings)
            : base(settings) { }

        public MediaSource Source
        {
            get
            {
                return GetProperty<MediaSource>();
            }
            set
            {
                if (value != null)
                    value.PropertyChanged -= Value_PropertyChanged;
                SetProperty<MediaSource>(value);
                if (value != null)
                    value.PropertyChanged += Value_PropertyChanged;
            }
        }

        private void Value_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Source.Name):
                    Title = Source.Name;
                    break;
            }
        }

    }
}
