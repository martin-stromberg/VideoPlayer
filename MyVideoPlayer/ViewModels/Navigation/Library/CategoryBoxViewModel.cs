using MyVideoPlayer.Helper.LibraryScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
