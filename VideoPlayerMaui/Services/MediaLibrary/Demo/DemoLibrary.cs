using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.MediaLibrary.Demo
{
    public class DemoLibrary: IMediaLibrary
    {

        public DemoLibrary(IUserSecrets secrets)
            : base()
        {
            this.secrets = secrets;
        }

        public void Fill()
        {
            AddSourceAsync(new FtpMediaSource()
                {
                    Name = "Filme",
                    Password = secrets.RespberryPiPassword,
                    Username = "mstro",
                    ServerName = "raspberrypi",
                    Path = "/mstro/Crucial X62/Filme",
                })
            .Wait();
            AddSourceAsync(new FtpMediaSource()
                {
                    Name = "Serien",
                    Password = secrets.RespberryPiPassword,
                    Username = "mstro",
                    ServerName = "raspberrypi",
                    Path = "/mstro/Crucial X62/Serien",
                })
            .Wait();
            AddSourceAsync(new FtpMediaSource()
                {
                    Name = "Serien 2",
                    Password = secrets.RespberryPiPassword,
                    Username = "mstro",
                    ServerName = "raspberrypi",
                    Path = "/mstro/Disk2/Serien",
                })
            .Wait();

            // AddSourceAsync(new FtpMediaSource()
            // {
            // Name = "Musik",
            // Password = secrets.RespberryPiPassword,
            // Username = "mstro",
            // ServerName = "raspberrypi",
            // Path = "/mstro/Crucial X62/Musik",
            // })
            // .Wait();
        }

        public Task AddSourceAsync(MediaSource source)
        {
            return Task.Run(() => sources.Add(source));
        }

        private List<MediaSource> sources = new List<MediaSource>();
        private readonly IUserSecrets secrets;

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;

        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;

        public Task<IEnumerable<MediaSource>> GetSourcesAsync()
        {
            return Task.FromResult(sources.Cast<MediaSource>());
        }

        public Task<bool> IsEmptyAsync()
        {
            return Task.FromResult(false);
        }

        public Task ImportAsync(IMediaLibrary library)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            throw new NotImplementedException();
        }

        public Task<MediaSource> GetSourceAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task AddMediaItemCollectionAsync(MediaItemCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task AddMediaItemAsync(MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItemCollection> FindMediaItemCollectionAsync(long id, string path)
        {
            throw new NotImplementedException();
        }

        public Task ClearMedia()
        {
            throw new NotImplementedException();
        }

        public Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItem> GetMediaItemAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public Task<Movie> FindMovieAsync(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public void AddMovieAsync(Movie movie)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShow>> FindTVShowByNameAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<TVShow> FindTVShowAsync(long id)
        {
            throw new NotImplementedException();
        }

        public void AddTVShowAsync(TVShow show)
        {
            throw new NotImplementedException();
        }

        public void AddTVShowSeasonAsync(TVShow show, TVShowSeason season)
        {
            throw new NotImplementedException();
        }

        public void AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            throw new NotImplementedException();
        }

        Task IMediaLibrary.AddMovieAsync(Movie movie)
        {
            throw new NotImplementedException();
        }

        Task IMediaLibrary.AddTVShowAsync(TVShow show)
        {
            throw new NotImplementedException();
        }

        Task IMediaLibrary.AddTVShowSeasonAsync(TVShow show, TVShowSeason season)
        {
            throw new NotImplementedException();
        }

        Task IMediaLibrary.AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Movie>> GetMovies()
        {
            throw new NotImplementedException();
        }

        public Task<Movie> GetMovie(long id)
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaItemAsync(MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShow>> GetTVShows()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            throw new NotImplementedException();
        }

        public Task<TVShow> GetTVShow(long id)
        {
            throw new NotImplementedException();
        }

        public Task<TVShowSeason> GetTVShowSeason(long id)
        {
            throw new NotImplementedException();
        }

        public Task<TVShowEpisode> GetTVShowEpisode(long id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task AddMovieCollectionAsync(MovieCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MovieCollection>> GetMovieCollections()
        {
            throw new NotImplementedException();
        }

        public Task<MovieCollection> GetMovieCollection(long id)
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaSourceAsync(MediaSource mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task GetAllMediaItems()
        {
            throw new NotImplementedException();
        }

        public Task GetAllMediaItemCollectionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaItemCollection(MediaItemCollection collection)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<MediaItemCollection>> IMediaLibrary.GetAllMediaItemCollectionsAsync()
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<MediaItem>> IMediaLibrary.GetAllMediaItems()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Movie>> GetMovies(long collectionId)
        {
            throw new NotImplementedException();
        }
        public Task RemoveMovieAsync(Movie movie)
        {
            throw new NotImplementedException();
        }
        public Task RemoveTVShowAsync(TVShow show)
        {
            throw new NotImplementedException();
        }

    }
}
