using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.Database
{
    public class MediaLibraryDatabase : IMediaLibraryDatabase
    {
        private readonly MediaLibraryDatabaseSettings settings;
        private SQLiteAsyncConnection connection;

        public MediaLibraryDatabase(MediaLibraryDatabaseSettings settings)
        {
            this.settings = settings;
        }

        protected SQLiteAsyncConnection Connection
        {
            get
            {
                if (connection == null)
                    connection = new SQLiteAsyncConnection(settings.FilePath, settings.OpenFlags);
                return connection;
            }
        } 

        private async Task InitOrUpgradeAsync()
        {
            var result = await Connection.CreateTableAsync<Models.MediaSource>();
            result = await Connection.CreateTableAsync<Models.MediaCollection>();
            result = await Connection.CreateTableAsync<Models.MediaItem>();
            result = await Connection.CreateTableAsync<Models.LogEntry>();
        }

        public async Task<AsyncTableQuery<MediaSource>> GetSourcesAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaSource>();
        }
        public async Task<Models.MediaSource> GetSourceAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaSource>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<Models.MediaCollection>> GetMediaCollectionsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaCollection>();
        }
        public async Task<Models.MediaCollection> GetMediaCollectionAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaCollection>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<MediaItem>> GetMediaItemsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaItem>();
        }
        public async Task<Models.MediaItem> GetMediaItemAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaItem>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<MediaSource> AddOrUpdateSourceAsync(MediaSource mediaSource)
        {
            return await AddOrUpdate<MediaSource>(mediaSource) as MediaSource;
        }

        private async Task<BaseDataModel> AddOrUpdate<T>(T model) where T : new()
        {
            var dataModel = model as BaseDataModel;
            if (dataModel == null)
                throw new ArgumentException(nameof(model));            

            await InitOrUpgradeAsync();
            Debug.WriteLine($"AddOrUpdate({typeof(T)})");
            var existing = (await Connection.Table<T>()
                .ToArrayAsync())                
                .FirstOrDefault(rec => (rec as BaseDataModel).IsRecord(model as BaseDataModel)) as BaseDataModel;
            if (existing == null)
            {
                Debug.WriteLine($"AddOrUpdate({typeof(T)}).Insert");
                await Connection.InsertAsync(model);
                return dataModel;
            }

            Debug.WriteLine($"AddOrUpdate({typeof(T)}).Update");
            existing.Update(dataModel);
            await Connection.UpdateAsync(existing);
            return dataModel;
        }

        public async Task<MediaCollection> AddOrUpdateMediaCollectionAsync(MediaCollection collection)
        {
            return await AddOrUpdate<MediaCollection>(collection) as MediaCollection;
        }

        public async Task<MediaItem> AddOrUpdateMediaItemAsync(MediaItem mediaItem)
        {
            return await AddOrUpdate<MediaItem>(mediaItem) as MediaItem;
        }

        public async Task RemoveMediaCollection(MediaCollection collection)
        {
            await Connection.DeleteAsync(collection);
        }

        public async Task RemoveMediaItem(MediaItem mediaItem)
        {
            await Connection.DeleteAsync(mediaItem);
        }

        public async Task AddLog(LogEntry entry)
        {
            await InitOrUpgradeAsync();
            _ = await AddOrUpdate<LogEntry>(entry) as LogEntry;
        }

        public async Task<IEnumerable<LogEntry>> GetLogs()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<LogEntry>().ToArrayAsync();
        }

        public async Task RemoveLog(LogEntry log)
        {            
            await Connection.DeleteAsync(log);
        }
    }
}
