using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

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
            if (ParentCollection != null)
                LoadMovieCollection(ParentCollection);
            else
                LoadMovies();
        }

        private void Add(BaseModel mediaItem)
        {
            if (Items.Any(item => item.Item.Id == mediaItem.Id))
                return;
            MediaListItemViewModel vm;
            if (mediaItem is Movie)
                vm = new MovieListItemViewModel(mediaItem as Movie, StatusPublisher, NavigationManager);
            else if (mediaItem is MovieCollection)
                vm = new MovieCollectionListItemViewModel(mediaItem as MovieCollection,
                                                          StatusPublisher,
                                                          NavigationManager);
            else
                return;
            Items.Add(vm);
        }

        private async void LoadMovieCollection(MovieCollection parentCollection)
        {
            var movies = await MediaLibrary.GetMovies(parentCollection.Id);
            foreach (var movie in movies.OrderBy(entry => entry.Name))
                Add(movie);
        }

        private async void LoadMovies()
        {
            var movies = await MediaLibrary.GetMovies(0);
            var movieCollections = await MediaLibrary.GetMovieCollections();
            foreach (var movie in movies.Cast<BaseModel>().Concat(movieCollections).OrderBy(entry => entry.Name))
                Add(movie);
        }

        public void SetParent(MovieCollection collection)
        {
            ParentCollection = collection;
        }

        public MovieCollection ParentCollection { get; set; }

    }
}
