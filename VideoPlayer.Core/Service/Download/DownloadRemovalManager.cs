using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;
using VideoPlayer.Tools;

namespace VideoPlayer.Service.Download
{
    public class DownloadRemovalManager : BaseDownloadService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IPlaylistManager playlistManager;
        private DateTime _LastDownloadRemovalCheck = DateTime.MinValue;
        private TimeSpan _DownloadRemovalInternal = TimeSpan.FromMinutes(5);
        public DownloadRemovalManager(IServiceProvider serviceProvider, IEnvironment environment, IApplicationSettings applicationSettings, IMediaLibrary mediaLibrary, IMediaCollectionSelector mediaCollectionSelector, IProcessorCollection processorCollection, ILogger logger) 
            : base(environment, applicationSettings, mediaLibrary, mediaCollectionSelector, processorCollection, logger)
        {
            this.serviceProvider = serviceProvider;
        }
        protected IPlaylistManager PlaylistManager
        {
            get { return playlistManager ?? serviceProvider.GetService<IPlaylistManager>(); }
        }

        protected override void Execute()
        {
            if (_LastDownloadRemovalCheck.Add(_DownloadRemovalInternal) > DateTime.Now)
                return;
            _LastDownloadRemovalCheck = DateTime.Now;
            PlaylistManager.CheckAndUpdateDueTimes();
            foreach (var mediaItem in mediaLibrary.GetDueMediaItems())
                if (!IsInDownloadQueue(mediaItem))
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

        #region Remove Downloads
        public void RemoveDownloads(MediaItem mediaItem)
        {
            if (mediaItem.CopyType == MediaItemCopyType.Download)
                RemoveDownload(mediaItem);
            else if (mediaItem.CopyType == MediaItemCopyType.Cache)
                RemoveDownload(mediaItem);
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
            catch (Exception ex)
            {
                NotifyError(ex);
                mediaItem.DueDate = DateTime.Now.Add(applicationSettings.DownloadDueTimeWatched);
                mediaLibrary.AddOrUpdateMediaItem(mediaItem);
                return false;
            }
        }
        #endregion
    }
}
