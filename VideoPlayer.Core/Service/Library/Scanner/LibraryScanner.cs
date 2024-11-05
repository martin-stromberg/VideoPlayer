using System;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Service.Library.Scanner
{

    public class LibraryScanner: SourceTimerService, ILibraryScanner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScannerSettings _Settings;

        public LibraryScanner(
            IMediaLibrary mediaLibrary,
            ILibraryScannerSettings settings)
            : base()
        {
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
            DueTime = settings.FirstCheck;
            Period = settings.CheckInterval;
        }

        protected override async Task ExecuteTimerAsync()
        {
            while (await ScanNextSourceAsync())
                CheckActive();
        }

        private async Task<bool> ScanNextSourceAsync()
        {
            var source = _MediaLibrary.GetNextScanSource();
            if (source is null)
                return false;
            if (_Settings.SourceScanInterval > TimeSpan.Zero)
                if (source.LastScan.Add(_Settings.SourceScanInterval) > DateTime.Now)
                    return false;
            await ScanSourceAsync(source);
            return true;
        }

        private async Task ScanSourceAsync(MediaSource source)
        {
            var reader = CreateReader(source);
            var root = reader.GetRoot();
            await ScanAsync(source, reader, root, null);

            source.LastScan = DateTime.Now;
            _MediaLibrary.AddOrUpdateSource(source);
        }

        private async Task ScanAsync(
            MediaSource source,
            ISourceReader reader,
            SourceFolder currentFolder,
            MediaCollection parentCollection)
        {
            CheckActive();
            StartProcess($"Erfasse {currentFolder.FullPath}");
            try
            {
                var collection = ProcessFolder(source, parentCollection, currentFolder);

                if (!collection.Classified)
                {
                    var folders = await reader.ReadFoldersAsync(currentFolder);
                    foreach (var folder in folders)
                        await ScanAsync(source, reader, folder, collection);

                    NotifyStatus($"Erfasse {currentFolder.FullPath}");
                    var files = await reader.ReadFilesAsync(currentFolder);
                    foreach (var file in files)
                        ProcessFile(file, collection);
                }
            }
            finally
            {
                FinishProcess();
            }
        }

        private MediaCollection ProcessFolder(MediaSource source, MediaCollection parentCollection, SourceFolder folder)
        {
            var collection = _MediaLibrary.GetMediaCollectionByPath(source.Id, folder.Path);

            if (collection is null)
                collection = CreateCollection(source.Id, parentCollection?.Id ?? 0, folder);

            collection.Classified = collection.Classified && (collection.LastAccess == folder.LastWriteTime);
            collection.LastAccess = folder.LastWriteTime;
            collection = _MediaLibrary.AddOrUpdateMediaCollection(collection);
            return collection;
        }

        private MediaCollection CreateCollection(long sourceId, long parentCollectionId, SourceFolder folder)
        {
            var collection = new MediaCollection()
            {
                LastAccess = DateTime.MinValue,
                Name = folder.Name,
                ParentId = parentCollectionId,
                SourceId = sourceId,
                Path = folder.Path,
                Classified = false,
                Id = 0
            };
            return collection;
        }

        private void ProcessFile(SourceFile file, MediaCollection collection)
        {
            var mediaItem = _MediaLibrary.GetMediaItemByPath(collection.Id, file.Path);
            if (mediaItem is null)
                mediaItem = CreateMediaItem(collection.Id, file);
            mediaItem.Classified = mediaItem.Classified && (mediaItem.LastAccess != file.LastWriteTime);
            mediaItem.LastAccess = file.LastWriteTime;
            mediaItem = _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
        }

        private MediaItem CreateMediaItem(long collectionId, SourceFile file)
        {
            var mediaItem = new MediaItem()
            {
                Id = 0,
                Name = file.Name,
                Path = file.Path,
                ParentCollectionId = collectionId,
                Classified = false,
                LastAccess = DateTime.MinValue
            };
            return mediaItem;
        }

    }
}
