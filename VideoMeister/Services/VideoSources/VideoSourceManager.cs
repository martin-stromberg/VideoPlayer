using CommunityToolkit.Maui.Views;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Helper;
using VideoMeister.Services.Database;
using VideoMeister.Services.Models;

namespace VideoMeister.Services.VideoSources
{
    public class VideoSourceManager
    {
        
        public VideoSourceManager(VideoSourceManagerSettings settings, MediaLibraryDatabase database) 
            :base()
        {
            this.database = database;
            this.LibraryPath = settings.LibraryPath;
        }

        private bool isInitiaized= false;
        internal void Initialize()
        {
            if (isInitiaized)
                return;
            try
            {
                LoadSourcesFromDatabase();
                isInitiaized = true;
            }
            catch(Exception ex) 
            {
                Debug.WriteLine(ex);
            }
        }

        private async void LoadSourcesFromDatabase()
        {
            foreach (var source in await database.GetSources())
            {
                VideoSource videoSource;
                switch (source.Type)
                {
                    case nameof(CachedSmbShareVideoSource):
                        videoSource = new CachedSmbShareVideoSource();
                        break;
                    default:
                        throw new NotImplementedException($"{source.Type}");
                }
                videoSource.Name = source.Name;
                videoSource.LoadConfiguration(source.Configuration);
                Add(videoSource);
            }
        }

        private Library.MediaLibrary library = null;
        private readonly MediaLibraryDatabase database;

        public Library.MediaLibrary Library
        {
            get
            {
                if (library == null)
                    library = new Library.MediaLibrary(database, LibraryPath);
                return library;
            }
        }
        public ObservableCollection<VideoSource> Sources { get; } = new ObservableCollection<VideoSource>();
        public string LibraryPath { get; set; }

        public void Add(VideoSource source)
        {
            Sources.Add(source);
            Library.StartScan(source);
        }


        public async Task<IEnumerable<MediaItem>> GetMediaItemsAsync(VideoSource source)
        {
            var dbSource = (await database.GetSources()).FirstOrDefault(s => s.Configuration == source.ConfigurationString);
            var collection = new LibraryMediaCollection(source);

            return (await (await database.GetItemsAsync())
                .Where(item => item.SourceId == dbSource.Id)
                .ToArrayAsync())
                .Select<Database.Models.MediaItem, MediaItem>(item =>
                {
                    var alternateItem = (item.AlternateId == 0) ? null : database.GetItemsAsync().Wait<AsyncTableQuery<Database.Models.MediaItem>>().FirstOrDefaultAsync(i => i.Id == item.AlternateId).Wait<Database.Models.MediaItem>();
                    return new LibraryMediaItem(item, collection)
                    {
                        AlternateFile = alternateItem == null ? null : new LibraryMediaItem(alternateItem, collection)
                    };
                });
        }
        internal MediaSource CreateVideoSource(MediaItem item)
        {
            return item.Source.CreateMediaSource(item);
        }

        
    }
}
