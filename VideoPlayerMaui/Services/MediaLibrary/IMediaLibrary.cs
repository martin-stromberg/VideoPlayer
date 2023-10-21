using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.MediaLibrary
{
    public interface IMediaLibrary
    {

        Task<bool> IsEmptyAsync();

        Task<IEnumerable<MediaSource>> GetSourcesAsync();

        Task<MediaSource> GetSourceAsync(long id);

        Task AddSourceAsync(MediaSource source);

        Task RemoveMediaSourceAsync(MediaSource mediaItem);

        Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id);

        Task<IEnumerable<MediaItemCollection>> GetAllMediaItemCollectionsAsync();

        Task RemoveMediaItemCollection(MediaItemCollection collection);

        Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId);

        Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId);

        Task AddMediaItemCollectionAsync(MediaItemCollection collection);

        Task<MediaItemCollection> FindMediaItemCollectionAsync(long id, string path);

        Task<MediaItem> GetMediaItemAsync(long id);

        Task<IEnumerable<MediaItem>> GetAllMediaItems();

        Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId);

        Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId);

        Task AddMediaItemAsync(MediaItem mediaItem);

        Task<MediaItem> FindMediaItemAsync(long SourceId, string path);

        Task ImportAsync(IMediaLibrary library);

        Task ClearMedia();

        Task<IEnumerable<Movie>> GetMovies();

        Task<Movie> FindMovieAsync(long mediaItemId);

        Task AddMovieAsync(Movie movie);

        Task<IEnumerable<TVShow>> FindTVShowByNameAsync(string name);

        Task<TVShow> FindTVShowAsync(long id);

        Task AddTVShowAsync(TVShow show);

        Task AddTVShowSeasonAsync(TVShow show, TVShowSeason season);

        Task AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode);

        Task<Movie> GetMovie(long id);

        Task RemoveMediaItemAsync(MediaItem mediaItem);

        Task<IEnumerable<TVShow>> GetTVShows();

        Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId);

        Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId);

        Task<TVShow> GetTVShow(long id);

        Task<TVShowSeason> GetTVShowSeason(long id);

        Task<TVShowEpisode> GetTVShowEpisode(long id);

        Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name);

        Task AddMovieCollectionAsync(MovieCollection collection);

        Task<IEnumerable<MovieCollection>> GetMovieCollections();

        Task<MovieCollection> GetMovieCollection(long id);

        event EventHandler<BaseModelEventArgs> ModelElementAdded;

        event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        event EventHandler<BaseModelEventArgs> ModelElementRemoved;

    }
}
