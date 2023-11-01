using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.Movies;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.Playlists;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.MediaLists.MediaListItem;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class MoviesListViewModel: BaseMediaListViewModel
    {

        public MoviesListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService) { }

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
            {
                Func<IEnumerable<Movie>> getMovies = (ParentCollection != null) ? () =>
                                                                                  Items.Select(i => i.Item)
                                                                                       .OfType<Movie>() : (new Func<IEnumerable<Movie>>(() =>
                                                                                                                                        new Movie[0]));
                vm = new MovieListItemViewModel(mediaItem as Movie,
                                                getMovies,
                                                StatusPublisher,
                                                NavigationManager,
                                                Settings);
            }
            else if (mediaItem is MovieCollection)
                vm = new MovieCollectionListItemViewModel(mediaItem as MovieCollection,
                                                          StatusPublisher,
                                                          NavigationManager,
                                                          PlaylistManager,
                                                          Settings);
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
