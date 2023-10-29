using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Playlists;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.MediaLibrary
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
        Task<IEnumerable<MediaSource>> GetSourcesAsync();

        Task<MediaSource> GetSourceAsync(long id);

        Task AddSourceAsync(MediaSource source);

        Task RemoveMediaSourceAsync(MediaSource mediaItem);
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
        #endregion

        #region Movies
        Task<IEnumerable<Movie>> GetMovies();

        Task<IEnumerable<Movie>> GetMovies(long collectionId);

        Task<Movie> FindMovieAsync(long mediaItemId);

        Task AddMovieAsync(Movie movie);

        Task<Movie> GetMovie(long id);

        Task RemoveMovieAsync(Movie movie);
        #endregion

        #region Movie Collection
        Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name);

        Task AddMovieCollectionAsync(MovieCollection collection);

        Task<IEnumerable<MovieCollection>> GetMovieCollections();

        Task<MovieCollection> GetMovieCollection(long id);

        Task<MovieCollection> GetMovieCollection(Movie movie);
        #endregion

        #region TV Show 
        Task<IEnumerable<TVShow>> GetTVShows();

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

        #region TV Show Episode
        Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId);

        Task<TVShowEpisode> GetTVShowEpisode(long id);

        Task AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode);
        #endregion

        #region Playlist 
        Task<IEnumerable<Playlist>> GetPlaylists();

        Task<IEnumerable<Playlist>> GetPlaylists(PlaylistType type);

        Task<Playlist> GetPlaylist(long id);

        Task AddPlaylistAsync(Playlist playlist);
        #endregion

    }
}
