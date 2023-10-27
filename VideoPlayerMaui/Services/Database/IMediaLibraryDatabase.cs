using SQLite;
using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Services.Database
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

        Task<MediaItem> AddOrUpdateMediaItemAsync(MediaItem mediaItem);

        Task<AsyncTableQuery<MediaItem>> GetMediaItemsAsync();

        Task<MediaItem> GetMediaItemAsync(long id);

        Task RemoveMediaCollection(MediaCollection collection);

        Task RemoveMediaItem(MediaItem mediaItem);

        Task<Movie> GetMovieByMediaItem(long mediaItemId);

        Task<IEnumerable<MovieMediaItem>> GetMovieMediaItems(long movieId);

        Task RemoveMovieMediaItemsAsync(long movieId);

        Task<Movie> AddOrUpdateMovie(Movie dataModel);

        Task<MovieMediaItem> AddMovieMediaItem(MovieMediaItem mediaItem);

        Task<IEnumerable<TVShow>> GetTVShowsByName(string name);

        Task<TVShow> GetTVShow(long id);

        Task<TVShow> AddOrUpdateTVShow(TVShow show);

        Task<TVShowSeason> AddOrUpdateTVShowSeason(TVShowSeason season);

        Task<TVShowEpisode> AddOrUpdateTVShowEpisode(TVShowEpisode episode);

        Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId);

        Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId);

        Task RemoveTVShowEpisodeMediaItemsAsync(long episodeId);

        Task<TVShowEpisodeMediaItem> AddTVShowEpisodeMediaItem(TVShowEpisodeMediaItem mediaItem);

        Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowEpisodeMediaItems(long episodeId);

        Task<IEnumerable<Movie>> GetMovies();

        Task<Movie> GetMovie(long id);

        Task<IEnumerable<TVShow>> GetTVShows();

        Task<TVShowSeason> GetTVShowSeason(long id);

        Task<TVShowEpisode> GetTVShowEpisode(long id);

        Task RemoveMovie(long movieId);

        Task RemoveTVShow(long id);

        Task<IEnumerable<MovieCollection>> GetMovieCollectionsByName(string name);

        Task<MovieCollection> AddOrUpdateMovieCollection(MovieCollection collection);

        Task<IEnumerable<MovieCollection>> GetMovieCollections();

        Task<MovieCollection> GetMovieCollection(long id);

        Task<IEnumerable<MovieMediaItem>> GetMovieMediaItemsForMediaItem(long mediaItemId);

        Task RemoveMovieMediaItemAsync(MovieMediaItem movieMediaItem);

        Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowMediaItemsForMediaItem(long mediaItemId);

        Task RemoveMovieMediaItemAsync(TVShowEpisodeMediaItem movieMediaItem);

        Task<IEnumerable<Playlist>> GetPlaylists();

        Task<Playlist> GetPlaylist(long id);

        Task<IEnumerable<PlaylistEntry>> GetPlaylistEntries(long id);

        Task<Playlist> AddOrUpdatePlaylistAsync(Playlist playlist);

        Task<PlaylistEntry> AddOrUpdatePlaylistEntryAsync(PlaylistEntry playlistEntry);

        Task RemovePlaylistEntryAsync(PlaylistEntry mediaItemToDelete);

    }
}
