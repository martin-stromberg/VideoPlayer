using SQLite;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.Database
{
    public interface IMediaLibraryDatabase
    {
        Task<Models.MediaSource> AddOrUpdateSourceAsync(Models.MediaSource mediaSource);
        Task<AsyncTableQuery<Models.MediaSource>> GetSourcesAsync();
        Task<Models.MediaSource> GetSourceAsync(long id);

        Task<Models.MediaCollection> AddOrUpdateMediaCollectionAsync(Models.MediaCollection collection);
        Task<AsyncTableQuery<Models.MediaCollection>> GetMediaCollectionsAsync();
        Task<Models.MediaCollection> GetMediaCollectionAsync(long id);

        Task<Models.MediaItem> AddOrUpdateMediaItemAsync(Models.MediaItem mediaItem);
        Task<AsyncTableQuery<Models.MediaItem>> GetMediaItemsAsync();
        Task<Models.MediaItem> GetMediaItemAsync(long id);
        Task RemoveMediaCollection(MediaCollection collection);
        Task RemoveMediaItem(MediaItem mediaItem);
        Task AddLog(LogEntry entry);
        Task<IEnumerable<Models.LogEntry>> GetLogs();
        Task RemoveLog(LogEntry log);

        Task<Models.Movie> GetMovieByMediaItem(long mediaItemId);
        Task<IEnumerable<Models.MovieMediaItem>> GetMovieMediaItems(long movieId);
        Task RemoveMovieMediaItemsAsync(long movieId);
        Task<Movie> AddOrUpdateMovie(Movie dataModel);
        Task<MovieMediaItem> AddMovieMediaItem(MovieMediaItem mediaItem);

        Task<IEnumerable<Models.TVShow>> GetTVShowsByName(string name);
        Task<Models.TVShow> GetTVShow(long id);
        Task<TVShow> AddOrUpdateTVShow(TVShow show);
        Task<TVShowSeason> AddOrUpdateTVShowSeason(TVShowSeason season);
        Task<TVShowEpisode> AddOrUpdateTVShowEpisode(TVShowEpisode episode);
        Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId);
        Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId);
        Task RemoveTVShowEpisodeMediaItemsAsync(long episodeId);
        Task<TVShowEpisodeMediaItem> AddTVShowEpisodeMediaItem(TVShowEpisodeMediaItem mediaItem);
        Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowEpisodeMediaItems(long episodeId);
        Task<IEnumerable<Models.Movie>> GetMovies();
        Task<Movie> GetMovie(long id);
        Task<IEnumerable<TVShow>> GetTVShows();
        Task<TVShowSeason> GetTVShowSeason(long id);
        Task<TVShowEpisode> GetTVShowEpisode(long id);
        Task RemoveMovie(long movieId);
        Task RemoveTVShow(long id);
    }
}
