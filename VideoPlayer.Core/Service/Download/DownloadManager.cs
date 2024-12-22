using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Service.Processor;
using VideoPlayer.Tools;
using static SQLite.SQLite3;

namespace VideoPlayer.Service.Download
{
    public class DownloadManager : SourceTimerService, IDownloadManager
    {        
        private ConcurrentQueue<DownloadSession> queueCheck = new ConcurrentQueue<DownloadSession>();
        private ConcurrentQueue<DownloadSession> queue = new ConcurrentQueue<DownloadSession>();
        private readonly IMediaLibrary mediaLibrary;
        private readonly IEnvironment environment;
        private readonly IMediaCollectionSelector mediaCollectionSelector;

        public DownloadManager(
            IMediaLibrary mediaLibrary,
            IEnvironment environment,
            IMediaCollectionSelector mediaCollectionSelector,
            IProcessorCollection processorCollection,
            ILogger<DownloadManager> logger)
            :base(nameof(DownloadManager), processorCollection, logger)
        {
            base.DueTime = TimeSpan.FromSeconds(5);
            base.Period = TimeSpan.FromSeconds(5);
            this.mediaLibrary = mediaLibrary;
            this.environment = environment;
            this.mediaCollectionSelector = mediaCollectionSelector;
        }

        public bool HasJobs
        {
            get => !queue.IsEmpty || !queueCheck.IsEmpty;
        }

        public DownloadSession Enqueue(ClassifiedEntry entry, MediaItem item)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = item,
                CopyType = MediaItemCopyType.Cache
            };
            queueCheck.Enqueue(session);
            return session;
        }
        public DownloadSession Enqueue(ClassifiedEntry entry, MediaItemCopyType copyType)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = null,
                CopyType = copyType
            };
            queueCheck.Enqueue(session);
            return session;
        }

        protected override Task ExecuteTimerAsync()
        {
            Task.Run(Execute);
            return Task.CompletedTask;
        }
        private void Execute()
        {
            try
            {
                ExecuteChecks();
                ExecuteTimerDownloads();
                ExecuteDownloadRemovals();
            }
            catch(Exception ex) 
            {
                NotifyError(ex);
            }
        }
        private object _ExecutingLock = new object();
        private bool _ExecutingCheck = false;
        private bool _ExecutingDownloads = false;
        public bool Executing
        {
            get { return _ExecutingDownloads || _ExecutingCheck; }
        }
        private void ExecuteChecks()
        {
            lock (_ExecutingLock)
            {
                if (_ExecutingCheck) return;
                _ExecutingCheck = true;
            }
            try
            {
                if (!queueCheck.TryPeek(out var firstEntry))
                    return;
                Check(firstEntry);
                if (!queueCheck.TryDequeue(out var secondEntry))
                    return;
                if (secondEntry.Entry.Id != firstEntry.Entry.Id)
                    queueCheck.Enqueue(secondEntry);
            }
            finally
            {
                _ExecutingCheck = false;
            }
        }

        private void Check(DownloadSession session)
        {
            try
            {
                session.Start();
                CompleteSession(session);
                if (SplitSession(session))
                    return;
                if (session.Item is null)
                    throw new ApplicationException($"No media item found to download.");

                if (session.Item.CopyType == session.CopyType)
                    session.Finish();
                else if (session.Item.CopyType == MediaItemCopyType.Download)
                    session.Finish();
                else if (session.Item.CopyType != MediaItemCopyType.Original)
                {
                    var newPath = Path.Combine(environment.GetPath(session.CopyType), $"{Guid.NewGuid}{Path.GetExtension(session.Item.Name)}");
                    session.Item.CopyType = session.CopyType;
                    if (session.Item.Path != newPath)
                    {
                        File.Move(session.Item.Path, newPath);
                        session.Item.Path = newPath;
                    }
                    mediaLibrary.AddOrUpdateMediaItem(session.Item);
                    session.Finish();
                }
                else
                    queue.Enqueue(session);
            }
            catch (Exception ex)
            {
                session.Fail(ex);
            }
        }

        private DateTime _LastDownloadRemovalCheck = DateTime.MinValue;
        private TimeSpan _DownloadRemovalInternal = TimeSpan.FromMinutes(5);
        private void ExecuteDownloadRemovals()
        {
            lock (_ExecutingLock)
            {
                if (_ExecutingDownloads) return;
                _ExecutingDownloads = true;
            }
            try
            {
                if (_LastDownloadRemovalCheck.Add(_DownloadRemovalInternal) > DateTime.Now)
                    return;
                _LastDownloadRemovalCheck = DateTime.Now;
                foreach (var mediaItem in mediaLibrary.GetDueMediaItems())
                    if (RemoveDownload(mediaItem))
                    {
                        var elem = mediaLibrary.GetMovieByMediaItem(mediaItem.Id) as ClassifiedEntry
                            ?? mediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);
                        if (elem is null) continue;
                        var de = elem as IDownloadableEntry;
                        if (de is null) continue;
                        if (de.DownloadMediaItemId != mediaItem.Id)
                            continue;
                        de.DownloadMediaItemId = 0;
                        mediaLibrary.AddOrUpdateEntry(elem);
                    }
            }
            finally
            {
                _ExecutingDownloads = false;
            }
        }

        public void RemoveDownloads(ClassifiedEntry entry)
        {
            var collectionEntry = entry as IMediaItemCollectionEntry;
            var downlodable = entry as IDownloadableEntry;
            if (collectionEntry is null) return;
            foreach (var mediaItem in collectionEntry
                .MediaItemIds
                .ToArray()
                .Select(id =>
                {
                    var mi = mediaLibrary.GetMediaItem(id);
                    if (mi is null)
                        collectionEntry.MediaItemIds = collectionEntry.MediaItemIds.Where(i => i != id).ToArray();
                    return mi;
                })
                .Where(mi => mi is not null)
                .Where(mi => mi.CopyType == MediaItemCopyType.Download || mi.CopyType == MediaItemCopyType.Cache))
            {
                if (!RemoveDownload(mediaItem))
                    continue;
                collectionEntry.MediaItemIds = collectionEntry.MediaItemIds.Where(i => i != mediaItem.Id).ToArray();
                if (downlodable is not null)
                {
                    if (mediaItem.Id == downlodable.DownloadMediaItemId)
                    {
                        downlodable.DownloadMediaItemId = 0;
                        mediaLibrary.AddOrUpdateEntry(entry);
                    }
                }

            }
        }
        public void RemoveDownloads(MediaItem mediaItem)
        {
            if (mediaItem.CopyType == MediaItemCopyType.Download)
                RemoveDownload(mediaItem);
            else if (mediaItem.CopyType == MediaItemCopyType.Cache)
                RemoveDownload(mediaItem);
        }
        private bool RemoveDownload(MediaItem mediaItem)
        {
            if (mediaItem.CopyType == MediaItemCopyType.Original)
                throw new ArgumentException(mediaItem.CopyType.ToString());
            try
            {                
                var path = PathTools.Combine(environment.GetRootPath(), mediaItem.Path);
                if (File.Exists(path))
                    File.Delete(path);
                mediaLibrary.Delete(mediaItem);
                return true;
            }
            catch(Exception ex)
            {
                NotifyError(ex);
                mediaItem.DueDate = DateTime.Now.Add(mediaLibrary.Setup.DownloadManager_DueTime_Watched);
                mediaLibrary.AddOrUpdateMediaItem(mediaItem);
                return false;
            }
        }

        private void ExecuteTimerDownloads()
        {
            lock (_ExecutingLock)
            {
                if (_ExecutingDownloads) return;
                _ExecutingDownloads = true;
            }
            try
            {
                if (!queue.TryPeek(out var firstEntry))
                    return;
                Download(firstEntry);
                if (!queue.TryDequeue(out var secondEntry))
                    return;
                if (secondEntry.Entry.Id != firstEntry.Entry.Id)
                    queue.Enqueue(secondEntry);
            }
            finally
            {
                _ExecutingDownloads = false;
            }
        }

        private void Download(DownloadSession session)
        {
            try
            {   
                session.Item = Download(session.Item, session.CopyType, (p) => 
                { 
                    session.SetProgress(p);
                    NotifyStatus($"Lade {session.Item.Name} ({p} %)");
                });
                if (session.Entry is not null)
                {
                    session.Entry = mediaLibrary.GetClassifiedEntry(session.Entry.Id);
                    if (session.Entry is IMediaItemCollectionEntry)
                    {                        
                        ((IMediaItemCollectionEntry)session.Entry).MediaItemIds = ((IMediaItemCollectionEntry)session.Entry).MediaItemIds.Concat(new long[] { session.Item.Id }).Distinct().ToArray();
                    }
                    if (session.Entry is not null && session.Entry is IDownloadableEntry)
                    {
                        if (session.Item.CopyType == MediaItemCopyType.Download)
                            ((IDownloadableEntry)session.Entry).DownloadMediaItemId = session.Item.Id;
                        else if (((IDownloadableEntry)session.Entry).DownloadMediaItemId == 0)
                            ((IDownloadableEntry)session.Entry).DownloadMediaItemId = session.Item.Id;
                    }
                    session.Entry = mediaLibrary.AddOrUpdateEntry(session.Entry);
                }
                session.Finish();
            }
            catch(Exception ex)
            {
                session.Fail(ex);
            }
        }

        #region SplitSession
        private bool SplitSession(DownloadSession session)
        {
            if (session.Entry is null) return false;
            return SplitSession(session.Entry as TVShow, session.CopyType)
                || SplitSession(session.Entry as TVShowSeason, session.CopyType)
                || SplitSession(session.Entry as MovieCollection, session.CopyType);
        }
        private bool SplitSession(MovieCollection movieCollection, MediaItemCopyType copyType)
        {
            if (movieCollection is null) return false;
            foreach (var movie in mediaCollectionSelector.FindNextEntries(movieCollection))
                Enqueue(movie, copyType);
            return true;
        }

        private bool SplitSession(TVShowSeason tVShowSeason, MediaItemCopyType copyType)
        {
            if (tVShowSeason is null) return false;
            foreach (var episode in mediaCollectionSelector.FindNextEntries(tVShowSeason)
                .OfType<TVShowEpisode>()
                .TakeWhile(episode => episode.SeasonId == tVShowSeason.Id))
                Enqueue(episode, copyType);
            return true;
        }

        private bool SplitSession(TVShow tVShow, MediaItemCopyType copyType)
        {
            if (tVShow is null) return false;
            var season = mediaCollectionSelector.FindFirstSeason(tVShow);
            return SplitSession(season, copyType);
        }
        #endregion

        private MediaItem Download(MediaItem item, MediaItemCopyType copyType, Action<decimal> progressCallback)
        {
            if (item.CopyType == copyType)
                return item;
            if (item.CopyType == MediaItemCopyType.Download)
                return item;
            if (item.CopyType != MediaItemCopyType.Original)
            {
                var newPath = Path.Combine(environment.GetPath(copyType), $"{Guid.NewGuid}{Path.GetExtension(item.Name)}");
                item.CopyType = copyType;
                if (item.Path != newPath)
                {
                    File.Move(item.Path, newPath);
                    item.Path = newPath;
                }
                mediaLibrary.AddOrUpdateMediaItem(item);
                return item;
            }
            var duplicate = new MediaItem()
            {
                Classified = false,
                CopyType = copyType,
                LastAccess = item.LastAccess,
                Name = item.Name,
                OriginalMediaItemId = item.Id,
                ParentCollectionId = item.ParentCollectionId,
                Path = Path.Combine(environment.GetPath(copyType), $"{Guid.NewGuid()}-{item.Name}"),
                LastPosition = item.LastPosition
            };
            Download(item, duplicate, progressCallback);

            var setup = mediaLibrary.Setup;
            switch(copyType)
            {
                case MediaItemCopyType.Download:
                    duplicate.DueDate = DateTime.Now.Add(setup.DownloadManager_DueTime_Download);
                    break;
                case MediaItemCopyType.Cache:
                    duplicate.DueDate = DateTime.Now.Add(setup.DownloadManager_DueTime_Cache);
                    break;
            }            
            duplicate.Path = duplicate.Path.Remove(0, environment.GetRootPath().Length);
            return mediaLibrary.AddOrUpdateMediaItem(duplicate);
        }

        private void Download(MediaItem item, MediaItem destItem, Action<decimal> progressCallback)
        {
            var collection = mediaLibrary.GetMediaCollection(item.ParentCollectionId);
            var source = mediaLibrary.GetSource(collection.SourceId);
            var reader = CreateReader(source);
            var tempFile = reader.Download(item, progressCallback);
            tempFile.MoveTo(destItem.Path);            
        }
        
        private void CompleteSession(DownloadSession session)
        {
            if (session.Entry is null)
                session.Entry = FindEntry(session.Item);
            if (session.Item is null)
                session.Item = FindItem(session.Entry);            
        }

        private MediaItem FindItem(ClassifiedEntry entry)
        {
            if (entry is null) return null;
            var firstPlayableEntry = entry as IMediaItemCollectionEntry;
            if (firstPlayableEntry is null) return null;
            var mediaItems = firstPlayableEntry.MediaItemIds
                .Select(id => mediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Where(mi => mi.CopyType != MediaItemCopyType.Trailer)
                .ToArray();
            var downloadedMediaItem = mediaItems
                .FirstOrDefault(mi => mi.CopyType == MediaItemCopyType.Download);
            if (downloadedMediaItem is not null)
                return downloadedMediaItem;
            var cachedMediaItem = mediaItems
                .FirstOrDefault(mi => mi.CopyType == MediaItemCopyType.Cache);
            if (cachedMediaItem is not null)
                return cachedMediaItem;
            var originalItem = mediaItems
                .FirstOrDefault(mi => mi.CopyType == MediaItemCopyType.Original);
            if (originalItem is not null)
            {
                mediaItems = mediaLibrary
                    .GetCopyMediaItems(originalItem.Id)
                    .ToArray();
                downloadedMediaItem = mediaItems
                    .FirstOrDefault(mi => mi.CopyType == MediaItemCopyType.Download);
                if (downloadedMediaItem is not null)
                {
                    firstPlayableEntry.MediaItemIds = firstPlayableEntry.MediaItemIds.Concat(new long[] { downloadedMediaItem.Id }).ToArray();
                    mediaLibrary.AddOrUpdateEntry(entry);
                    return downloadedMediaItem;
                }
                cachedMediaItem = mediaItems
                    .FirstOrDefault(mi => mi.CopyType == MediaItemCopyType.Cache);
                if (cachedMediaItem is not null)
                {
                    firstPlayableEntry.MediaItemIds = firstPlayableEntry.MediaItemIds.Concat(new long[] { cachedMediaItem.Id }).ToArray();
                    mediaLibrary.AddOrUpdateEntry(entry);
                    return cachedMediaItem;
                }
            }
            return originalItem;
        }

        private ClassifiedEntry FindEntry(MediaItem item)
        {
            if (item is null) return null;
            var movie = mediaLibrary.GetMovieByMediaItem(item.Id);
            if (movie is not null) return movie;
            return null;
        }

        public void ClearTempFolder()
        {
            var localFilePath = Path.GetTempFileName();
            File.Delete(localFilePath);
            var folderPath = Path.GetDirectoryName(localFilePath);
            foreach (var file in Directory.GetFiles(folderPath))
                File.Delete(file);
        }

        public IEnumerable<FileInfo> GetOrphanedFiles()
        {
            var rootPath = environment.GetRootPath();
            var copyTypes = new MediaItemCopyType[] { MediaItemCopyType.Cache, MediaItemCopyType.Download };
            return copyTypes
                .Select(ct => environment.GetPath(ct))
                .SelectMany(path => Directory.GetFiles(path))
                .Select(file => new FileInfo(file))
                .Where(file =>
                {
                    var relPath = file.FullName.Remove(0, rootPath.Length);
                    var mediaItem = mediaLibrary.GetMediaItemByPath(relPath);
                    mediaLibrary.Release(mediaItem);
                    return mediaItem is null;
                });
        }
    }
}
