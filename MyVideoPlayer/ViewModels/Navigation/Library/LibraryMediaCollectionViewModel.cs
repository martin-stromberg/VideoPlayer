using MyVideoPlayer.ViewModels.Navigation.MediaCollection;
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
                {
                    await LoadMovieCollections();
                    await LoadMoviesAsync();
                }
                else if (CategoryType == typeof(TVShow))
                    await LoadTVShows();
            }
            else
            {
                if (CategoryType == typeof(TVShow))
                {
                    await LoadTVShowsSeasons(Parent as TVShow);
                    
                }
                else if (CategoryType == typeof(TVShowSeason))
                    await LoadTVShowsEpisodes(Parent as TVShowSeason);
                else if (CategoryType == typeof(MovieCollection))
                    await LoadMoviesAsync();
            }
        }

        

        private async Task LoadTVShowsSeasons(TVShow tVShow)
        {
            if (tVShow == null) return;
            var seasons = (await mediaLibrary.GetTVShowSeasons(tVShow.Id)).ToArray();
            foreach (var season in seasons.OrderBy(m => m.Name))
                AddTVShowSeason(season);
        }

        private async Task LoadTVShowsEpisodes(TVShowSeason tVShowSeason)
        {
            if (tVShowSeason == null) return;
            var episodes = (await mediaLibrary.GetTVShowEpisodes(tVShowSeason.Id)).ToArray();
            foreach (var episode in episodes.OrderBy(m => m.EpisodeNo).ThenBy(m => m.Name))
                AddTVShowEpisode(episode);
        }
        private async Task LoadTVShows()
        {
            var shows = (await mediaLibrary.GetTVShows()).ToArray();
            foreach (var show in shows.OrderBy(m => m.Name))
                AddTVShow(show);
        }
        private void AddTVShowEpisode(TVShowEpisode episode)
        {
            if (Items.Cast<TVShowEpisodeBoxViewModel>().Any(vm => vm.Item.Id == episode.Id))
                return;
            TVShowEpisodeBoxViewModel vm = ServiceProvider.GetService<TVShowEpisodeBoxViewModel>();
            vm.Title = episode.Name;
            vm.Item = episode;
            Items.Add(vm);
        }
        private void AddTVShowSeason(TVShowSeason season)
        {
            if (Items.Cast<TVShowSeasonBoxViewModel>().Any(vm => vm.Item.Id == season.Id)) 
                return;
            TVShowSeasonBoxViewModel vm = ServiceProvider.GetService<TVShowSeasonBoxViewModel>();
            vm.Title = season.Name;
            vm.Item = season;
            Items.Add(vm);
        }

        public override void OnAppeared()
        {
            _ = ReadMediaItems(null);
        }
        private async Task LoadMovieCollections()
        {            
            var collections = await mediaLibrary.GetMovieCollections();
            foreach (var collection in collections.OrderBy(c => c.Name))
                AddMovieCollection(collection);
        }

        private void AddMovieCollection(MovieCollection collection)
        {
            if (Items.OfType<MovieCollectionBoxViewModel>().Any(vm => vm.Collection.Id == collection.Id))
                return;
            MovieCollectionBoxViewModel vm = ServiceProvider.GetService<MovieCollectionBoxViewModel>();
            vm.Title = collection.Name;
            vm.Collection = collection;
            Items.Add(vm);
        }

        private async Task LoadMoviesAsync()
        {
            var collection = Parent as MovieCollection;
            var movies = await mediaLibrary.GetMovies();
            foreach (var movie in movies
                .Where(m => (collection == null && m.CollectionId == 0) || (collection != null && m.CollectionId == collection.Id))
                .OrderBy(m => m.Name))
                AddMovie(movie);
        }

        private void AddMovie(Movie movie)
        {
            if (Items.OfType<MovieBoxViewModel>().Any(vm => vm.Item.Id == movie.Id))
                return;
            MovieBoxViewModel vm = ServiceProvider.GetService<MovieBoxViewModel>();
            vm.Title = movie.Name;
            vm.Item = movie;
            Items.Add(vm);
        }

        private void AddTVShow(TVShow show)
        {
            if (Items.Cast<TVShowBoxViewModel>().Any(vm => vm.Item.Id == show.Id))
                return;
            TVShowBoxViewModel vm = ServiceProvider.GetService<TVShowBoxViewModel>();
            vm.Title = show.Name;
            vm.Item = show;
            Items.Add(vm);
        }
    }
}
