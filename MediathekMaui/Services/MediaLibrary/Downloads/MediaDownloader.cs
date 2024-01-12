using FluentFTP;
using Mediathek.Extensions;
using Mediathek.Services.Database;
using Mediathek.Services.MediaLibrary.Scanner.Http;
using Mediathek.StatusManagement;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace Mediathek.Services.MediaLibrary.Downloads
{
    public class MediaDownloader: IMediaDownloader
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly MediaLibraryEnvironment _Settings;
        private readonly IJobDatabase _JobDataSource;
        private readonly IStatusPublisher _StatusPublisher;

        public MediaDownloader(
            IMediaLibrary mediaLibrary,
            MediaLibraryEnvironment settings,
            IJobDatabase jobDataSource,
            IStatusPublisher statusPublisher)
        {
            _JobDataSource = jobDataSource;
            _StatusPublisher = statusPublisher;
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
        }

        public event EventHandler<BaseModelEventArgs> Downloaded;

        public event EventHandler<BaseModelEventArgs> DownloadDeleted;

        private void OnDownloaded(BaseModelEventArgs e)
        {
            Downloaded?.Invoke(this, e);
        }

        private void OnDownloadDeleted(BaseModelEventArgs e)
        {
            DownloadDeleted?.Invoke(this, e);
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
            else if (source is HttpMediaSource)
                return await DownloadHttpMediaItemAsync(source as HttpMediaSource,
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
            alternateMediaItem.Path = Path.Combine(_Settings.GetPath(copyType),
                                                   collection.Id.ToString(),
                                                   mediaItem.Name);
            var config = new FtpConfig();
            var logger = new FtpLogger();
            logger.NewEntry += (sender, e) =>
            {
                if (e.Exception != null)
                    Debug.WriteLine($"{e.Exception}");
                else
                    Debug.WriteLine(e.Message);
            };
            using (FtpClient client = new FtpClient(source.ServerName, new NetworkCredential(source.Username, source.Password)))
                try
                {
                    client.ValidateCertificate += (sender, e) => { e.Accept = true; };
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
            OnDownloaded(new BaseModelEventArgs(mediaItem));
            return alternateMediaItem;
        }

        private async Task<MediaItem> DownloadHttpMediaItemAsync(
            HttpMediaSource source,
            MediaItemCollection collection,
            MediaItem mediaItem,
            MediaItemCopyType copyType)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = copyType;
            alternateMediaItem.Path = Path.Combine(_Settings.GetPath(copyType),
                                                   collection.Id.ToString(),
                                                   mediaItem.Name);
            HttpShare client = new HttpShare(source.Uri);
            try
            {
                client.DownloadFile(mediaItem.Path, alternateMediaItem.Path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            if (!File.Exists(alternateMediaItem.Path))
                return null;
            await _MediaLibrary.AddMediaItemAsync(alternateMediaItem);
            OnDownloaded(new BaseModelEventArgs(mediaItem));
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

        public async Task<IEnumerable<DownloadSession>> StartDownload(BaseModel item)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            jobs.Add(await StartDownloadAsync(item as MediaItem));
            jobs.Add(await StartDownloadAsync(item as TVShowEpisode));
            jobs.Add(await StartDownloadAsync(item as Movie));
            jobs.AddRange(await StartDownloadAsync(item as TVShow));
            jobs.AddRange(await StartDownloadAsync(item as TVShowSeason));
            jobs.AddRange(await StartDownloadAsync(item as MovieCollection));
            return jobs.Where(job => job != null);
        }

        public async Task<DownloadSession> StartDownloadAsync(MediaItem item)
        {
            if (item == null)
                return null;
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
                return null;

            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);

            Database.Models.DownloadJob job = new Database.Models.DownloadJob()
            {
                MediaItemId = item.Id,
                EntryTime = DateTime.Now,
                SourceId = source.Id
            };

            // var session = new DownloadSession(job);
            await _JobDataSource.AddDownloadJob(job);
            StartWorker();
            return null;// session;
        }

        public async Task<IEnumerable<DownloadSession>> StartDownloadAsync(TVShow item)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var seasons = await _MediaLibrary.GetTVShowSeasons(item.Id);
            foreach (var season in seasons)
                jobs.AddRange(await StartDownloadAsync(season));
            return jobs;
        }

        public async Task<IEnumerable<DownloadSession>> StartDownloadAsync(TVShowSeason item)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var episodes = await _MediaLibrary.GetTVShowEpisodes(item.Id);
            foreach (var episode in episodes)
                jobs.Add(await StartDownloadAsync(episode));
            return jobs;
        }

        public async Task<DownloadSession> StartDownloadAsync(TVShowEpisode item)
        {
            if (item == null)
                return null;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                    return await StartDownloadAsync(mediaItem);
            }
            return null;
        }

        public async Task<IEnumerable<DownloadSession>> StartDownloadAsync(MovieCollection item)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var movies = await _MediaLibrary.GetMovies(item.Id);
            foreach (var movie in movies)
                jobs.Add(await StartDownloadAsync(movie));
            return jobs;
        }

        public async Task<DownloadSession> StartDownloadAsync(Movie item)
        {
            if (item == null)
                return null;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                    return await StartDownloadAsync(mediaItem);
            }
            return null;
        }

        public async void RemoveDownload(BaseModel item)
        {
            if (await RemoveDownloadAsync(item as MediaItem))
                OnDownloadDeleted(new BaseModelEventArgs(item));
            if (await RemoveDownloadAsync(item as TVShowEpisode))
                OnDownloadDeleted(new BaseModelEventArgs(item));
        }

        public async Task<bool> RemoveDownloadAsync(TVShowEpisode item)
        {
            if (item == null)
                return false;
            var removed = false;
            foreach (var itemId in item.MediaItems)
            {
                var mediaItem = await _MediaLibrary.GetMediaItemAsync(itemId);
                removed = (await RemoveDownloadAsync(mediaItem)) || removed;
            }
            return removed;
        }

        public async Task<bool> RemoveDownloadAsync(MediaItem item)
        {
            if (item == null)
                return false;
            if ((item.CopyType == MediaItemCopyType.Download) || (item.CopyType == MediaItemCopyType.Cache))
            {
                if (File.Exists(item.Path))
                    File.Delete(item.Path);
                await _MediaLibrary.RemoveMediaItemAsync(item);
                OnDownloadDeleted(new BaseModelEventArgs(item));
                return true;
            }
            var removed = false;
            var items = await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id);
            foreach (var alternateItem in items)
                if (await RemoveDownloadAsync(alternateItem))
                {
                    removed = true;
                    OnDownloadDeleted(new BaseModelEventArgs(item));
                }
            return removed;
        }

        private BackgroundWorker _Worker = null;
        private bool working = false;

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
            if (!working)
                _Worker.RunWorkerAsync();
        }

        private void _Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (working)
                return;
            working = true;
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
                    var source = _MediaLibrary.GetSourceAsync(collection.MediaSourceId).Wait<MediaElementSource>();
                    var statusId = _StatusPublisher.AddStatus($"Lade {mediaItem.Name}...", false);
                    try
                    {
                        if (source is null)
                            throw new ArgumentNullException(nameof(source));
                        else if (source is SmbMediaSource)
                            DownloadSmbMediaItem(source as SmbMediaSource, collection, mediaItem);
                        else if (source is FtpMediaSource)
                            DownloadFtpMediaItemAsync(source as FtpMediaSource,
                                                      collection,
                                                      mediaItem,
                                                      MediaItemCopyType.Download)
                            .Wait();
                        else if (source is HttpMediaSource)
                            DownloadHttpMediaItemAsync(source as HttpMediaSource,
                                                       collection,
                                                       mediaItem,
                                                       MediaItemCopyType.Download)
                            .Wait();
                        else
                            throw new NotSupportedException($"{source.GetType()}");
                    }
                    finally
                    {
                        _StatusPublisher.Clear(statusId);
                    }
                }
                _JobDataSource.RemoveDownloadJob(job).Wait();
            }
            catch (Exception ex)
            {
                _StatusPublisher.AddStatus(ex.Message, false);
            }
            finally
            {
                working = false;
            }
        }

        public async Task ContinueDownloadsAsync()
        {
            if (await _JobDataSource.DownloadJobsExist())
                StartWorker();
        }

        public void RemoveAllDownloads()
        {
            ClearFolder(_Settings.DownloadFolderPath);
            ClearFolder(_Settings.CacheFolderPath);
        }

        private void ClearFolder(string cacheFolderPath)
        {
            foreach (var folder in Directory.GetDirectories(cacheFolderPath))
                try
                {
                    Directory.Delete(folder, true);
                }
                catch { }
            foreach (var file in Directory.GetFiles(cacheFolderPath))
                try
                {
                    File.Delete(file);
                }
                catch { }
        }

    }
}
