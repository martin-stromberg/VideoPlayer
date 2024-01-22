using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Demo
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
        }

        public Task AddSourceAsync(MediaElementSource source)
        {
            return Task.Run(() => sources.Add(source));
        }

        private List<MediaElementSource> sources = new List<MediaElementSource>();
        private readonly IUserSecrets secrets;

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;

        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;

        public Task<IEnumerable<MediaElementSource>> GetSourcesAsync()
        {
            return Task.FromResult(sources.Cast<MediaElementSource>());
        }

        public Task<bool> IsEmptyAsync()
        {
            return Task.FromResult(false);
        }

        public Task ImportAsync(IMediaLibrary library)
        {
            throw new NotImplementedException();
        }

        public Task ClearMedia()
        {
            throw new NotImplementedException();
        }

        public Task<MediaElementSource> GetSourceAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaSourceAsync(MediaElementSource mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItemCollection>> GetAllMediaItemCollectionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaItemCollection(MediaItemCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId)
        {
            throw new NotImplementedException();
        }

        public Task AddMediaItemCollectionAsync(MediaItemCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItemCollection> FindMediaItemCollectionAsync(long id, string path)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItem> GetMediaItemAsync(long id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetAllMediaItems()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItem> GetOriginalMediaItemsAsync(MediaItem item)
        {
            throw new NotImplementedException();
        }

        public Task AddMediaItemAsync(MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task UpdateMediaItemAsync(MediaItem item, bool notify)
        {
            throw new NotImplementedException();
        }

        public Task<MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            throw new NotImplementedException();
        }

        public Task RemoveMediaItemAsync(MediaItem mediaItem)
        {
            throw new NotImplementedException();
        }

        public Task<BaseModel> GetTypedItem(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetUncategorizedMediaItems(int offset, int count)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<MediaItem>> GetDownloadedMediaItems(int offset, int count)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Movie>> GetMovies()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Movie>> GetMovies(long collectionId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Movie>> GetMovies(long collectionId, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public Task<Movie> FindMovieAsync(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public Task AddMovieAsync(Movie movie)
        {
            throw new NotImplementedException();
        }

        public Task<Movie> GetMovie(long id)
        {
            throw new NotImplementedException();
        }

        public Task RemoveMovieAsync(Movie movie)
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

        public Task<IEnumerable<MovieCollection>> GetMovieCollections(int offset, int count)
        {
            throw new NotImplementedException();
        }

        public Task<MovieCollection> GetMovieCollection(long id)
        {
            throw new NotImplementedException();
        }

        public Task<MovieCollection> GetMovieCollection(Movie movie)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShow>> GetTVShows()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShow>> GetTVShows(int offset, int value)
        {
            throw new NotImplementedException();
        }

        public Task<TVShow> GetTVShow(long id)
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

        public Task AddTVShowAsync(TVShow show)
        {
            throw new NotImplementedException();
        }

        public Task RemoveTVShowAsync(TVShow show)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId)
        {
            throw new NotImplementedException();
        }

        public Task<TVShowSeason> GetTVShowSeason(long id)
        {
            throw new NotImplementedException();
        }

        public Task AddTVShowSeasonAsync(TVShow show, TVShowSeason season)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            throw new NotImplementedException();
        }

        public Task<TVShowEpisode> GetTVShowEpisode(long id)
        {
            throw new NotImplementedException();
        }

        public Task<TVShowEpisode> FindTVShowEpisodeByMediaItem(long mediaItemId)
        {
            throw new NotImplementedException();
        }

        public Task AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Playlist>> GetPlaylists()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Playlist>> GetPlaylists(PlaylistType type)
        {
            throw new NotImplementedException();
        }

        public Task<Playlist> GetPlaylist(long id)
        {
            throw new NotImplementedException();
        }

        public Task AddPlaylistAsync(Playlist playlist)
        {
            throw new NotImplementedException();
        }

        public Task AddPlaybackHistory(History history)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<HistoryEntry>> GetPlayBackHistoryEntries()
        {
            throw new NotImplementedException();
        }

        public Task RemoveMovieCollectionAsync(MovieCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task RemoveTVShowSeasonAsync(TVShowSeason season)
        {
            throw new NotImplementedException();
        }

        public Task RemoveTVShowEpisodeAsync(TVShowEpisode episode)
        {
            throw new NotImplementedException();
        }

        public void AddTVShowCollectionAsync(BaseModel item)
        {
            throw new NotImplementedException();
        }

        public Task AddTVShowCollectionAsync(TVShowCollection item)
        {
            throw new NotImplementedException();
        }

        public object GetTVShowCollections()
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<TVShowCollection>> IMediaLibrary.GetTVShowCollections()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShowCollection>> FindTVShowCollectionByNameAsync(string name)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<TVShow>> GetTVShows(long id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<object>> GetTVShowNames()
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<TVShowName>> IMediaLibrary.GetTVShowNames()
        {
            throw new NotImplementedException();
        }

        public Task RemoveTVShowCollectionAsync(TVShowCollection collection)
        {
            throw new NotImplementedException();
        }

        public Task GetTVShowCollection(long collectionId)
        {
            throw new NotImplementedException();
        }

        Task<TVShowCollection> IMediaLibrary.GetTVShowCollection(long Id)
        {
            throw new NotImplementedException();
        }
        public Task RemovePlaylistAsync(Playlist playlist)
        {
            throw new NotImplementedException();
        }

    }
}
