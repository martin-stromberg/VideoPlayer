using VideoPlayerLib.Services.MediaLibrary.Models;

namespace VideoPlayerLib.Services.MediaLibrary
{
    public interface IMediaLibrary
    {
        Task<bool> IsEmptyAsync();
        Task<IEnumerable<MediaSource>> GetSourcesAsync();
        Task<MediaSource> GetSourceAsync(long id);
        Task AddSourceAsync(Models.MediaSource source);

        Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id);
        Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId);
        Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId);
        Task AddMediaItemCollectionAsync(Models.MediaItemCollection collection);
        Task<MediaItemCollection> FindMediaItemCollectionAsync(long id, string path);

        Task<MediaItem> GetMediaItemAsync(long id);
        Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId);
        Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId);
        Task AddMediaItemAsync(Models.MediaItem mediaItem);
        Task<MediaItem> FindMediaItemAsync(long SourceId, string path);
        
        Task ImportAsync(IMediaLibrary library);        
        Task ClearMedia();

        event EventHandler<BaseModelEventArgs> ModelElementAdded;
        event EventHandler<BaseModelEventArgs> ModelElementUpdated;
        event EventHandler<BaseModelEventArgs> ModelElementRemoved;
    }
}
