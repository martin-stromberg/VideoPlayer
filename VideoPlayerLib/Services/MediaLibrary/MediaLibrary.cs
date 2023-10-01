using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib;
using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace VideoPlayerLib.Services.MediaLibrary
{
    public class MediaLibrary : IMediaLibrary
    {
        private readonly IMediaLibraryDatabase dataStore;

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;
        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;
        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;
        protected void OnElementChanged(BaseModelEventArgs modelElementAdded, BaseModelEventArgs modelElementUpdated, BaseModelEventArgs modelElementRemoved)
        {
            if (modelElementAdded != null && ModelElementAdded != null)
                ModelElementAdded(this, modelElementAdded);
            if (modelElementUpdated != null && ModelElementUpdated != null)
                ModelElementUpdated(this, modelElementAdded);
            if (modelElementRemoved != null && ModelElementRemoved != null)
                ModelElementRemoved(this, modelElementAdded);
        }

        public MediaLibrary(IMediaLibraryDatabase dataStore)
        {
            this.dataStore = dataStore;
        }

        #region Sources
        public async Task<IEnumerable<MediaSource>> GetSourcesAsync()
        {
            return (await (await dataStore.GetSourcesAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaSource.FromDataModel(source) as MediaSource);
                
        }
        public async Task<MediaSource> GetSourceAsync(long id)
        {
            return MediaSource.FromDataModel(await dataStore.GetSourceAsync(id)) as MediaSource;
        }
        public async Task AddSourceAsync(Models.MediaSource source)
        {
            var isNew = source.Id == 0;
            var dataModel = source.ToDataModelAsync() as Database.Models.MediaSource;
            await dataStore.AddOrUpdateSourceAsync(dataModel);            
            source.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? new BaseModelEventArgs(source) : null, !isNew ? new BaseModelEventArgs(source) : null, null);
        }
        #endregion

        #region Media Collections
        public async Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId)
        {
            return (await (await dataStore.GetMediaCollectionsAsync())
                .Where(s => s.MediaSourceId == SourceId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }
        public async Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id)
        {
            return MediaItemCollection.FromDataModel(await dataStore.GetMediaCollectionAsync(Id)) as MediaItemCollection;
        }
        public async Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId)
        {
            return (await(await dataStore.GetMediaCollectionsAsync())
                .Where(s => s.ParentCollectionId == collectionId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }
        public async Task<MediaItemCollection> FindMediaItemCollectionAsync(long sourceId, string path)
        {
            return MediaItemCollection.FromDataModel(await (await dataStore.GetMediaCollectionsAsync())
                .FirstOrDefaultAsync(item => item.MediaSourceId == sourceId && item.Path == path)) as MediaItemCollection;
        }
        public async Task AddMediaItemCollectionAsync(MediaItemCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModel = collection.ToDataModelAsync() as Database.Models.MediaCollection;
            await dataStore.AddOrUpdateMediaCollectionAsync(dataModel);
            collection.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? new BaseModelEventArgs(collection) : null, !isNew ? new BaseModelEventArgs(collection) : null, null);
        }
        #endregion

        #region Media Items
        public async Task<MediaItem> GetMediaItemAsync(long id)
        {
            return MediaItem.FromDataModel(await dataStore.GetMediaItemAsync(id)) as MediaItem;
        }
        public async Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            return (await (await dataStore.GetMediaItemsAsync())
                .Where(s => s.ParentCollectionId == CollectionId
                         && s.OriginalMediaItemId == 0)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source) as MediaItem);
        }
        public async Task<MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            var items = await (await dataStore.GetMediaItemsAsync())
                .Where(item => item.Path == path)
                .ToArrayAsync();
            return items
                .Where(item =>
                {
                    var collection = dataStore
                        .GetMediaCollectionAsync(item.ParentCollectionId)
                        .Wait<Database.Models.MediaCollection>();
                    return collection.MediaSourceId == SourceId;
                })
                .Select(item => MediaItem.FromDataModel(item) as MediaItem)
                .FirstOrDefault();
        }
        public async Task AddMediaItemAsync(MediaItem mediaItem)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync() as Database.Models.MediaItem;
            await dataStore.AddOrUpdateMediaItemAsync(dataModel);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? new BaseModelEventArgs(mediaItem) : null, !isNew ? new BaseModelEventArgs(mediaItem) : null, null);
        }
        public async Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            return (await(await dataStore.GetMediaItemsAsync())
                .Where(s => s.OriginalMediaItemId == mediaItemId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source) as MediaItem);
        }
        #endregion 

        public async Task ImportAsync(IMediaLibrary library)
        {
            foreach (var fromSource in await library.GetSourcesAsync())
            {
                fromSource.Id = 0;
                await AddSourceAsync(fromSource);
            }
            //ToDo: Auch die MediaItems und MediaCollections
        }
        public async Task<bool> IsEmptyAsync()
        {
            return (await (await dataStore.GetSourcesAsync()).FirstOrDefaultAsync()) == null;
        }

        #region Clear
        public async Task ClearMedia()
        {
            foreach (var source in await (await dataStore.GetSourcesAsync()).ToArrayAsync())
            {
                await ClearSourceMediaAsync(source);
                source.LastScan = DateTime.MinValue;
                await dataStore.AddOrUpdateSourceAsync(source);
            }
        }
        private async Task ClearSourceMediaAsync(Database.Models.MediaSource source)
        {
            var collStore = await dataStore.GetMediaCollectionsAsync();
            var collections = await collStore.Where(c => c.MediaSourceId == source.Id).ToArrayAsync();
            foreach (var coll in collections)
            {
                ClearCaches(coll);
                await ClearCollectionMediaAsync(coll);
                await dataStore.RemoveMediaCollection(coll);
            }
        }
        private async Task ClearCollectionMediaAsync(Database.Models.MediaCollection coll)
        {
            var mediaStore = await dataStore.GetMediaItemsAsync();
            var mediaItems = await mediaStore.Where(mi => mi.ParentCollectionId == coll.Id).ToArrayAsync();
            foreach (var mediaItem in mediaItems)
            {
                ClearCaches(mediaItem);
                await dataStore.RemoveMediaItem(mediaItem);
            }
        }
        private void ClearCaches(Database.Models.MediaItem mediaItem)
        {
            if (File.Exists(mediaItem.PicturePath))
                File.Delete(mediaItem.PicturePath);
            if ((MediaItemCopyType)mediaItem.CopyType == MediaItemCopyType.Cache)
                File.Delete(mediaItem.Path);
        }
        #endregion

    }
}
