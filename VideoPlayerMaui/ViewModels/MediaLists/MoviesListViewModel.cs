using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class MoviesListViewModel: BaseMediaListViewModel
    {

        public MoviesListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, mediaLibrary) { }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadMovies();
        }

        private void Add(BaseModel mediaItem)
        {
            if (Items.Any(item => item.Item.Id == mediaItem.Id))
                return;
            var vm = new MediaListItemViewModel(mediaItem, StatusPublisher, NavigationManager);
            Items.Add(vm);
        }

        private async void LoadMovies()
        {
            var movies = await MediaLibrary.GetMovies();
            var movieCollections = await MediaLibrary.GetMovieCollections();
            foreach (var movie in movies.Cast<BaseModel>().Concat(movieCollections).OrderBy(entry => entry.Name))
                Add(movie);
        }

    }
}
