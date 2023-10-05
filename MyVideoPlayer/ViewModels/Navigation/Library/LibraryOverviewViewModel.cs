using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class LibraryOverviewViewModel : NavigationContentViewModel
    {
        public LibraryOverviewViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider) 
            : base(mediaLibrary, serviceProvider)
        {
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadOverviewCategories();
        }

        private bool categoriesLoaded = false;
        private void LoadOverviewCategories()
        {
            if (categoriesLoaded)
                return;
            var vm = ServiceProvider.GetService<CategoryBoxViewModel>();
            vm.Title = "Filme";
            vm.Type = typeof(Movie);
            Items.Add(vm);

            vm = ServiceProvider.GetService<CategoryBoxViewModel>();
            vm.Title = "Serien";
            vm.Type = typeof(TVShow);
            Items.Add(vm);
            categoriesLoaded = true;
        }
    }
}
