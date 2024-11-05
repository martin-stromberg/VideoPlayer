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
using VideoPlayer.Tools;
using static SQLite.SQLite3;

namespace VideoPlayer.Service.Download 
{
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception error)
            :base()
        {
            Error = error;
        }

        public Exception Error { get; }
    }
    public class DownloadManager : SourceTimerService, IDownloadManager
    {
        public enum DownloadStatus { Waiting, Downloading, Finished, Failed }
        public class DownloadSession
        {
            public MediaItem Item { get; internal set; }
            public ClassifiedEntry Entry { get; internal set; }
            public decimal DownloadProgress { get; private set; }
            public MediaItemCopyType CopyType { get; internal set; }
            private DownloadStatus _Status = DownloadStatus.Waiting;
            public DownloadStatus Status { get => _Status; 
                private set
                {
                    _Status = value;
                    switch (value)
                    {
                        case DownloadStatus.Downloading:
                            Starting?.Invoke(this, new DownloadEventArgs(Entry)
                            {
                                Session = this
                            });
                            break;
                        case DownloadStatus.Finished:
                            Finished?.Invoke(this, new DownloadEventArgs(Entry)
                            {
                                Session = this
                            });
                            break;
                    }
                }
            }
            public void Reset()
            {
                Status = DownloadStatus.Waiting;
            }
            public void Start()
            {
                Status = DownloadStatus.Downloading;
            }
            public void Finish()
            {
                Status = DownloadStatus.Finished;
            }
            public void Fail(Exception error)
            {
                Status = DownloadStatus.Failed;
                Failed?.Invoke(this, new DownloadFailedEventArgs(Entry, error)
                {
                    Session = this
                });
            }

            public event EventHandler<DownloadEventArgs> Starting;
            public event EventHandler<DownloadEventArgs> Finished;
            public event EventHandler<DownloadFailedEventArgs> Failed;
            public event EventHandler<ProgressEventArgs> Progress;

            private ProgressEventArgs _progressInfo = null;
            internal void SetProgress(decimal progress)
            {
                if (Status != DownloadStatus.Downloading)
                    return;
                DownloadProgress = progress;
                _progressInfo = _progressInfo ??= new ProgressEventArgs(progress);
                _progressInfo.Progress = progress;
                Progress?.Invoke(this, _progressInfo);
            }
        }
        private ConcurrentQueue<DownloadSession> queue = new ConcurrentQueue<DownloadSession>();
        private readonly IMediaLibrary mediaLibrary;
        private readonly IEnvironment environment;

        public DownloadManager(
            IMediaLibrary mediaLibrary,
            IEnvironment environment)
            :base()
        {
            base.DueTime = TimeSpan.FromSeconds(5);
            base.Period = TimeSpan.FromSeconds(5);
            this.mediaLibrary = mediaLibrary;
            this.environment = environment;
        }

        public bool HasJobs
        {
            get => !queue.IsEmpty;
        }

        public DownloadSession Enqueue(ClassifiedEntry entry, MediaItem item)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = item,
                CopyType = MediaItemCopyType.Cache
            };
            queue.Enqueue(session);
            return session;
        }

        protected override async Task ExecuteTimerAsync()
        {
            await ExecuteTimerDownloads();
            await ExecuteDownloadRemovals();
        }

        private Task ExecuteDownloadRemovals()
        {
            foreach (var mediaItem in mediaLibrary.GetDueMediaItems())
                if (RemoveDownload(mediaItem))
                {                    
                    var elem = mediaLibrary.GetMovieByMediaItem(mediaItem.Id)as ClassifiedEntry 
                        ?? mediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);
                    if (elem is null) continue;
                    var de = elem as IDownloadableEntry;
                    if (de is null) continue;
                    if (de.DownloadMediaItemId != mediaItem.Id)
                        continue;
                    de.DownloadMediaItemId = 0;
                    mediaLibrary.AddOrUpdateEntry(elem);
                }
            return Task.CompletedTask;
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
        private bool RemoveDownload(MediaItem mediaItem)
        {
            if (mediaItem.CopyType == MediaItemCopyType.Original)
                throw new ArgumentException(mediaItem.CopyType.ToString());
            try
            {                
                var path = Path.Combine(environment.GetPath(mediaItem.CopyType), mediaItem.Name);
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

        private Task ExecuteTimerDownloads()
        {
            if (!queue.TryPeek(out var firstEntry))
                return Task.CompletedTask;
            Download(firstEntry);
            if (!queue.TryDequeue(out var secondEntry))
                return Task.CompletedTask;
            if (secondEntry.Entry.Id != firstEntry.Entry.Id)
                queue.Enqueue(secondEntry);
            return Task.CompletedTask;
        }

        private void Download(DownloadSession session)
        {
            try
            {
                session.Start();
                CompleteSession(session);
                if (session.Item is null)
                    throw new ApplicationException($"No media item found to download.");
                session.Item = Download(session.Item, session.CopyType, (p) => { session.SetProgress(p); });
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
                Path = Path.Combine(environment.GetPath(copyType), $"{Guid.NewGuid()}-{item.Name}")
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
    }
}
