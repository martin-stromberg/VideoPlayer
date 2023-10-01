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
    }
}
