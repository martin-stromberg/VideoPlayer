using Microsoft.Extensions.Logging;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;

namespace VideoPlayer.Service.Download
{
    public abstract class BaseDownloadService : SourceTimerService
    {
        private object _ExecutingLock = new object();
        private bool _Executing = false;

        public BaseDownloadService(IEnvironment environment, IApplicationSettings applicationSettings, IMediaLibrary mediaLibrary, IMediaCollectionSelector mediaCollectionSelector, IProcessorCollection processorCollection, ILogger logger) 
            : base(string.Empty, processorCollection, logger)
        {
            this.environment = environment;
            this.applicationSettings = applicationSettings;
            this.mediaLibrary = mediaLibrary;
            this.mediaCollectionSelector = mediaCollectionSelector;
        }

        protected readonly IMediaLibrary mediaLibrary;
        protected readonly IMediaCollectionSelector mediaCollectionSelector;
        protected readonly IApplicationSettings applicationSettings;
        protected readonly IEnvironment environment;

        protected override Task ExecuteTimerAsync()
        {
            Task.Run(InternalExecute);
            return Task.CompletedTask;
        }
        private void InternalExecute()
        {
            lock (_ExecutingLock)
            {
                if (_Executing) return;
                _Executing = true;
            }
            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
            finally
            {
                _Executing = false;
            }
        }
        public virtual bool Executing
        {
            get { return _Executing; }
        }
        protected abstract void Execute();

        protected MediaItem FindItem(ClassifiedEntry entry)
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
        protected ClassifiedEntry FindEntry(MediaItem item)
        {
            if (item is null) return null;
            var movie = mediaLibrary.GetMovieByMediaItem(item.Id);
            if (movie is not null) return movie;
            var episode = mediaLibrary.GetTVShowEpisodeByMediaItem(item.Id);
            if (episode is not null) return episode;
            return null;
        }
        protected void SetDue(MediaItem mediaItem, MediaItemCopyType copyType, TimeSpan dueTime)
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

        protected bool IsInDownloadQueue(MediaItem mediaItem)
        {
            MediaItemEventArgs mediaItemEventArgs = new MediaItemEventArgs(mediaItem)
            {
                Allowed = true
            };
            CheckBeforeRemove(this, mediaItemEventArgs);
            return !mediaItemEventArgs.Allowed;
        }
        public event EventHandler<MediaItemEventArgs> CheckBeforeRemove;
    }
}
