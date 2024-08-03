using Mediathek.Extensions;
using Mediathek.Services.Database;
using Mediathek.Services.MediaLibrary.Scanner.Http;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System.ComponentModel;

namespace Mediathek.Services.MediaLibrary.Downloads
{
    public class DownloadManager: IDownloadManager
    {

        private readonly IMediaLibrary mediaLibrary;
        private readonly IJobDatabase jobDatabase;
        private readonly IStatusPublisher statusPublisher;
        private readonly MediaLibraryEnvironment settings;

        public DownloadManager(
            IMediaLibrary mediaLibrary,
            IJobDatabase jobDatabase,
            IStatusPublisher statusPublisher,
            ISettingsService settingsService,
            MediaLibraryEnvironment settings)
        {
            _SettingsService = settingsService;
            this.mediaLibrary = mediaLibrary;
            this.jobDatabase = jobDatabase;
            this.statusPublisher = statusPublisher;
            this.settings = settings;
        }

        public async Task<IEnumerable<DownloadSession>> StartDownloadAsync(BaseModel item, MediaItemCopyType copyType)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            jobs.Add(await StartDownloadAsync(item as MediaItem, copyType));
            jobs.Add(await StartDownloadAsync(item as TVShowEpisode, copyType));
            jobs.Add(await StartDownloadAsync(item as Movie, copyType));
            jobs.AddRange(await StartDownloadAsync(item as TVShow, copyType));
            jobs.AddRange(await StartDownloadAsync(item as TVShowSeason, copyType));
            jobs.AddRange(await StartDownloadAsync(item as MovieCollection, copyType));
            return jobs.Where(job => job != null);
        }

        private async Task<IEnumerable<DownloadSession>> StartDownloadAsync(TVShow item, MediaItemCopyType copyType)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var seasons = await mediaLibrary.GetTVShowSeasons(item.Id);
            foreach (var season in seasons)
                jobs.AddRange(await StartDownloadAsync(season, copyType));
            return jobs;
        }

        private async Task<IEnumerable<DownloadSession>> StartDownloadAsync(TVShowSeason item, MediaItemCopyType copyType)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var episodes = await mediaLibrary.GetTVShowEpisodes(item.Id);
            foreach (var episode in episodes)
                jobs.Add(await StartDownloadAsync(episode, copyType));
            return jobs;
        }

        private async Task<DownloadSession> StartDownloadAsync(TVShowEpisode item, MediaItemCopyType copyType)
        {
            if (item == null)
                return null;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await mediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                    return await StartDownloadAsync(mediaItem, copyType);
            }
            return null;
        }

        private async Task<IEnumerable<DownloadSession>> StartDownloadAsync(MovieCollection item, MediaItemCopyType copyType)
        {
            List<DownloadSession> jobs = new List<DownloadSession>();
            if (item == null)
                return jobs;
            var movies = await mediaLibrary.GetMovies(item.Id);
            foreach (var movie in movies)
                jobs.Add(await StartDownloadAsync(movie, copyType));
            return jobs;
        }

        private async Task<DownloadSession> StartDownloadAsync(Movie item, MediaItemCopyType copyType)
        {
            if (item == null)
                return null;
            foreach (var mediaItemId in item.MediaItems)
            {
                var mediaItem = await mediaLibrary.GetMediaItemAsync(mediaItemId);
                if (mediaItem.CopyType == MediaItemCopyType.None)
                    return await StartDownloadAsync(mediaItem, copyType);
            }
            return null;
        }

        public async Task<DownloadSession> StartDownloadAsync(MediaItem item, MediaItemCopyType copyType)
        {
            if (item == null)
                return null;

            Database.Models.DownloadJob job = new Database.Models.DownloadJob()
            {
                CopyType = copyType,
                MediaItemId = item.Id,
                EntryTime = DateTime.Now,
                SourceId = 0
            };
            var session = DownloadSession.CreateFromJob(job);
            await jobDatabase.AddDownloadJob(job);
            await CheckAddSession(session);
            return session;
        }

        public void ContinueDownloads()
        {
            StartWorker();
        }

        private async Task<MediaItem> GetExistingDownloadedItemAsync(MediaItem item, MediaItemCopyType copyType)
        {
            var mediaItems = (await mediaLibrary.GetAlternateMediaItemsAsync(item.Id)).ToList();
            foreach (var mediaItem in mediaItems.Where(mi => !File.Exists(mi.Path)))
            {
                await mediaLibrary.RemoveMediaItemAsync(mediaItem);
                mediaItems.Remove(mediaItem);
            }
            var existingItem = mediaItems.FirstOrDefault(i => i.CopyType == copyType);
            if (existingItem is not null)
                return existingItem;

            if (copyType == MediaItemCopyType.Cache)
            {
                var downloadItem = mediaItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Download);
                if (downloadItem is not null)
                    return downloadItem;
            }
            else if (copyType == MediaItemCopyType.Download)
            {
                var cacheItem = mediaItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Cache);
                if (cacheItem is not null)
                    return await TransferCacheToDownloadAsync(cacheItem);
            }
            return null;
        }

        private async Task<MediaItem> TransferCacheToDownloadAsync(MediaItem cacheItem)
        {
            var cachePath = settings.GetPath(MediaItemCopyType.Cache);
            var downloadPath = settings.GetPath(MediaItemCopyType.Download);
            if (cachePath != downloadPath)
            {
                var sourceFilePath = cacheItem.Path;
                var destFilePath = sourceFilePath.Replace(cachePath, downloadPath);
                var destFolderPath = Path.GetDirectoryName(destFilePath);
                if (!Path.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath);
                File.Move(sourceFilePath, destFilePath);
                cacheItem.Path = destFilePath;
            }
            cacheItem.CopyType = MediaItemCopyType.Download;
            await mediaLibrary.AddMediaItemAsync(cacheItem);
            return cacheItem;
        }

        public async void RemoveDownload(BaseModel item)
        {
            if (await RemoveDownloadAsync(item as MediaItem))
                ;// OnDownloadDeleted(new BaseModelEventArgs(item));
            if (await RemoveDownloadAsync(item as TVShowEpisode))
                ;// OnDownloadDeleted(new BaseModelEventArgs(item));
        }

        public async Task<bool> RemoveDownloadAsync(TVShowEpisode item)
        {
            if (item == null)
                return false;
            var removed = false;
            foreach (var itemId in item.MediaItems)
            {
                var mediaItem = await mediaLibrary.GetMediaItemAsync(itemId);
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
                await mediaLibrary.RemoveMediaItemAsync(item);

                // OnDownloadDeleted(new BaseModelEventArgs(item));
                return true;
            }
            var removed = false;
            var items = await mediaLibrary.GetAlternateMediaItemsAsync(item.Id);
            foreach (var alternateItem in items)
                if (await RemoveDownloadAsync(alternateItem))
                {
                    removed = true;

                    // OnDownloadDeleted(new BaseModelEventArgs(item));
                }
            return removed;
        }

        public void RemoveAllDownloads()
        {
            ClearFolder(settings.DownloadFolderPath);
            ClearFolder(settings.CacheFolderPath);
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

        #region Worker
        private List<DownloadSession> downloadSessions = new List<DownloadSession>();
        private async Task CheckAddSession(DownloadSession session)
        {
            switch (session.Job.CopyType)
            {
                case MediaItemCopyType.Cache:
                    var existingItems = (await mediaLibrary.GetAlternateMediaItemsAsync(session.Job.MediaItemId)).ToArray();
                    var download = existingItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Download);
                    if (download is not null)
                    {
                        session.SetFinished(download);
                        return;
                    }
                    var cache = existingItems.FirstOrDefault(i => i.CopyType == MediaItemCopyType.Cache);
                    if (cache is not null)
                    {
                        session.SetFinished(cache);
                        return;
                    }
                    AddSession(session);
                    break;
                default:
                    AddSession(session);
                    break;
            }
        }
        private void AddSession(DownloadSession session)
        {
            switch (session.Job.CopyType)
            {
                case MediaItemCopyType.Cache:
                    downloadSessions.Insert(0, session);
                    if ((currentSession != null) && (currentSession != session))
                        currentSession = null;
                    break;
                case MediaItemCopyType.Download:
                    downloadSessions.Add(session);
                    break;
            }
            StartWorker();
        }

        private void RemoveSession(DownloadSession session)
        {
            downloadSessions.Remove(session);
        }

        private async Task<DownloadSession> GetNextSessionAsync()
        {
            var session = downloadSessions.FirstOrDefault();
            if (session == null)
            {
                await LoadSessionsFromDatabaseAsync();
                session = downloadSessions.FirstOrDefault();
            }
            return session;
        }

        private async Task LoadSessionsFromDatabaseAsync()
        {
            var jobs = await jobDatabase.GetDownloadJobs();
            downloadSessions.AddRange(jobs
                .Where(job => !downloadSessions.Any(s => s.Job.Id == job.Id))
                .OrderByDescending(job => job.CopyType)
                .ThenBy(job => job.Id)
                .Select(job => DownloadSession.CreateFromJob(job)));
        }

        private BackgroundWorker worker = null;
        private bool working = false;
        private DownloadSession currentSession = null;
        private readonly ISettingsService _SettingsService;

        private void StartWorker()
        {
            if (worker != null)
                return;
            worker = new BackgroundWorker();
            worker.DoWork += _Worker_DoWork;
            worker.RunWorkerCompleted += _Worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private async void _Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((bool)e.Result)
            {
                statusPublisher.AddStatus(string.Empty, false);
                await Task.Delay(1000);
            }
            else
                await Task.Delay(10000);
            if (!working)
                worker.RunWorkerAsync();
        }

        private void _Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            if (working)
                return;
            working = true;
            try
            {
                //e.Result = ((bool)e.Result) || Worker_DownloadNextItem();
                //e.Result = ((bool)e.Result) || Worker_RemoveNextOldDownload();
            }
            catch (Exception ex)
            {
                statusPublisher.AddStatus(ex.Message, false);
            }
            finally
            {
                working = false;
            }
        }

        private bool Worker_RemoveNextOldDownload()
        {
            try
            {
                var mediaItem = mediaLibrary.GetDueDownloadedMediaItems(0, 1)
                                            .Wait<IEnumerable<MediaItem>>()
                                            .FirstOrDefault();
                if (mediaItem is null)
                    return false;
                if (mediaItem.CopyType != MediaItemCopyType.Download)
                {
                    mediaItem.DueDate = DateTime.MinValue;
                    mediaLibrary.UpdateMediaItemAsync(mediaItem, false).Wait();
                }
                else
                {
                    mediaItem.DueDate = DateTime.Now.AddMinutes(1);
                    mediaLibrary.UpdateMediaItemAsync(mediaItem, false).Wait();

                    RemoveDownload(mediaItem);
                }
                return true;
            }
            catch (Exception ex)
            {
                statusPublisher.AddStatus(ex.Message, false);
                return false;
            }
        }

        private bool Worker_DownloadNextItem()
        {
            currentSession = GetNextSessionAsync().Wait<DownloadSession>();
            var currSession = currentSession;
            if (currentSession is null)
                return false;
            try
            {
                if (!currentSession.Job.Failed)
                {
                    currSession.SetStarted();
                    CompleteJobAsync(currentSession.Job).Wait();
                    var mediaItem = mediaLibrary.GetMediaItemAsync(currentSession.Job.MediaItemId).Wait<MediaItem>();
                    var existingItem = GetExistingDownloadedItemAsync(mediaItem, currentSession.Job.CopyType)
                                       .Wait<MediaItem>();
                    if (existingItem is null)
                    {
                        var collection = mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId)
                                                     .Wait<MediaItemCollection>();
                        var source = mediaLibrary.GetSourceAsync(collection.MediaSourceId).Wait<MediaElementSource>();
                        var statusId = statusPublisher.AddStatus($"Lade {mediaItem.Name}...", false);
                        try
                        {
                            currSession.SetProgress(0);
                            existingItem = DownloadItemAsync(source,
                                                             collection,
                                                             mediaItem,
                                                             currentSession,
                                                             (progress) =>
                                                             {
                                                                 if (currentSession == null)
                                                                     throw new ApplicationException($"Download caceled");
                                                                 if (currSession.Progress != progress)
                                                                 {
                                                                     currSession.SetProgress(progress);
                                                                     var statusId = statusPublisher.AddStatus($"Lade {mediaItem.Name} ({currSession.Progress} %)...", false);
                                                                 }
                                                             })
                                           .Wait<MediaItem>();
                        }
                        finally
                        {
                            statusPublisher.Clear(statusId);
                        }
                    }
                    currSession.SetFinished(existingItem);
                }
                RemoveSession(currentSession);
            }
            catch (Exception ex)
            {
                if (currentSession == null)
                {
                    currSession.SetCanceled();
                    throw new ApplicationException(string.Empty);
                }
                else
                {
                    currentSession.SetFailed(ex);
                    if (currentSession.ErrorCounter >= 5)
                    {
                        currentSession.Job.Failed = true;
                        jobDatabase.AddDownloadJob(currentSession.Job);
                        RemoveSession(currentSession);
                    }
                }
                statusPublisher.AddStatus(ex.Message, false);
                return false;
            }
            _ = jobDatabase.RemoveDownloadJob(currentSession.Job);
            return true;
        }

        private async Task CompleteJobAsync(Database.Models.DownloadJob job)
        {
            var item = await mediaLibrary.GetMediaItemAsync(job.MediaItemId);
            if (item.CopyType != MediaItemCopyType.None)
            {
                item = await mediaLibrary.GetMediaItemAsync(item.OriginalMediaItemId);
                job.MediaItemId = item.Id;
            }
            if (job.SourceId == 0)
            {
                var collection = await mediaLibrary.GetMediaItemCollectionAsync(item.ParentCollectionId);
                var source = await mediaLibrary.GetSourceAsync(collection.MediaSourceId);
                job.SourceId = source.Id;
            }
        }

        private async Task<MediaItem> DownloadItemAsync(
            MediaElementSource source,
            MediaItemCollection collection,
            MediaItem mediaItem,
            DownloadSession currentSession,
            Action<float> OnProgressChanged)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = currentSession.Job.CopyType;
            alternateMediaItem.Path = Path.Combine(settings.GetPath(alternateMediaItem.CopyType),
                                                   collection.Id.ToString(),
                                                   mediaItem.Name);
            if (!File.Exists(alternateMediaItem.Path))
            {
                RemoteShare share = CreateRemoteShare(source);
                share.DownloadProgress += (sender, e) =>
                {
                    try
                    {
                        OnProgressChanged(e.Progress);
                    }
                    catch
                    {
                        e.Cancel = true;
                    }
                };
                share.DownloadFile(mediaItem.Path, alternateMediaItem.Path);
            }
            if (!File.Exists(alternateMediaItem.Path))
                throw new ApplicationException($"Download of file failed.");
            if ((_SettingsService.Current.Download_KeepingDuration != TimeSpan.Zero)
                && (alternateMediaItem.CopyType == MediaItemCopyType.Download))
                alternateMediaItem.DueDate = DateTime.Now.Add(_SettingsService.Current.Download_KeepingDuration);
            await mediaLibrary.AddMediaItemAsync(alternateMediaItem);
            return alternateMediaItem;
        }

        private RemoteShare CreateRemoteShare(MediaElementSource source)
        {
            if (source is SmbMediaSource)
                throw new NotImplementedException();
            else if (source is FtpMediaSource)
                throw new NotImplementedException();
            else if (source is HttpMediaSource)
                return CreateHttpShare(source as HttpMediaSource);
            else
                throw new NotSupportedException($"{source.GetType()}");
        }

        private RemoteShare CreateHttpShare(HttpMediaSource source)
        {
            return new HttpShare(source.Uri);
        }
        #endregion

    }
}
