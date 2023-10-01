using CommunityToolkit.Maui.Views;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Database.Models;

namespace VideoMeister.Services.Database
{
    public class MediaLibraryDatabase
    {
        SQLiteAsyncConnection Database;

        public MediaLibraryDatabase(DatabaseSettings settings)
        {
            this.settings = settings;
        }
        private object lockObj = new object();
        private readonly DatabaseSettings settings;

        async Task Init()
        {
            if (Database is not null)
                return;
            Debug.WriteLine($"Init()");
            Database = new SQLiteAsyncConnection(settings.FilePath, settings.Flags);
            var result = await Database.CreateTableAsync<Models.MediaSource>();
            result = await Database.CreateTableAsync<Models.MediaItem>();            
        }

        public async Task<IEnumerable<Models.MediaSource>> GetSources() 
        {
            await Init();
            Debug.WriteLine($"GetSources()");
            return await Database
                .Table<Models.MediaSource>()
                .ToArrayAsync();
        }
        internal async Task<Models.MediaSource> AddOrUpdate(Models.MediaSource mediaSource)
        {
            await Init();
            Debug.WriteLine($"AddOrUpdate(MediaSource)");
            var existing = (await GetSources()).FirstOrDefault(s => s.Equals(mediaSource));
            if (existing != null)
                return existing;
            Debug.WriteLine($"AddOrUpdate(MediaSource).Insert");
            await Database.InsertAsync(mediaSource);
            return mediaSource;
        }

        public async Task<AsyncTableQuery<MediaItem>> GetItemsAsync()
        {
            await Init();
            Debug.WriteLine($"GetItems()");
            return Database
                .Table<Models.MediaItem>();
        }
        internal async Task<Models.MediaItem> AddOrUpdate(MediaItem mediaItem)
        {
            await Init();
            Debug.WriteLine($"AddOrUpdate(MediaItem)");
            var existing = (await (await GetItemsAsync())
                .Where(s => s.SourceId == mediaItem.SourceId 
                                        && s.Type == mediaItem.Type
                                        && s.Path == mediaItem.Path
                                        && s.ParentId == mediaItem.ParentId)
                .ToArrayAsync())
                .FirstOrDefault(s => s.Equals(mediaItem));
            if (existing != null)
                return existing;
            Debug.WriteLine($"AddOrUpdate(MediaItem).Insert");
            await Database.InsertAsync(mediaItem);
            return mediaItem;
        }
    }
}
