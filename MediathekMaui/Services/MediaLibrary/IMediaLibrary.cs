using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary
{
    public interface IMediaLibrary
    {

        #region General
        Task<bool> IsEmptyAsync();

        Task ImportAsync(IMediaLibrary library);

        Task ClearMedia();

        event EventHandler<BaseModelEventArgs> ModelElementAdded;

        event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        event EventHandler<BaseModelEventArgs> ModelElementRemoved;
        #endregion

        #region Media Sources
        Task<IEnumerable<MediaElementSource>> GetSourcesAsync();

        Task<MediaElementSource> GetSourceAsync(long id);

        Task AddSourceAsync(MediaElementSource source);

        Task RemoveMediaSourceAsync(MediaElementSource mediaItem);
        #endregion

        #region Media Item Collection 
        Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id);

        Task<IEnumerable<MediaItemCollection>> GetAllMediaItemCollectionsAsync();

        Task RemoveMediaItemCollection(MediaItemCollection collection);

        Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId);

        Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId);

        Task AddMediaItemCollectionAsync(MediaItemCollection collection);

        Task<MediaItemCollection> FindMediaItemCollectionAsync(long id, string path);
        #endregion

        #region Media Item        
        Task<MediaItem> GetMediaItemAsync(long id);

        Task<IEnumerable<MediaItem>> GetAllMediaItems();

        Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId);

        Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId);

        Task<MediaItem> GetOriginalMediaItemsAsync(MediaItem item);

        Task AddMediaItemAsync(MediaItem mediaItem);

        Task UpdateMediaItemAsync(MediaItem item, bool notify);

        Task<MediaItem> FindMediaItemAsync(long SourceId, string path);

        Task RemoveMediaItemAsync(MediaItem mediaItem);

        Task<BaseModel> GetTypedItem(long mediaItemId);

        Task<IEnumerable<MediaItem>> GetUncategorizedMediaItems(int offset, int count);

        Task<IEnumerable<MediaItem>> GetDownloadedMediaItems(int offset, int count);
        #endregion

        #region Movies
        Task<IEnumerable<Movie>> GetMovies();

        Task<IEnumerable<Movie>> GetMovies(long collectionId);

        Task<IEnumerable<Movie>> GetMovies(long collectionId, int offset, int count);

        Task<Movie> FindMovieAsync(long mediaItemId);

        Task AddMovieAsync(Movie movie);

        Task<Movie> GetMovie(long id);

        Task RemoveMovieAsync(Movie movie);
        #endregion

        #region Movie Collection
        Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name);

        Task AddMovieCollectionAsync(MovieCollection collection);

        Task<IEnumerable<MovieCollection>> GetMovieCollections();

        Task<IEnumerable<MovieCollection>> GetMovieCollections(int offset, int count);

        Task<MovieCollection> GetMovieCollection(long id);

        Task<MovieCollection> GetMovieCollection(Movie movie);
        #endregion

        #region TV Show 
        Task<IEnumerable<TVShowName>> GetTVShowNames();

        Task<IEnumerable<TVShow>> GetTVShows();

        Task<IEnumerable<TVShow>> GetTVShows(int offset, int value);

        Task<IEnumerable<TVShow>> GetTVShows(long collectionId);

        Task<TVShow> GetTVShow(long id);

        Task<IEnumerable<TVShow>> FindTVShowByNameAsync(string name);

        Task<TVShow> FindTVShowAsync(long id);

        Task AddTVShowAsync(TVShow show);

        Task RemoveTVShowAsync(TVShow show);
        #endregion

        #region TV Show Season
        Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId);

        Task<TVShowSeason> GetTVShowSeason(long id);

        Task AddTVShowSeasonAsync(TVShow show, TVShowSeason season);
        #endregion

        #region TV Show Collection
        Task<IEnumerable<TVShowCollection>> GetTVShowCollections();

        Task<TVShowCollection> GetTVShowCollection(long Id);

        Task<IEnumerable<TVShowCollection>> FindTVShowCollectionByNameAsync(string name);

        Task AddTVShowCollectionAsync(TVShowCollection item);
        #endregion

        #region TV Show Episode
        Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId);

        Task<TVShowEpisode> GetTVShowEpisode(long id);

        Task<TVShowEpisode> FindTVShowEpisodeByMediaItem(long mediaItemId);

        Task AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode);
        #endregion

        #region Playlist 
        Task<IEnumerable<Playlist>> GetPlaylists();

        Task<IEnumerable<Playlist>> GetPlaylists(PlaylistType type);

        Task<Playlist> GetPlaylist(long id);

        Task AddPlaylistAsync(Playlist playlist);
        #endregion

        Task AddPlaybackHistory(History history);

        Task<IEnumerable<HistoryEntry>> GetPlayBackHistoryEntries();

        Task RemoveMovieCollectionAsync(MovieCollection collection);

        Task RemoveTVShowCollectionAsync(TVShowCollection collection);

        Task RemoveTVShowSeasonAsync(TVShowSeason season);

        Task RemoveTVShowEpisodeAsync(TVShowEpisode episode);

        Task RemovePlaylistAsync(Playlist playlist);

    }
}
