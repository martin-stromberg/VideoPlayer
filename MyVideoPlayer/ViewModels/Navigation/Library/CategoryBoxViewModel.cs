using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    internal class CategoryBoxViewModel : BaseMediaElementBoxViewModel
    {
        public CategoryBoxViewModel(LibraryScannerSettings settings) : base(settings)
        {
        }

        public Type Type { get; set; }
    }
}
