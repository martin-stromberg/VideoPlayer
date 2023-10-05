using MyVideoPlayer.ViewModels.Navigation.MediaCollection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Library
{
    public class LibraryMediaCollectionViewModel : MediaCollectionViewModel
    {
        private readonly IMediaLibrary mediaLibrary;

        public LibraryMediaCollectionViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider) 
            : base(mediaLibrary, serviceProvider)
        {
            this.mediaLibrary = mediaLibrary;
        }

        public Type CategoryType { get; set; }
        public BaseModel Parent { get; set; }

        protected override void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            
        }
        protected override void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            
        }
        protected override void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {
            
        }

        internal override Task ReadMediaCollection(MediaSource source)
        {
            return Task.CompletedTask;
        }
        internal override async Task ReadMediaItems(MediaItemCollection collection)
        {
            if (Parent == null)
            {
                if (CategoryType == typeof(Movie))
                    await LoadMoviesAsync();
            }
        }

        public override async void OnAppeared()
        {
            await ReadMediaItems(null);
        }

        private async Task LoadMoviesAsync()
        {
            var movies = await mediaLibrary.GetMovies();
            foreach (var movie in movies.OrderBy(m => m.Name))
                AddMovie(movie);
        }

        private void AddMovie(Movie movie)
        {
            MovieBoxViewModel vm = ServiceProvider.GetService<MovieBoxViewModel>();
            vm.Title = movie.Name;
            vm.Item = movie;
            vm.Picture = movie.Picture;
            Items.Add(vm);
        }
    }
}
