using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Playlists;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Mediathek.ViewModels.MediaLists.MediaListItem;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists
{
    public class MoviesListViewModel: BaseMediaListViewModel
    {

        public MoviesListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary,
            IPlaylistManager playlistManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager)
            : base(statusPublisher, navigationManager, mediaLibrary, playlistManager, settingsService, downloadManager) { }

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
            BaseMediaListItemViewModel vm;
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
                                                Settings,
                                                DownloadManager,
                                                MediaLibrary);
            }
            else if (mediaItem is MovieCollection)
            {
                vm = new MovieCollectionListItemViewModel(mediaItem as MovieCollection,
                                                          StatusPublisher,
                                                          NavigationManager,
                                                          PlaylistManager,
                                                          Settings,
                                                          DownloadManager,
                                                          MediaLibrary);
            }
            else
                return;

            int offset = 0;
            var addBefore = Items.SkipWhile(i =>
            {
                offset += 1;
                return i.Item.Name.CompareTo(mediaItem.Name) < 0;
            })
                                 .FirstOrDefault();
            if (addBefore is null)
                Items.Add(vm);
            else
                Items.Insert(offset - 1, vm);
        }

        private async void LoadMovieCollection(MovieCollection parentCollection)
        {
            var movies = await MediaLibrary.GetMovies(parentCollection.Id);
            foreach (var movie in movies
                .OrderBy(entry => entry.Date)
                .ThenBy(entry => entry.Name))
                Add(movie);
        }

        private int recordsToLoad = 10;

        private async void LoadMovies(int offset = 0, int totalOffset = 0)
        {
            recordsToLoad = (offset == 0) ? 10 : 2;
            var movieCollections = (totalOffset != 0) ? (new MovieCollection[0]) : (await MediaLibrary.GetMovieCollections(offset, recordsToLoad));
            foreach (var movieCollection in movieCollections.Where(coll => !coll.IsSingleMovie))
                Add(movieCollection);

            var movies = (totalOffset == 0) ? (new Movie[0]) : (await MediaLibrary.GetMovies(0,
                                                                                             offset - totalOffset,
                                                                                             recordsToLoad));
            foreach (var movie in movies)
                Add(movie);

            var found = movieCollections.Count() + movies.Count();
            offset += found;
            if (found == recordsToLoad)
                LoadMovies(offset, totalOffset);
            else if (totalOffset == 0)
            {
                totalOffset = offset;
                LoadMovies(offset, totalOffset);
            }
        }

        public void SetParent(MovieCollection collection)
        {
            ParentCollection = collection;
        }

        public MovieCollection ParentCollection { get; set; }

    }
}
