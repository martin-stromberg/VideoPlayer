using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;
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
        private readonly IApplicationSettings applicationSettings;

        public DownloadManager(
            IMediaLibrary mediaLibrary,
            IEnvironment environment,
            IMediaCollectionSelector mediaCollectionSelector,
            IProcessorCollection processorCollection,
            IApplicationSettings applicationSettings,
            ILogger<DownloadManager> logger)
            :base(nameof(DownloadManager), processorCollection, logger)
        {
            base.DueTime = TimeSpan.FromSeconds(5);
            base.Period = TimeSpan.FromSeconds(5);
            this.mediaLibrary = mediaLibrary;
            this.environment = environment;
            this.mediaCollectionSelector = mediaCollectionSelector;
            this.applicationSettings = applicationSettings;
        }

        public bool HasJobs
        {
            get => !queue.IsEmpty || !queueCheck.IsEmpty;
        }

        public DownloadSession Enqueue(ClassifiedEntry entry, MediaItem item, TimeSpan dueTime)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = item,
                CopyType = MediaItemCopyType.Cache,
                DueTime = dueTime,
            };
            queueCheck.Enqueue(session);
            return session;
        }
        public DownloadSession Enqueue(ClassifiedEntry entry, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = null,
                CopyType = copyType,
                DueTime = dueTime
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
                if (firstEntry.Waiting)
                {
                    if (!queueCheck.TryDequeue(out var secondFirstEntry))
                        return;
                    queueCheck.Enqueue(secondFirstEntry);
                    return;
                }
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
                {
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    session.Finish();
                }
                else if (session.Item.CopyType == MediaItemCopyType.Download)
                {
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    session.Finish();
                }
                else if (session.Item.CopyType != MediaItemCopyType.Original)
                {
                    var oldPath = Path.Combine(environment.GetPath(session.Item.CopyType), $"{session.Item.Path}");
                    var newPath = Path.Combine(environment.GetPath(session.CopyType), $"{Guid.NewGuid}{Path.GetExtension(session.Item.Name)}");
                    session.Item.CopyType = session.CopyType;
                    if (session.Item.Path != newPath)
                    {
                        File.Move(session.Item.Path, newPath);
                        session.Item.Path = newPath;
                    }
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    mediaLibrary.AddOrUpdateMediaItem(session.Item);
                    session.Finish();
                }
                else
                {
                    var existingSession = FindExistingCheckSession(session);
                    if (existingSession is not null)
                    {
                        if (existingSession.DueTime != TimeSpan.Zero)
                            session.DueTime = existingSession.DueTime;
                    }
                    existingSession = FindExistingDownloadSession(session);
                    if (existingSession is not null)
                    {
                        if (existingSession.DueTime != TimeSpan.Zero)
                            session.DueTime = existingSession.DueTime;
                        session.Assign(existingSession);
                        queueCheck.Enqueue(session);
                    }
                    else if (!IsSessionAlreadyQueued(session))
                        queue.Enqueue(session);
                }
            }
            catch (Exception ex)
            {
                session.Fail(ex);
            }
        }

        private bool IsSessionAlreadyQueued(DownloadSession session)
        {
            return queue.Any(s => s.SessionId == session.SessionId);
        }

        private DownloadSession FindExistingCheckSession(DownloadSession session)
        {
            var existing = queueCheck.FirstOrDefault(s => s.SessionId != session.SessionId 
                && s.Item is not null 
                && session.Item is not null 
                && s.Item.Id == session.Item.Id);
            if (existing is null)
                existing = queueCheck.FirstOrDefault(s => s.SessionId != session.SessionId 
                    && s.Entry is not null 
                    && session.Entry is not null 
                    && s.Entry.Id == session.Entry.Id);
            return existing;
        }
        private DownloadSession FindExistingDownloadSession(DownloadSession session)
        {
            var existing = queue.FirstOrDefault(s => s.SessionId != session.SessionId 
                && s.Item is not null 
                && session.Item is not null 
                && s.Item.Id == session.Item.Id);
            if (existing is null)
                existing = queue.FirstOrDefault(s => s.SessionId != session.SessionId 
                    && s.Entry is not null 
                    && session.Entry is not null 
                    && s.Entry.Id == session.Entry.Id);
            return existing;
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
                        mediaLibrary.AddProtocol(elem, $"Remove Download (MediaItem {mediaItem.Id} - {mediaItem.CopyType} - {mediaItem.DueDate})");
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
                mediaLibrary.AddProtocol(entry, $"Remove Download (MediaItem {mediaItem.Id} - {mediaItem.CopyType} - {mediaItem.DueDate})");
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
                mediaItem.DueDate = DateTime.Now.Add(applicationSettings.DownloadDueTimeWatched);
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
                session.Item = Download(session.Item, session.CopyType, session.DueTime, (p) => 
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
                    mediaLibrary.AddProtocol(session.Entry, $"Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                }
                session.Finish();
            }
            catch(FileDeletedException ex)
            {
                RemoveMediaItemWithRescan(session.Item);
                session.Item = null;
                session.Reset();
                queueCheck.Enqueue(session);
            }
            catch(Exception ex)
            {
                session.Fail(ex);
            }
        }

        private void RemoveMediaItemWithRescan(MediaItem item)
        {
            if (item.CopyType != MediaItemCopyType.Original)
                return;
            var collection = mediaLibrary.GetMediaCollection(item.ParentCollectionId);
            try
            {
                mediaLibrary.Delete(item);
                collection.LastAccess = DateTime.MinValue;
                mediaLibrary.AddOrUpdateMediaCollection(collection);
                Notify(this, new Events.NotificationEventArgs("Scan", null));
            }
            finally
            {
                mediaLibrary.Release(collection);
            }
        }

        #region SplitSession
        private bool SplitSession(DownloadSession session)
        {
            if (session.Entry is null) return false;
            return SplitSession(session.Entry as TVShow, session.CopyType, session.DueTime)
                || SplitSession(session.Entry as TVShowSeason, session.CopyType, session.DueTime)
                || SplitSession(session.Entry as MovieCollection, session.CopyType, session.DueTime);
        }
        private bool SplitSession(MovieCollection movieCollection, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (movieCollection is null) return false;
            foreach (var movie in mediaCollectionSelector.FindNextEntries(movieCollection))
                Enqueue(movie, copyType, dueTime);
            return true;
        }

        private bool SplitSession(TVShowSeason tVShowSeason, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (tVShowSeason is null) return false;
            foreach (var episode in mediaCollectionSelector.FindNextEntries(tVShowSeason)
                .OfType<TVShowEpisode>()
                .TakeWhile(episode => episode.SeasonId == tVShowSeason.Id))
                Enqueue(episode, copyType, dueTime);
            return true;
        }

        private bool SplitSession(TVShow tVShow, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (tVShow is null) return false;
            var season = mediaCollectionSelector.FindFirstSeason(tVShow);
            return SplitSession(season, copyType, dueTime);
        }
        #endregion

        private MediaItem Download(MediaItem item, MediaItemCopyType copyType, TimeSpan dueTime, Action<decimal> progressCallback)
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
            SetDue(duplicate, copyType, dueTime);
            duplicate.Path = duplicate.Path.Remove(0, environment.GetRootPath().Length);
            return mediaLibrary.AddOrUpdateMediaItem(duplicate);
        }

        private void SetDue(MediaItem mediaItem, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            var newDueDate = (dueTime != TimeSpan.Zero)
                ? DateTime.Now.Add(dueTime)
                : copyType switch
                {
                    MediaItemCopyType.Download => DateTime.Now.Add(applicationSettings.DownloadDueTimeDownload),
                    MediaItemCopyType.Cache => DateTime.Now.Add(applicationSettings.DownloadDueTimeCache),
                    _ => DateTime.Now.Add(applicationSettings.DownloadDueTimeCache)
                };
            if (mediaItem.DueDate < newDueDate)
                mediaItem.DueDate = newDueDate;
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
            {
                session.Entry = FindEntry(session.Item);
                mediaLibrary.Hold(session.Entry);
            }
            if (session.Item is null)
            {
                session.Item = FindItem(session.Entry);
                mediaLibrary.Hold(session.Item);
            }
            if (session.Item is null)
                return;            
            if (session.Entry is not null && session.Item.CopyType == MediaItemCopyType.Original)
            {
                var existingDownloadItem = FindItem(session.Entry);
                if (existingDownloadItem is not null)
                {
                    mediaLibrary.Release(session.Item);
                    session.Item = existingDownloadItem;
                }
            }
            if (session.Item is null)
                return;
        }

        private MediaItem FindItem(ClassifiedEntry entry)
        {
            if (entry is null) return null;
            var firstPlayableEntry = entry as IMediaItemCollectionEntry;
            if (firstPlayableEntry is null) return null;
            var mediaItems = firstPlayableEntry.MediaItemIds
                .Select(id => mediaLibrary.GetMediaItem(id))
                .Select(mi => { mediaLibrary.Release(mi); return mi; })
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
            var episode = mediaLibrary.GetTVShowEpisodeByMediaItem(item.Id);
            if (episode is not null) return episode;
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

        public void PrepareWatchedMediaItem(ClassifiedEntry entry, MediaItem item)
        {
            if (item.CopyType == MediaItemCopyType.Original)
                return;
            var newDueDate = item.CopyType switch
            {
                MediaItemCopyType.Download => DateTime.Now.Add(applicationSettings.DownloadDueTimeWatched),
                MediaItemCopyType.Cache => DateTime.Now.Add(applicationSettings.DownloadDueTimeWatched),
                _ => DateTime.Now.Add(applicationSettings.DownloadDueTimeWatched)
            };
            if (item.DueDate <= newDueDate)
                return;
            item.DueDate = newDueDate;
            mediaLibrary.AddOrUpdateMediaItem(item);
            mediaLibrary.AddProtocol(entry, $"Watched (MediaItem {item.Id} - {item.CopyType} - {item.DueDate})");
        }
    }
}
