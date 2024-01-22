using Mediathek.Services.Database.Models;
using SQLite;

namespace Mediathek.Services.Database
{
    public interface IMediaLibraryDatabase
    {

        Task RemoveSource(MediaSource source);

        Task<MediaSource> AddOrUpdateSourceAsync(MediaSource mediaSource);

        Task<AsyncTableQuery<MediaSource>> GetSourcesAsync();

        Task<MediaSource> GetSourceAsync(long id);

        Task<MediaCollection> AddOrUpdateMediaCollectionAsync(MediaCollection collection);

        Task<AsyncTableQuery<MediaCollection>> GetMediaCollectionsAsync();

        Task<MediaCollection> GetMediaCollectionAsync(long id);

        Task<Services.Database.Models.MediaItem> AddOrUpdateMediaItemAsync(Services.Database.Models.MediaItem mediaItem);

        Task<AsyncTableQuery<Services.Database.Models.MediaItem>> GetMediaItemsAsync();

        Task<Services.Database.Models.MediaItem> GetMediaItemAsync(long id);

        Task RemoveMediaCollection(MediaCollection collection);

        Task RemoveMediaItem(Services.Database.Models.MediaItem mediaItem);

        Task<Services.Database.Models.Movie> GetMovieByMediaItem(long mediaItemId);

        Task<IEnumerable<MovieMediaItem>> GetMovieMediaItems(long movieId);

        Task RemoveMovieMediaItemsAsync(long movieId);

        Task<Services.Database.Models.Movie> AddOrUpdateMovie(Services.Database.Models.Movie dataModel);

        Task<MovieMediaItem> AddMovieMediaItem(MovieMediaItem mediaItem);

        Task<IEnumerable<Services.Database.Models.TVShow>> GetTVShowsByName(string name);

        Task<IEnumerable<Services.Database.Models.TVShowCollection>> GetTVShowCollections();

        Task<IEnumerable<Services.Database.Models.TVShowCollection>> GetTVShowCollectionsByName(string name);

        Task<Services.Database.Models.TVShow> GetTVShow(long id);

        Task<Services.Database.Models.TVShow> AddOrUpdateTVShow(Services.Database.Models.TVShow show);

        Task<Services.Database.Models.TVShowCollection> AddOrUpdateTVShowCollection(Models.TVShowCollection collection);

        Task<Services.Database.Models.TVShowCollection> GetTVShowCollection(long id);

        Task RemoveTVShowCollection(long id);

        Task<Services.Database.Models.TVShowSeason> AddOrUpdateTVShowSeason(Services.Database.Models.TVShowSeason season);

        Task<Services.Database.Models.TVShowEpisode> AddOrUpdateTVShowEpisode(Services.Database.Models.TVShowEpisode episode);

        Task<IEnumerable<Services.Database.Models.TVShowSeason>> GetTVShowSeasons(long showId);

        Task<IEnumerable<Services.Database.Models.TVShowEpisode>> GetTVShowEpisodes(long seasonId);

        Task RemoveTVShowEpisodeMediaItemsAsync(long episodeId);

        Task<TVShowEpisodeMediaItem> AddTVShowEpisodeMediaItem(TVShowEpisodeMediaItem mediaItem);

        Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowEpisodeMediaItems(long episodeId);

        Task<IEnumerable<Services.Database.Models.Movie>> GetMovies();

        Task<Services.Database.Models.Movie> GetMovie(long id);

        Task<IEnumerable<Services.Database.Models.TVShow>> GetTVShows();

        Task<Services.Database.Models.TVShowSeason> GetTVShowSeason(long id);

        Task<Services.Database.Models.TVShowEpisode> GetTVShowEpisode(long id);

        Task<Services.Database.Models.TVShowEpisode> FindTVShowEpisodeByMediaItem(long originalMediaItemId);

        Task RemoveMovie(long movieId);

        Task RemoveTVShow(long id);

        Task<IEnumerable<Services.Database.Models.MovieCollection>> GetMovieCollectionsByName(string name);

        Task<Services.Database.Models.MovieCollection> AddOrUpdateMovieCollection(Services.Database.Models.MovieCollection collection);

        Task<IEnumerable<Services.Database.Models.MovieCollection>> GetMovieCollections();

        Task<Services.Database.Models.MovieCollection> GetMovieCollection(long id);

        Task<IEnumerable<MovieMediaItem>> GetMovieMediaItemsForMediaItem(long mediaItemId);

        Task RemoveMovieMediaItemAsync(MovieMediaItem movieMediaItem);

        Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowMediaItemsForMediaItem(long mediaItemId);

        Task RemoveMovieMediaItemAsync(TVShowEpisodeMediaItem movieMediaItem);

        Task<IEnumerable<Services.Database.Models.Playlist>> GetPlaylists();

        Task<Services.Database.Models.Playlist> GetPlaylist(long id);

        Task<IEnumerable<Services.Database.Models.PlaylistEntry>> GetPlaylistEntries(long id);

        Task<Services.Database.Models.Playlist> AddOrUpdatePlaylistAsync(Services.Database.Models.Playlist playlist);

        Task<Services.Database.Models.PlaylistEntry> AddOrUpdatePlaylistEntryAsync(Services.Database.Models.PlaylistEntry playlistEntry);

        Task RemovePlaylistEntryAsync(Services.Database.Models.PlaylistEntry mediaItemToDelete);

        Task<IEnumerable<PlaybackHistoryEntry>> GetPlaybackHistoryEntriesAsync();

        Task<PlaybackHistoryEntry> AddOrUpdatePlaybackHistoryEntry(PlaybackHistoryEntry entry);

        Task RemoveMovieCollection(long id);

        Task RemoveTVShowSeason(long id);

        Task RemoveTVShowEpisode(long id);

        Task RemovePlaylist(long id);

    }
}
