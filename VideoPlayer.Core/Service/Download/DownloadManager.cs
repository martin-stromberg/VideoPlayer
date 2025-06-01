using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Layouts;
using Renci.SshNet;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;
using VideoPlayer.Tools;
using static SQLite.SQLite3;

namespace VideoPlayer.Service.Download
{
    public class DownloadManager : BaseDownloadService, IDownloadManager
    {   
        private ConcurrentQueue<DownloadSession> queue = new ConcurrentQueue<DownloadSession>();
        private IPlaylistManager playlistManager;

        private DownloadChecker _Checker;
        private DownloadRemovalManager _Remover;

        public DownloadManager(
            IMediaLibrary mediaLibrary,
            IEnvironment environment,
            IMediaCollectionSelector mediaCollectionSelector,
            IProcessorCollection processorCollection,
            IApplicationSettings applicationSettings,
            IServiceProvider serviceProvider,
            ILogger<DownloadManager> logger)
            :base(environment, applicationSettings, mediaLibrary, mediaCollectionSelector, processorCollection, logger)
        {
            _Checker = new DownloadChecker(environment, applicationSettings, mediaLibrary, mediaCollectionSelector, processorCollection, logger);
            _Checker.CheckSessionExists += _Checker_CheckSessionExists;
            _Checker.Download += _Checker_Download;
            _Remover = new DownloadRemovalManager(serviceProvider, environment, applicationSettings, mediaLibrary, mediaCollectionSelector, processorCollection, logger);
            _Remover.CheckBeforeRemove += _Remover_CheckBeforeRemove;

            base.DueTime = TimeSpan.FromSeconds(5);
            base.Period = TimeSpan.FromSeconds(5);
        }

        private void _Checker_Download(object sender, DownloadSessionEventArgs e)
        {
            queue.Enqueue(e.Session);
        }

        private void _Checker_CheckSessionExists(object sender, DownloadSessionEventArgs e)
        {
            var existing = queue.FirstOrDefault(s => s.SessionId != e.Session.SessionId
                && s.Item is not null
                && e.Session.Item is not null
                && s.Item.Id == e.Session.Item.Id);
            if (existing is null)
                existing = queue.FirstOrDefault(s => s.SessionId != e.Session.SessionId
                && s.Entry is not null
                    && e.Session.Entry is not null
                    && s.Entry.Id == e.Session.Entry.Id);
            e.Session = existing;
        }

        public bool IsInQueue(MediaItem mediaItem)
        {
            return queue.Any(e => e.Item?.Id == mediaItem.Id);
        }
        private void _Remover_CheckBeforeRemove(object sender, MediaItemEventArgs e)
        {
            e.Allowed = !IsInQueue(e.MediaItem) && !_Checker.IsInQueue(e.MediaItem);
        }

        public override void Start()
        {
            base.Start();
            _Checker.Start();
            _Remover.Start();
        }
        public override void Stop()
        {
            base.Stop();
            _Checker.Stop();
            _Remover.Stop();
        }


        public bool HasJobs
        {
            get => !queue.IsEmpty || _Checker.HasJobs;
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
            _Checker.Add(session);
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
            _Checker.Add(session);
            return session;
        }

        protected override void Execute()
        {
            if (!queue.TryPeek(out var firstEntry))
                return;
            Download(firstEntry);
            if (!queue.TryDequeue(out var secondEntry))
                return;
            if (secondEntry.Entry.Id != firstEntry.Entry.Id)
                queue.Enqueue(secondEntry);
        }
        public override bool Executing => base.Executing || _Checker.Executing;
        
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
                session.TryCount += 1;
                if (session.TryCount < 5)
                {
                    session.Reset();
                    _Checker.Add(session);
                }
                else
                    session.Fail(ex);
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

        private void Download(MediaItem item, MediaItem destItem, Action<decimal> progressCallback)
        {
            var collection = mediaLibrary.GetMediaCollection(item.ParentCollectionId);
            var source = mediaLibrary.GetSource(collection.SourceId);
            var reader = CreateReader(source);
            var tempFile = reader.Download(item, progressCallback);
            tempFile.MoveTo(destItem.Path);
            progressCallback(100);
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

        public void RemoveDownloads(ClassifiedEntry entry)
        {
            _Remover.RemoveDownloads(entry);
        }
    }
}
