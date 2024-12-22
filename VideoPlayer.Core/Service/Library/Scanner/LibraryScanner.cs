using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.MediaInformation;
using VideoPlayer.Service.Library.Models.Sources;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;
using VideoPlayer.Tools;

namespace VideoPlayer.Service.Library.Scanner
{

    public class LibraryScanner: SourceTimerService, ILibraryScanner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScannerSettings _Settings;
        private readonly IApplicationSettings _ApplicationSettings;
        private ConcurrentQueue<BaseServiceModel> _ForceEntries = new ConcurrentQueue<BaseServiceModel>();
        private ConcurrentQueue<BaseServiceModel> _ForceReloadEntries = new ConcurrentQueue<BaseServiceModel>();

        public LibraryScanner(
            IMediaLibrary mediaLibrary,
            ILibraryScannerSettings settings,
            IApplicationSettings applicationSettings,
            IProcessorCollection processorCollection,
            ILogger<LibraryScanner> logger)
            : base(nameof(LibraryScanner), processorCollection, logger)
        {
            _Settings = settings;
            _ApplicationSettings = applicationSettings;
            _MediaLibrary = mediaLibrary;
            DueTime = settings.FirstCheck;
            Period = settings.CheckInterval;
        }
        protected override void CheckActive()
        {
            base.CheckActive();
            if (!_ApplicationSettings.ScanningEnabled)
                throw new CancelledException();
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
                case "Scan":
                    ForceExecute();
                    break;
                case "Reload":
                    EnqueueForceReload(e.Data as BaseServiceModel);
                    ForceExecute();
                    break;
            }
        }

        #region EnqueueForceReload
        private void EnqueueForceReload(BaseServiceModel entry)
        {
            if (!_ForceReloadEntries.Any(e => e.Id == entry.Id))
            {
                _MediaLibrary.Hold(entry);
                _ForceReloadEntries.Enqueue(entry);
            }
        }
        #endregion
        #region EnqueueForceScan
        private void EnqueueForceScan(BaseServiceModel entry)
        {
            Reset(entry as Movie);
            Reset(entry as MovieCollection);
            Reset(entry as TVShow);
            Reset(entry as TVShowSeason);
            Reset(entry as TVShowEpisode);
            _MediaLibrary.Hold(entry);
            _ForceEntries.Enqueue(entry);
        }
        private void Reset(MovieCollection entry)
        {
            if (entry is null) return;
            foreach (var movie in _MediaLibrary.GetCollectionMovies(entry.Id))
            {
                Reset(movie);
                _MediaLibrary.Release(movie);
            }
            var collection = _MediaLibrary.GetMediaCollection(entry.MediaItemCollectionId);
            Reset(collection, false);
            _MediaLibrary.Release(collection);
        }
        private void Reset(MediaCollection collection, bool recurse = true)
        {
            collection.LastAccess = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateMediaCollection(collection);

            if (!recurse) return;
            foreach (var mediaItem in _MediaLibrary.GetMediaCollectionItems(collection.Id))
            {
                Reset(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
        }
        private void Reset(Movie entry)
        {
            if (entry is null) return;
            var mediaItems = entry.MediaItemIds
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null);
            foreach (var mediaItem in mediaItems)
            {
                Reset(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
        }
        private void Reset(TVShow entry)
        {
            if (entry is null) return;
            foreach (var season in _MediaLibrary.GetSeasons(entry.Id))
            {
                Reset(season);
                _MediaLibrary.Release(season);
            }
        }
        private void Reset(TVShowSeason entry)
        {
            if (entry is null) return;
            foreach (var episode in _MediaLibrary.GetEpisodes(entry.Id))
            {
                Reset(episode);
                _MediaLibrary.Release(episode);
            }
        }
        private void Reset(TVShowEpisode entry)
        {
            if (entry is null) return;
            var mediaItems = entry.MediaItemIds
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .ToArray();
            foreach (var mediaItem in mediaItems)
            {
                Reset(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
            foreach (var collection in mediaItems
                .Select(mi => mi.ParentCollectionId)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id)))
            {
                Reset(collection, false);
                _MediaLibrary.Release(collection);
            }
        }
        private void Reset(MediaItem mediaItem)
        {
            mediaItem.LastAccess = DateTime.MinValue;
            mediaItem.NeedsPictureUpdate = true;
            mediaItem.LastMetaInformationUpdate = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
        }
        #endregion
        protected override void ExecuteTimerSync()
        {
            base.ExecuteTimerSync();
            while (ScanNextSource())
                CheckActive();
        }

        #region Forced Reload
        private bool CheckReloadNextForcedEntries()
        {
            CheckActive();
            if (!_ForceReloadEntries.TryDequeue(out BaseServiceModel entry))
                return false;
            try
            {
                ForceReload(entry as MediaItem);
                ForceReload(entry as MediaCollection);
                ForceReload(entry as TVShow);
                ForceReload(entry as TVShowSeason);
                ForceReload(entry as TVShowEpisode);
                ForceReload(entry as Movie);
                ForceReload(entry as MovieCollection);
                return true;
            }
            finally
            {
                _MediaLibrary.Release(entry);
            }
        }
        private void ForceReload(MovieCollection movieCollection)
        {
            if (movieCollection is null) return;
            var collections = _MediaLibrary.GetCollectionMovies(movieCollection.Id)
                .Where(movie => movie is not null)
                .SelectMany(movie => movie.MediaItemIds)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Select(mi => mi.ParentCollectionId)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id));
            foreach (var collection in collections)
            {
                EnqueueForceReload(collection);
                _MediaLibrary.Release(collection);
            }
        }
        private void ForceReload(Movie movie)
        {
            if (movie is null) return;
            var mediaItems = movie.MediaItemIds
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null);
            foreach (var mediaItem in mediaItems)
            {
                EnqueueForceReload(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
        }
        private void ForceReload(TVShowEpisode episode)
        {
            if (episode is null) return;
            var mediaItems = episode.MediaItemIds
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null);
            foreach (var mediaItem in mediaItems)
            {
                EnqueueForceReload(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
        }
        private void ForceReload(TVShowSeason season)
        {
            if (season is null) return;
            var collections = _MediaLibrary.GetEpisodes(season.Id)
                .Where(episode => episode is not null)
                .SelectMany(episode => episode.MediaItemIds)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Select(mi => mi.ParentCollectionId)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id))
                .Where(collection => collection is not null);
            foreach (var collection in collections)
            {
                EnqueueForceReload(collection);
                _MediaLibrary.Release(collection);
            }
        }
        private void ForceReload(TVShow show)
        {
            if (show is null) return;
            var collections = _MediaLibrary
                .GetSeasons(show.Id)
                .Where(season => season is not null)
                .SelectMany(season =>
                {
                    _MediaLibrary.Release(season);
                    return _MediaLibrary.GetEpisodes(season.Id);
                })
                .Where(episode => episode is not null)
                .SelectMany(episode =>
                {
                    _MediaLibrary.Release(episode);
                    return episode.MediaItemIds;
                })
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Select(mi =>
                {
                    _MediaLibrary.Release(mi);
                    return mi.ParentCollectionId;
                })
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id))
                .Where(col => col is not null)
                .Distinct()
                .SelectMany(col =>
            {
                var parentCol = col;
                while (parentCol.MetaInformation is not null
                    && !(parentCol.MetaInformation is TVShowInformation)
                    && parentCol.ParentId != 0)
                    parentCol = _MediaLibrary.GetMediaCollection(col.ParentId);
                if (parentCol.MetaInformation is null)
                {
                    _MediaLibrary.Release(parentCol);
                    return new MediaCollection[] { col };
                }
                if (!(parentCol.MetaInformation is TVShowInformation))
                {
                    _MediaLibrary.Release(parentCol);
                    return new MediaCollection[] { col };
                }
                return new MediaCollection[] { col, parentCol };
            })
                .Where(col => col is not null)
                .ToArray();
            foreach (var collection in collections.Distinct())
                EnqueueForceReload(collection);
            foreach (var collection in collections)
                _MediaLibrary.Release(collection);
        }
        private void ForceReload(MediaCollection collection)
        {
            if (collection is null) return;
            var mediaItems = _MediaLibrary.GetMediaCollectionItems(collection.Id);
            foreach (var mediaItem in mediaItems)
            {
                EnqueueForceReload(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }
        }
        private void ForceReload(MediaItem mediaItem)
        {
            if (mediaItem is null) return;

            var collection = _MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            var source = collection is not null ? _MediaLibrary.GetSource(collection.SourceId): null;
            var parentCollection = collection is not null ? _MediaLibrary.GetMediaCollection(collection.ParentId) : null;
            try
            {
                var movie = _MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
                var episode = _MediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);

                if (movie is not null)
                    _MediaLibrary.Delete(movie);
                else if (episode is not null)
                    _MediaLibrary.Delete(episode);
                else
                    _MediaLibrary.Delete(mediaItem);

                if (parentCollection is not null && parentCollection.MetaInformation is not null)
                    EnqueueForceScan(parentCollection);
                EnqueueForceScan(collection);
            }
            finally
            {
                _MediaLibrary.Release(source);
                _MediaLibrary.Release(parentCollection);
                _MediaLibrary.Release(collection);
            }
        }
        #endregion


        #region Forced Scan
        private  bool ScanNextForcedEntry()
        {
            CheckActive();

            if (!_ForceEntries.TryDequeue(out BaseServiceModel entry))
                return false;
            try
            {
                ForceScan(entry as MediaItem);
                ForceScan(entry as MediaCollection);
                ForceScan(entry as TVShow);
                ForceScan(entry as TVShowSeason);
                ForceScan(entry as TVShowEpisode);
                ForceScan(entry as Movie);
                ForceScan(entry as MovieCollection);
                return true;
            }
            finally
            {
                _MediaLibrary.Release(entry);
            }
        }

        private void ForceScan(MovieCollection movieCollection)
        {
            if (movieCollection is null) return;
            var collections = _MediaLibrary.GetCollectionMovies(movieCollection.Id)
                .SelectMany(movie => movie.MediaItemIds)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Select(mi => mi.ParentCollectionId)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id));
            foreach (var collection in collections)
            {
                EnqueueForceScan(collection);
                _MediaLibrary.Release(collection);
            }
        }
        private void ForceScan(Movie movie)
        {
            if (movie is null) return;
            var mediaItems = movie.MediaItemIds
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id));
            foreach (var mediaItem in mediaItems)
                _ForceEntries.Enqueue(mediaItem);
        }
        private void ForceScan(TVShowEpisode episode)
        {
            if (episode is null) return;
            var mediaItems = episode.MediaItemIds
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id));
            foreach (var mediaItem in mediaItems)
                _ForceEntries.Enqueue(mediaItem);
        }
        private void ForceScan(TVShowSeason season)
        {
            if (season is null) return;
            var collections = _MediaLibrary.GetEpisodes(season.Id)
                .SelectMany(episode => episode.MediaItemIds)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Select(mi => mi.ParentCollectionId)
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id));
            foreach (var collection in collections)
            {
                EnqueueForceScan(collection);
                _MediaLibrary.Release(collection);
            }
        }
        private void ForceScan(TVShow show)
        {
            if (show is null) return;
            var seasons = _MediaLibrary
                .GetSeasons(show.Id)
                .ToArray();
            var episodes = seasons
                .SelectMany(season =>
                {
                    _MediaLibrary.Release(season);
                    return _MediaLibrary.GetEpisodes(season.Id);
                })
                .ToArray();
            var mediaItems = episodes
                .SelectMany(episode => 
                {
                    _MediaLibrary.Release(episode);
                    return episode.MediaItemIds; 
                })
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .ToArray();
            var collections = mediaItems
                .Select(mi => 
                {
                    _MediaLibrary.Release(mi);
                    return mi.ParentCollectionId; 
                })
                .Distinct()
                .Select(id => _MediaLibrary.GetMediaCollection(id))
                .Where(col => col is not null)
                .ToArray();
            collections = collections.Distinct().ToArray();
            collections = collections.SelectMany(col =>
                {
                    var parentCol = col;
                    while (parentCol.MetaInformation is not null
                        && !(parentCol.MetaInformation is TVShowInformation)
                        && parentCol.ParentId != 0)
                        parentCol = _MediaLibrary.GetMediaCollection(col.ParentId);
                    if (parentCol.MetaInformation is null)
                    {
                        _MediaLibrary.Release(parentCol);
                        return new MediaCollection[] { col };
                    }
                    if (!(parentCol.MetaInformation is TVShowInformation))
                    {
                        _MediaLibrary.Release(parentCol);
                        return new MediaCollection[] { col };
                    }
                    return new MediaCollection[] { col, parentCol };
                })
                .Where(col => col is not null)
                .ToArray();
            foreach (var collection in collections.Distinct())
                EnqueueForceScan(collection);
            foreach (var collection in collections)
                _MediaLibrary.Release(collection);
        }
        private void EnqueueForceScan(MediaCollection collection)
        {
            _ForceEntries.Enqueue(collection);
        }

        private void ForceScan(MediaCollection collection)
        {
            if (collection is null) return; 
            
            collection.LastAccess = DateTime.MinValue;
            _MediaLibrary.AddOrUpdateMediaCollection(collection);

            var mediaItems = _MediaLibrary.GetMediaCollectionItems(collection.Id);
            foreach (var mediaItem in mediaItems)
            {
                ForceScan(mediaItem);
                _MediaLibrary.Release(mediaItem);
            }

            MediaCollection parentCollection = _MediaLibrary.GetMediaCollection(collection.ParentId);
            MediaSource source = _MediaLibrary.GetSource(collection.SourceId);
            try
            {
                var reader = CreateReader(source);
                var folder = reader.GetRoot();
                while (folder.Path != collection.Path)
                {
                    var relPath = collection.Path.Remove(0, PathTools.IncludeTrailingPathDelimiter(folder.Path).Length);
                    var relPathParts = relPath.Split('/');
                    var name = relPathParts.FirstOrDefault();
                    folder = (reader.ReadFolders(folder))
                        .Where(f => f.Name == name)
                        .FirstOrDefault();
                }
                Scan(source, reader, folder, parentCollection, true);
            }
            finally
            {
                _MediaLibrary.Release(collection);
                _MediaLibrary.Release(parentCollection);
                _MediaLibrary.Release(source);
            }
        }
        private void ForceScan(MediaItem mediaItem)
        {
            if (mediaItem is null) return;
            MediaCollection collection = _MediaLibrary.GetMediaCollection(mediaItem.ParentCollectionId);
            collection.Classified = false;
            MediaCollection parentCollection = _MediaLibrary.GetMediaCollection(collection.ParentId);
            MediaSource source = _MediaLibrary.GetSource(collection.SourceId);
            try
            {
                var reader = CreateReader(source);
                var folder = reader.GetRoot();
                while (folder.Path != collection.Path)
                {
                    var relPath = collection.Path.Remove(0, PathTools.IncludeTrailingPathDelimiter(folder.Path).Length);
                    var relPathParts = relPath.Split('/');
                    var name = relPathParts.FirstOrDefault();
                    folder = (reader.ReadFolders(folder))
                        .Where(f => f.Name == name)
                        .FirstOrDefault();
                }
                Scan(source, reader, folder, parentCollection, true);
            }
            finally
            {
                _MediaLibrary.Release(collection);
                _MediaLibrary.Release(parentCollection);
                _MediaLibrary.Release(source);
            }
        }


        private void CheckScanNextForcedEntry()
        {
            while (ScanNextForcedEntry())
                ;
        }
        #endregion

        private bool ScanNextSource()
        {
            if (!_ApplicationSettings.ScanningEnabled)
                return false;
            while (CheckReloadNextForcedEntries());
            CheckScanNextForcedEntry();
            var source = _MediaLibrary.GetNextScanSource();
            if (source is null)
                return false;
            try
            {
                if (source.Deleted)
                {
                    DeleteSource(source);
                    return true;
                }                
                if (_Settings.SourceScanInterval > TimeSpan.Zero)
                    if (source.LastScan.Add(_Settings.SourceScanInterval) > DateTime.Now)
                        return false;
                ScanSource(source);                
                return true;
            }
            finally
            {
                _MediaLibrary.Release(source);
            }
        }

        private void DeleteSource(MediaSource source)
        {
            StartProcess($"Entferne Quelle {source.Name}");
            try
            {
                _MediaLibrary.Delete(source);
            }
            finally
            {
                FinishProcess();
            }
        }

        private void ScanSource(MediaSource source)
        {
            var reader = CreateReader(source);
            var root = reader.GetRoot();
            Scan(source, reader, root, null);

            source.LastScan = DateTime.Now;
            _MediaLibrary.AddOrUpdateSource(source);
        }

        private void Scan(
            MediaSource source,
            ISourceReader reader,
            SourceFolder currentFolder,
            MediaCollection parentCollection,
            bool skipScanForcedEntries = false)
        {
            if (!skipScanForcedEntries)
                CheckScanNextForcedEntry();
            else
                CheckActive();
            StartProcess($"Erfasse {currentFolder.FullPath}");
            try
            {
                var collection = ProcessFolder(source, parentCollection, currentFolder);
                try
                {
                    if (!collection.LastScanCompleted)
                    {
                        var folders = reader.ReadFolders(currentFolder);
                        foreach (var folder in folders)
                            Scan(source, reader, folder, collection);

                        NotifyStatus($"Erfasse {currentFolder.FullPath}");
                        var files = reader.ReadFiles(currentFolder);
                        foreach (var file in files)
                            ProcessFile(file, collection);                        

                        collection.LastScanCompleted = true;
                        _MediaLibrary.AddOrUpdateMediaCollection(collection);

                        NotifyScanCompleted();
                    }                    
                }
                finally
                {
                    _MediaLibrary.Release(collection);
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

            collection.LastScanCompleted = collection.LastScanCompleted && (collection.LastAccess == folder.LastWriteTime) && (folder.LastWriteTime != DateTime.MinValue);
            collection.Classified = collection.Classified && collection.LastScanCompleted;
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
            var isNew = (mediaItem is null);
            if (isNew)
                mediaItem = CreateMediaItem(collection.Id, file);
            mediaItem.Classified = mediaItem.Classified && (mediaItem.LastAccess == file.LastWriteTime);
            mediaItem.NeedsPictureUpdate = false;
            mediaItem.LastAccess = file.LastWriteTime;
            mediaItem = _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
            if (isNew)
                _MediaLibrary.Release(mediaItem);
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
