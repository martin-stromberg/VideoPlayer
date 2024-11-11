using System;
using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.MediaInformation;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Tools;

namespace VideoPlayer.Service.Library.Scanner
{

    public class LibraryScanner: SourceTimerService, ILibraryScanner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScannerSettings _Settings;
        private ConcurrentQueue<BaseServiceModel> _ForceEntries = new ConcurrentQueue<BaseServiceModel>();

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
        protected override void ProcessNotification(NotificationEventArgs e)
        {
            base.ProcessNotification(e);
            switch(e.Name)
            {
                case "Rescan":
                    EnqueueForceScan(e.Data as BaseServiceModel);                    
                    ForceExecute();
                    break;
            }
        }

        private void EnqueueForceScan(BaseServiceModel entry)
        {
            Reset(entry as Movie);
            Reset(entry as MovieCollection);
            Reset(entry as TVShow);
            Reset(entry as TVShowSeason);
            Reset(entry as TVShowEpisode);
            _ForceEntries.Enqueue(entry);
        }
        private void Reset(MovieCollection entry)
        {
            if (entry is null) return;
            foreach (var movie in _MediaLibrary.GetCollectionMovies(entry.Id))
                Reset(movie);
            var collection = _MediaLibrary.GetMediaCollection(entry.MediaItemCollectionId);
            Reset(collection, false);
        }
        private void Reset(MediaCollection collection, bool recurse = true)
        {
            collection.LastAccess = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateMediaCollection(collection);

            if (!recurse) return;
            foreach (var mediaItem in _MediaLibrary.GetMediaCollectionItems(collection.Id))
                Reset(mediaItem);
        }
        private void Reset(Movie entry)
        {
            if (entry is null) return;
            var mediaItems = entry.MediaItemIds
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null);
            foreach (var mediaItem in mediaItems)
                Reset(mediaItem);
        }
        private void Reset(TVShow entry)
        {
            if (entry is null) return;
            foreach (var season in _MediaLibrary.GetSeasons(entry.Id))
                Reset(season);
        }
        private void Reset(TVShowSeason entry)
        {
            if (entry is null) return;
            foreach (var episode in _MediaLibrary.GetEpisodes(entry.Id))
                Reset(episode);
        }
        private void Reset(TVShowEpisode entry)
        {
            if (entry is null) return;
            var mediaItems = entry.MediaItemIds
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .ToArray();
            foreach (var mediaItem in mediaItems)
                Reset(mediaItem);
            foreach (var collection in mediaItems
                .Select(mi => _MediaLibrary.GetMediaCollection(mi.ParentCollectionId))
                .Distinct())
                Reset(collection, false);
        }
        private void Reset(MediaItem mediaItem)
        {
            mediaItem.LastAccess = DateTime.MinValue;
            mediaItem.NeedsPictureUpdate = true;
            mediaItem.LastMetaInformationUpdate = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
        }

        protected override async Task ExecuteTimerAsync()
        {
            while (await ScanNextSourceAsync())
                CheckActive();
        }

        #region Forced Scan
        private async Task<bool> ScanNextForcedEntry()
        {
            CheckActive();

            if (!_ForceEntries.TryDequeue(out BaseServiceModel entry))
                return false;

            await ForceScan(entry as MediaItem);
            await ForceScan(entry as MediaCollection);
            ForceScan(entry as TVShow);
            ForceScan(entry as TVShowSeason);
            ForceScan(entry as TVShowEpisode);
            ForceScan(entry as Movie);
            ForceScan(entry as MovieCollection);
            return true;
        }

        private void ForceScan(MovieCollection movieCollection)
        {
            if (movieCollection is null) return;
            var collections = _MediaLibrary.GetCollectionMovies(movieCollection.Id)
                .SelectMany(movie => movie.MediaItemIds.Select(id => _MediaLibrary.GetMediaItem(id))
                .Select(mi => _MediaLibrary.GetMediaCollection(mi.ParentCollectionId))
                ).Distinct();
            foreach (var collection in collections)
                EnqueueForceScan(collection);
        }
        private void ForceScan(Movie movie)
        {
            if (movie is null) return;
            var mediaItems = movie.MediaItemIds.Select(id => _MediaLibrary.GetMediaItem(id));
            foreach (var mediaItem in mediaItems)
                _ForceEntries.Enqueue(mediaItem);
        }
        private void ForceScan(TVShowEpisode episode)
        {
            if (episode is null) return;
            var mediaItems = episode.MediaItemIds.Select(id => _MediaLibrary.GetMediaItem(id));
            foreach (var mediaItem in mediaItems)
                _ForceEntries.Enqueue(mediaItem);
        }
        private void ForceScan(TVShowSeason season)
        {
            if (season is null) return;
            var collections = _MediaLibrary.GetEpisodes(season.Id)
                .SelectMany(episode => episode.MediaItemIds.Select(id => _MediaLibrary.GetMediaItem(id))
                .Select(mi => _MediaLibrary.GetMediaCollection(mi.ParentCollectionId))
                .Distinct());
            foreach (var collection in collections)
                EnqueueForceScan(collection);
        }
        private void ForceScan(TVShow show)
        {
            if (show is null) return;
            var seasons = _MediaLibrary.GetSeasons(show.Id).ToArray();
            var episodes = seasons.SelectMany(season => _MediaLibrary.GetEpisodes(season.Id)).ToArray();
            var mediaItems = episodes.SelectMany(episode => episode.MediaItemIds.Select(id => _MediaLibrary.GetMediaItem(id))).ToArray();
            var collections = mediaItems.Select(mi => _MediaLibrary.GetMediaCollection(mi.ParentCollectionId)).ToArray();
            collections = collections.Distinct().ToArray();
            collections = collections.SelectMany(col =>
                {
                    var parentCol = col;
                    while (parentCol.MetaInformation is not null
                        && !(parentCol.MetaInformation is TVShowInformation)
                        && parentCol.ParentId != 0)
                        parentCol = _MediaLibrary.GetMediaCollection(col.ParentId);
                    if (parentCol.MetaInformation is null)
                        return new MediaCollection[] { col };
                    if (!(parentCol.MetaInformation is TVShowInformation))
                        return new MediaCollection[] { col }; ;
                    return new MediaCollection[] { col, parentCol };
                })
                .ToArray();
            collections = collections
                .Where(col => col is not null)
                .Distinct()
                .ToArray();
            foreach (var collection in collections)
                EnqueueForceScan(collection);
        }
        private void EnqueueForceScan(MediaCollection collection)
        {
            _ForceEntries.Enqueue(collection);
        }

        private async Task ForceScan(MediaCollection collection)
        {
            if (collection is null) return;
            var mediaItems = _MediaLibrary.GetMediaCollectionItems(collection.Id);
            foreach (var mediaItem in mediaItems)
                await ForceScan(mediaItem);
        }
        private async Task ForceScan(MediaItem mediaItem)
        {
            if (mediaItem is null) return;
            MediaCollection collection = _MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            collection.Classified = false;
            MediaCollection parentCollection = _MediaLibrary.GetMediaCollection(collection.ParentId);
            MediaSource source = _MediaLibrary.GetSource(collection.SourceId);
            var reader = CreateReader(source);
            var folder = reader.GetRoot();
            while (folder.Path != collection.Path)
            {               
                var relPath = collection.Path.Remove(0, PathTools.IncludeTrailingPathDelimiter(folder.Path).Length);
                var relPathParts = relPath.Split('/');
                var name = relPathParts.FirstOrDefault();
                folder = (await reader.ReadFoldersAsync(folder))
                    .Where(f => f.Name == name)
                    .FirstOrDefault();
            }
            await ScanAsync(source, reader, folder, parentCollection, true);
        }


        private async Task CheckScanNextForcedEntryAsync()
        {
            while (await ScanNextForcedEntry())
                ;
        }
        #endregion

        private async Task<bool> ScanNextSourceAsync()
        {
            await CheckScanNextForcedEntryAsync();
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
            MediaCollection parentCollection,
            bool skipScanForcedEntries = false)
        {
            if (!skipScanForcedEntries)
                await CheckScanNextForcedEntryAsync();
            else
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
                    NotifyScanCompleted();
                }                
            }
            finally
            {
                FinishProcess();
            }
        }

        private void NotifyScanCompleted()
        {
            Notify(this, new NotificationEventArgs("ScanCompleted", null));
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
            mediaItem.Classified = mediaItem.Classified && (mediaItem.LastAccess == file.LastWriteTime);
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
