using FluentFTP;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using VideoPlayer.Extensions;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public class MediaDownloader: IMediaDownloader
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly MediaLibraryEnvironment _Settings;
        private readonly IJobDatabase _JobDataSource;

        public MediaDownloader(IMediaLibrary mediaLibrary, MediaLibraryEnvironment settings, IJobDatabase jobDataSource)
        {
            _JobDataSource = jobDataSource;
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
        }

        private async Task<MediaItem> FindLocalItemAsync(MediaItem item, MediaItemCopyType copyType)
        {
            if ((item.CopyType == MediaItemCopyType.Cache) || (item.CopyType == MediaItemCopyType.Download))
                return item;
            var alternateMediaItem = (await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id))
                .FirstOrDefault(foundItem =>
                                (foundItem.CopyType == copyType) && (foundItem.OriginalMediaItemId == item.Id));
            while ((alternateMediaItem != null) && !File.Exists(alternateMediaItem.Path))
            {
                await _MediaLibrary.RemoveMediaItemAsync(alternateMediaItem);
                alternateMediaItem = (await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id))
                    .FirstOrDefault(foundItem =>
                                    (foundItem.CopyType == copyType) && (foundItem.OriginalMediaItemId == item.Id));
            }
            return alternateMediaItem;
        }

        public async Task<MediaItem> CacheAsync(MediaItem item)
        {
            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);
            var alternateMediaItem = await FindLocalItemAsync(item, MediaItemCopyType.Cache);
            if (alternateMediaItem is null)
                alternateMediaItem = await FindLocalItemAsync(item, MediaItemCopyType.Download);
            if (alternateMediaItem is not null)
                return alternateMediaItem;
            if (source is SmbMediaSource)
                return DownloadSmbMediaItem(source as SmbMediaSource, collection, item);
            else if (source is FtpMediaSource)
                return await DownloadFtpMediaItemAsync(source as FtpMediaSource,
                                                       collection,
                                                       item,
                                                       MediaItemCopyType.Cache);
            else
                return null;
        }

        private async Task<MediaItem> DownloadFtpMediaItemAsync(
            FtpMediaSource source,
            MediaItemCollection collection,
            MediaItem mediaItem,
            MediaItemCopyType copyType)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = copyType;
            alternateMediaItem.Path = Path.Combine(_Settings.DownloadFolderPath,
                                                   collection.Id.ToString(),
                                                   mediaItem.Name);
            using (FtpClient client = new FtpClient(source.ServerName, new NetworkCredential(source.Username, source.Password)))
                try
                {
                    client.Connect();
                    try
                    {
                        client.DownloadFile(alternateMediaItem.Path,
                                            mediaItem.Path,
                                            FtpLocalExists.Overwrite,
                                            FtpVerify.Throw);
                    }
                    finally
                    {
                        client.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            if (!File.Exists(alternateMediaItem.Path))
                return null;
            await _MediaLibrary.AddMediaItemAsync(alternateMediaItem);
            return alternateMediaItem;
        }

        private MediaItem DownloadSmbMediaItem(
            SmbMediaSource source,
            MediaItemCollection collection,
            MediaItem mediaItem)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Cache;
            alternateMediaItem.Path = Path.Combine(_Settings.CacheFolderPath, collection.Id.ToString(), mediaItem.Name);

            // SambaShare sambaShare = new SambaShare(source.ServerName, source.Username, source.Password);
            // try
            // {
            // sambaShare.Connect();
            // try
            // {
            // sambaShare.DownloadFile(mediaItem.Path, alternateMediaItem.Path);
            // }
            // finally
            // {
            // sambaShare.Disconnect();
            // }
            // }
            // catch (Exception ex)
            // {
            // Debug.WriteLine(ex);
            // }

            // if (!File.Exists(alternateMediaItem.Path))
            // return null;

            // mediaLibrary.AddMediaItemAsync(alternateMediaItem).Wait();
            // return alternateMediaItem;
            throw new NotImplementedException();
        }

        public async void StartDownload(BaseModel item)
        {
            await StartDownloadAsync(item as MediaItem);
            await StartDownloadAsync(item as TVShow);
            await StartDownloadAsync(item as TVShowSeason);
            await StartDownloadAsync(item as TVShowEpisode);
            await StartDownloadAsync(item as MovieCollection);
            await StartDownloadAsync(item as Movie);
        }

        public async Task StartDownloadAsync(MediaItem item)
        {
            if (item == null)
                return;
            if (item.CopyType != MediaItemCopyType.None)
                item = await _MediaLibrary.GetMediaItemAsync(item.OriginalMediaItemId);

            var mediaItems = (await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id)).ToList();
            var downloadItem = mediaItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Download);
            while ((downloadItem != null) && !File.Exists(downloadItem.Path))
            {
                await _MediaLibrary.RemoveMediaItemAsync(downloadItem);
                mediaItems.Remove(downloadItem);
                downloadItem = mediaItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Download);
            }
            if (downloadItem is not null)
                return;

            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);

            Database.Models.DownloadJob job = new Database.Models.DownloadJob()
            {
                MediaItemId = item.Id,
                EntryTime = DateTime.Now,
                SourceId = source.Id
            };
            await _JobDataSource.AddDownloadJob(job);
            StartWorker();
        }

        public async Task StartDownloadAsync(TVShow item)
        {
            if (item == null)
                return;
            var seasons = await _MediaLibrary.GetTVShowSeasons(item.Id);
            foreach (var season in seasons)
                await StartDownloadAsync(season);
        }

        public async Task StartDownloadAsync(TVShowSeason item)
        {
            if (item == null)
                return;
            var episodes = await _MediaLibrary.GetTVShowEpisodes(item.Id);
            foreach (var episode in episodes)
                await StartDownloadAsync(episode);
        }

        public async Task StartDownloadAsync(TVShowEpisode item)
        {
            if (item == null)
                return;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                {
                    await StartDownloadAsync(mediaItem);
                    break;
                }
            }
        }

        public async Task StartDownloadAsync(MovieCollection item)
        {
            if (item == null)
                return;
            var movies = await _MediaLibrary.GetMovies(item.Id);
            foreach (var movie in movies)
                await StartDownloadAsync(movie);
        }

        public async Task StartDownloadAsync(Movie item)
        {
            if (item == null)
                return;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                {
                    StartDownloadAsync(mediaItem);
                    break;
                }
            }
        }

        private BackgroundWorker _Worker = null;

        private void StartWorker()
        {
            if (_Worker != null)
                return;
            _Worker = new BackgroundWorker();
            _Worker.DoWork += _Worker_DoWork;
            _Worker.RunWorkerCompleted += _Worker_RunWorkerCompleted;
            _Worker.RunWorkerAsync();
        }

        private async void _Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            await Task.Delay(1000);
            _Worker.RunWorkerAsync();
        }

        private void _Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var job = _JobDataSource.GetDownloadJobs()
                                        .Wait<IEnumerable<Database.Models.DownloadJob>>()
                                        .FirstOrDefault();
                if (job == null)
                    return;
                var mediaItem = _MediaLibrary.GetMediaItemAsync(job.MediaItemId).Wait<MediaItem>();
                var mediaItems = _MediaLibrary.GetAlternateMediaItemsAsync(mediaItem.Id).Wait<IEnumerable<MediaItem>>();
                var downloadItem = mediaItems.FirstOrDefault(i =>
                                                             (i.CopyType == MediaItemCopyType.Download)
                    && Path.Exists(i.Path));
                if (downloadItem == null)
                {
                    var collection = _MediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId)
                                                  .Wait<MediaItemCollection>();
                    var source = _MediaLibrary.GetSourceAsync(collection.MediaSourceId).Wait<MediaSource>();

                    if (source is SmbMediaSource)
                        DownloadSmbMediaItem(source as SmbMediaSource, collection, mediaItem);
                    else if (source is FtpMediaSource)
                        DownloadFtpMediaItemAsync(source as FtpMediaSource,
                                                  collection,
                                                  mediaItem,
                                                  MediaItemCopyType.Download)
                        .Wait();
                }
                _JobDataSource.RemoveDownloadJob(job).Wait();
            }
            catch { }
        }

        public async Task ContinueDownloadsAsync()
        {
            if (await _JobDataSource.DownloadJobsExist())
                StartWorker();
        }

    }
}
