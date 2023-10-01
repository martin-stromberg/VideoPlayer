using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Net.Http.Headers;
using VideoMeister.Services.Database;
using VideoMeister.Services.Models;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.Services.Library
{
    public class MediaLibrary
    {
        public MediaLibrary(
            MediaLibraryDatabase database,
            string path)
        {
            this.database = database;
            this.path = path;
        }

        private readonly string[] folderBlacklist = { "$RECYCLE.BIN" };
        private readonly string[] fileExtVideo = { ".avi", ".mkv" };
        private readonly MediaLibraryDatabase database;
        private readonly string path;
        private Queue<VideoSource> sourcesToScan = new Queue<VideoSource>();
        private BackgroundWorker scanner = null;
        private void StartScanner()
        {
            if (scanner != null)
                return;
            scanner = new BackgroundWorker();
            scanner.DoWork += Scan;
            scanner.RunWorkerCompleted += ScanCompleted;
            scanner.RunWorkerAsync();
        }
        private void ScanCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (sourcesToScan.Count == 0)
                return;
            scanner.RunWorkerAsync();
        }
        private void Scan(object sender, DoWorkEventArgs e)
        {
            try
            {
                var source = sourcesToScan.Dequeue();
                if (source == null)
                    return;
                ScanAsync(source).Wait();
            }
            catch(Exception ex) 
            { 
                Console.WriteLine(ex.ToString());
            }
        }
        private async Task ScanAsync(VideoSource source)
        {
            var cacheFolder = Cache.CreateFolder(source.Name);
            var mediaSource = await SaveVideoSourceAsync(source);
            foreach (var file in source.Files)
                await ScanAsync(file, cacheFolder, mediaSource, null);
            foreach (var folder in source.Folders
                .Where(f => !folderBlacklist.Contains(f.Name)))
                await ScanAsync(folder, cacheFolder.CreateFolder(folder.Name), mediaSource, null);
        }

        private async Task<Database.Models.MediaSource> SaveVideoSourceAsync(VideoSource source)
        {
            Database.Models.MediaSource mediaSource = new Database.Models.MediaSource()
            {
                Name = source.Name,
                Type = source.GetType().Name,
                Configuration = source.ConfigurationString
            };
            return await database.AddOrUpdate(mediaSource);
        }

        private async Task ScanAsync(MediaItemCollection folder, DriveMediaItemCollection cacheFolder, Database.Models.MediaSource mediaSource, Database.Models.MediaItem parentMediaFolder)
        {
            var mediaFolder = await SaveMediaItemCollectionAsync(folder, mediaSource, parentMediaFolder);
            foreach (var file in folder.Files)
                await ScanAsync(file, cacheFolder, mediaSource, mediaFolder);
            foreach (var subFolder in folder.Folders
                .Where(f => !folderBlacklist.Contains(f.Name)))
                await ScanAsync(subFolder, cacheFolder.CreateFolder(subFolder.Name), mediaSource, mediaFolder);
        }

        private async Task<Database.Models.MediaItem> SaveMediaItemCollectionAsync(MediaItemCollection folder, Database.Models.MediaSource mediaSource, Database.Models.MediaItem parentMediaFolder)
        {
            Database.Models.MediaItem mediaItem = new Database.Models.MediaItem()
            {
                Type = Database.Models.MediaItemType.Folder,
                Name = folder.Name,
                SourceId = mediaSource.Id,
                ParentId = parentMediaFolder == null ? 0 : parentMediaFolder.Id,
            };
            return await database.AddOrUpdate(mediaItem);
        }

        private async Task ScanAsync(MediaItem file, DriveMediaItemCollection cacheFolder, Database.Models.MediaSource mediaSource, Database.Models.MediaItem parentMediaFolder)
        {
            var fileInfo = new FileInfo(file.URI);
            var nfoFile = file.Parent.Files.FirstOrDefault(f => f.Name == $"{fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length)}.nfo");
            var imageFile = file.Parent.Files.FirstOrDefault(f => f.Name == $"{fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length)}.jpg");
            if (imageFile == null)
                imageFile = file.Parent.Files.FirstOrDefault(f => f.Name == $"{fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length)}.png");
            if (fileExtVideo.Contains(fileInfo.Extension.ToLower()))
                await ScanVideoAsync(file, nfoFile, imageFile, cacheFolder, mediaSource, parentMediaFolder);
        }
        private MediaItem CacheFile(MediaItem sourceItem, DriveMediaItemCollection cacheFolder)
        {
            if (sourceItem == null)
                return null;
            var destFile = $"{cacheFolder.URI}\\{sourceItem.Name}";
            var localFile = cacheFolder.Files.FirstOrDefault(f => f.Name == sourceItem.Name);
            if (localFile == null)
            {
                sourceItem.Source.Download(sourceItem, destFile);
                cacheFolder.Refresh();
                localFile = cacheFolder.Files.FirstOrDefault(f => f.Name == sourceItem.Name);
            }
            return localFile;
        }
        private MediaItem LinkFile(MediaItem sourceItem, DriveMediaItemCollection cacheFolder)
        {
            if (sourceItem == null)
                return null;
            var destFile = $"{cacheFolder.URI}\\{sourceItem.Name}";
            var localFile = cacheFolder.Files.FirstOrDefault(f => f.Name == $"{sourceItem.Name}");
            if (localFile == null)
            {
                destFile = $"{cacheFolder.URI}\\{sourceItem.Name}.url";
                localFile = cacheFolder.Files.FirstOrDefault(f => f.Name == $"{sourceItem.Name}.url");
                if (localFile == null)
                {
                    using (StreamWriter writer = new StreamWriter(destFile))
                    {
                        writer.WriteLine("[InternetShortcut]");
                        writer.WriteLine("URL=" + sourceItem.URI);
                    }
                    cacheFolder.Refresh();
                    localFile = cacheFolder.Files.FirstOrDefault(f => (f.Name == sourceItem.Name) || (f.Name == $"{sourceItem.Name}.url"));
                }
            }
            return localFile;
        }
        private async Task ScanVideoAsync(MediaItem mediaFile, MediaItem nfoFile, MediaItem imageFile, DriveMediaItemCollection cacheFolder, Database.Models.MediaSource mediaSource, Database.Models.MediaItem parentMediaFolder)
        {
            nfoFile = CacheFile(nfoFile, cacheFolder);
            imageFile = CacheFile(imageFile, cacheFolder);
            MediaItem localFile = LinkFile(mediaFile, cacheFolder);
            mediaFile.AlternateFile = localFile;

            await SaveMediaItemAsync(mediaFile, mediaSource, parentMediaFolder);
        }



        private async Task SaveMediaItemAsync(MediaItem mediaFile, Database.Models.MediaSource mediaSource, Database.Models.MediaItem parentMediaFolder)
        {
            Database.Models.MediaItem mediaItem = null;
            if (mediaFile.AlternateFile != null)
            {
                mediaItem = new Database.Models.MediaItem()
                {
                    Type = Database.Models.MediaItemType.File,
                    Name = mediaFile.AlternateFile.Name,
                    SourceId = 0,
                    ParentId = 0,
                    AlternateId = 0,
                    Path = mediaFile.AlternateFile.URI
                };
                await database.AddOrUpdate(mediaItem);
            }
            mediaItem = new Database.Models.MediaItem()
            {
                Type = Database.Models.MediaItemType.File,
                Name = mediaFile.Name,
                SourceId = mediaSource.Id,
                ParentId = parentMediaFolder == null ? 0 : parentMediaFolder.Id,
                AlternateId = mediaItem == null ? 0 : mediaItem.Id,
                Path = mediaFile.URI
            };
            await database.AddOrUpdate(mediaItem);
        }

        private DriveVideoSource cache = null;
        protected DriveVideoSource Cache => cache ?? (cache = new DriveVideoSource()
        {
            Name = "Cache",
            Path = $"{path}"
        });

        public async void StartScan(VideoSource source)
        {
            await SaveVideoSourceAsync(source);
            sourcesToScan.Enqueue(source);
            StartScanner();
        }
    }
}
