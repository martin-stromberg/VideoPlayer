#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Xml;
using VideoPlayer.Extensions;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Models.Sources;
using VideoPlayer.Services.MediaLibrary.Scanner.Events;
using VideoPlayer.Services.MediaLibrary.Scanner.FTP;
using VideoPlayer.Services.MediaLibrary.Scanner.Models;
using VideoPlayer.Services.MediaLibrary.Scanner.Samba;
using VideoPlayer.Services.MediaLibrary.Scanner.Shares;
using VideoPlayer.Services.Mediathek;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.Services.MediaLibrary.Scanner
{
    public class LibraryScanner: ILibraryScanner
    {

        private readonly IStatusPublisher _StatusPublisher;
        private readonly IMediaLibrary mediaLibrary;
        private readonly ISettingsService _Settings;
        private readonly ILogger<LibraryScanner> logger;
        private readonly MediaLibraryEnvironment environment;
        private string[] FileExtVideo = { ".avi", ".mkv", ".mp4", ".mov" };
        private string[] FileExtAudio = { ".mp3", ".wav", ".ogg" };
        private string[] FileExtImage = { ".jpg", ".png" };
        private List<RemoteSourceScanner> _Scanners = null;

        public LibraryScanner(
            IMediaLibrary mediaLibrary,
            ILogger<LibraryScanner> logger,
            MediaLibraryEnvironment environment,
            ISettingsService settings,
            IStatusPublisher statusPublisher)
        {
            _Settings = settings;
            _StatusPublisher = statusPublisher;
            this.mediaLibrary = mediaLibrary;
            this.logger = logger;
            this.environment = environment;
            InitializeScanners();
        }

        private void InitializeScanners()
        {
            if (_Scanners != null)
                return;
            _Scanners = new List<RemoteSourceScanner>()
                {
                    new SambaShareScanner(),
                    new FtpScanner()
                };
            foreach (var scanner in _Scanners)
            {
                scanner.BeforeScanFolder += Scanner_BeforeScanFolder;
                scanner.FileFound += Scanner_FileFound;
                scanner.FolderFound += Scanner_FolderFound;
                scanner.ScanCompleted += Scanner_ScanCompleted;
            }
        }

        private void Scanner_BeforeScanFolder(object sender, FolderScanEventArgs e)
        {
            CheckContinue();
            _StatusPublisher.AddStatus($"{e.Value}", false);
            if (e.ScanFiles)
            {
                var scanner = sender as RemoteSourceScanner;
                scanner.CurrentSource.LatestScanPath = e.Value;
                mediaLibrary.AddSourceAsync(scanner.CurrentSource).Wait();
            }
        }

        private void Scanner_ScanCompleted(object sender, EventArgs e)
        {
            var scanner = sender as RemoteSourceScanner;
            scanner.CurrentSource.LatestScanPath = null;
            mediaLibrary.AddSourceAsync(scanner.CurrentSource).Wait();

            FindRemovedFiles(scanner.CurrentSource, scanner).Wait();
        }

        private void Scanner_FolderFound(object sender, FolderEventArgs e)
        {
            CheckContinue();
            var scanner = sender as RemoteSourceScanner;
            ProcessFolderAsync(scanner.CurrentSource, scanner, e.Folder).Wait();
        }

        private void Scanner_FileFound(object sender, FileEventArgs e)
        {
            CheckContinue();
            var scanner = sender as RemoteSourceScanner;
            ProcessFile(scanner.CurrentSource, scanner, e.File).Wait();
        }

        private void CheckContinue()
        {
            if (stopScan)
                throw new ApplicationException("Scan canceled");
        }

        private BackgroundWorker scanner = null;
        private bool stopScan = false;

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
            running = true;
            try
            {
                ScanQueueEntriesAsync().Wait();
            }
            catch { }

            if (!_Settings.Current.LibraryScan_AutomaticScan)
                return;

            try
            {
                ScanNextSource();
            }
            catch { }

            try
            {
                ScanNextCollectionMediaAsync().Wait();
            }
            catch { }

            try
            {
                RemoveDeletedSourcesAsync();
            }
            catch { }
        }

        private async void Scanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!stopScan)
                await Task.Delay(1000);
            if (!stopScan && _Settings.Current.LibraryScan_AutomaticScan)
                Start();
            else
                running = false;
        }

        private bool running = false;

        public void Start()
        {
            stopScan = false;
            if (scanner == null)
            {
                scanner = new BackgroundWorker();
                scanner.DoWork += Scanner_DoWork;
                scanner.RunWorkerCompleted += Scanner_RunWorkerCompleted;
            }
            scanner.RunWorkerAsync();
        }

        public void Stop()
        {
            if (running)
                stopScan = true;
        }

        public async Task WaitForFinish()
        {
            while (running)
                await Task.Delay(100);
        }

        private void ScanNextSource()
        {
            var source = mediaLibrary
                .GetSourcesAsync()
                .Wait<IEnumerable<MediaSource>>()
                .Where(s => !s.Inactive)
                .OrderBy(s => s.LastScan)
                .FirstOrDefault();
            if (source == null)
                return;
            if (source.LastScan.AddHours(_Settings.Current.LibraryScan_ScanIntervalHours) >= DateTime.Now)
            {
                _StatusPublisher.AddStatus(string.Empty, true);
                return;
            }
            ScanSource(source);
        }

        private void ScanSource(MediaSource source)
        {
            try
            {
                if (stopScan)
                    return;
                logger.LogInformation($"Start scanning source {source.Name}");
                source.LastScanStart = DateTime.Now;

                foreach (var scanner in _Scanners)
                    if (scanner.CanScan(source))
                    {
                        scanner.Scan(source);
                        logger.LogInformation($"Scan of source {source.Name} finished.");
                        return;
                    }
                throw new ApplicationException($"No scanner registered for this source.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                _StatusPublisher.AddStatus(ex.Message, true);
            }
            finally
            {
                source = mediaLibrary.GetSourceAsync(source.Id).Wait<MediaSource>();
                source.LastScan = DateTime.Now;
                mediaLibrary.AddSourceAsync(source);
            }
        }

        private async Task ScanNextCollectionMediaAsync()
        {
            var sources = mediaLibrary
                .GetSourcesAsync()
                .Wait<IEnumerable<MediaSource>>()
                .ToArray();
            var refDate = DateTime.Now.AddYears(-20);
            foreach (var source in sources)
            {
                SmbMediaSource smbSource = source as SmbMediaSource;
                if (smbSource != null)
                    await ScanNextSmbShareCollectionMediaAsync(smbSource, refDate);

                FtpMediaSource ftpSource = source as FtpMediaSource;
                if (ftpSource != null)
                    await ScanNextFtpShareCollectionMediaAsync(ftpSource, refDate);
            }
        }

        private async Task ScanRemoteCollectionMediaSyncAsync(
            RemoteSourceScanner scanner,
            MediaSource source,
            DateTime refDate)
        {
            var collectionsSource = await mediaLibrary.GetMediaItemCollectionsAsync(source.Id);
            var collections = collectionsSource
                    .OrderBy(coll => coll.MetaDataTime)
                    .Where(coll => coll.MetaDataTime < refDate);
            foreach (var collection in collections)
                await ScanCollectionMetaDataAsync(scanner, collection);
        }

        private async Task ScanCollectionMetaDataAsync(RemoteSourceScanner scanner, MediaItemCollection collection)
        {
            await ProcessMetaDataForFolderAsync(scanner, collection);
            await ProcessPictureForFolderAsync(scanner, collection);
            collection.MetaDataTime = DateTime.Now;
            await mediaLibrary.AddMediaItemCollectionAsync(collection);
        }

        private bool removingSources = false;

        private async void RemoveDeletedSourcesAsync()
        {
            if (removingSources)
                return;
            removingSources = true;
            try
            {
                var sources = (await mediaLibrary.GetSourcesAsync())
                    .Where(s => s.Inactive);
                foreach (var source in sources)
                    await mediaLibrary.RemoveMediaSourceAsync(source);
            }
            finally
            {
                removingSources = false;
            }
        }

        #region samba Share
        private async Task ScanNextSmbShareCollectionMediaAsync(SmbMediaSource smbSource, DateTime refDate)
        {
            // SambaShare share = new SambaShare(smbSource.ServerName, smbSource.Username, smbSource.Password);
            // SambaShareScanner smbScanner = new SambaShareScanner(share);
            // try
            // {
            // share.Connect();
            // await ScanRemoteCollectionMediaSyncAsync(smbScanner, smbSource, refDate);
            // }
            // catch (Exception ex)
            // {
            // Debug.WriteLine(ex);
            // }
            // finally
            // {
            // share.Disconnect();
            // }
            await Task.Delay(0);
        }
        #endregion

        #region Ftp
        private async Task ScanNextFtpShareCollectionMediaAsync(FtpMediaSource source, DateTime refDate)
        {
            // FtpShare share = new FtpShare(source.ServerName, source.Username, source.Password);
            // FtpScanner smbScanner = new FtpScanner(share);
            // await ScanRemoteCollectionMediaSyncAsync(smbScanner, source, refDate);
            await Task.Delay(0);
        }
        #endregion

        private async Task FindRemovedFiles(RemoteMediaSource source, RemoteSourceScanner scanner)
        {
            CheckContinue();
            var collections = await mediaLibrary.GetMediaItemCollectionsAsync(source.Id);
            foreach (var collection in collections)
            {
                _StatusPublisher.AddStatus($"{collection.Path}", false);
                var mediaItems = await mediaLibrary.GetMediaItemsAsync(collection.Id);
                foreach (var mediaItem in mediaItems.Where(mi => mi.LastConfirmation < source.LastScanStart))
                    await CheckRemovedMediaItemAsync(source, scanner, mediaItem);
            }
        }

        private async Task CheckRemovedMediaItemAsync(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            MediaItem mediaItem)
        {
            CheckContinue();
            var folderPath = Path.GetDirectoryName(mediaItem.Path).Replace('\\', source.PathDelimiter);
            var fileName = Path.GetFileName(mediaItem.Path);
            var fileExist = scanner.FindFiles(folderPath, fileName).Any();
            if (!fileExist)
                await mediaLibrary.RemoveMediaItemAsync(mediaItem);
            else
            {
                mediaItem.LastConfirmation = DateTime.Now;
                await mediaLibrary.AddMediaItemAsync(mediaItem);
            }
        }

        private async Task ProcessFile(RemoteMediaSource source, RemoteSourceScanner scanner, RemoteFile file)
        {
            try
            {
                logger.LogDebug($"File: {file.Path}");
                var ext = Path.GetExtension(file.Name);
                if (!FileExtVideo.Contains(ext) && !FileExtAudio.Contains(ext))
                    return;
                var item = await mediaLibrary.FindMediaItemAsync(source.Id, file.Path);
                if (item == null)
                {
                    logger.LogDebug($"Create new item.");
                    var folderPath = Path.GetDirectoryName(file.Path).Replace('\\', source.PathDelimiter);
                    var folder = await mediaLibrary.FindMediaItemCollectionAsync(source.Id, folderPath);
                    if (folder == null)
                        folder = await ProcessFolderAsync(source,
                                                          scanner,
                                                          new RemoteFolder()
                            {
                                Path = folderPath,
                                Name = Path.GetFileName(folderPath)
                            });

                    item = new MediaItem()
                    {
                        Name = file.Name,
                        Path = file.Path,
                        ParentCollectionId = folder.Id,
                        LastConfirmation = DateTime.Now
                    };
                    await mediaLibrary.AddMediaItemAsync(item);
                }
                try
                {
                    item.LastConfirmation = DateTime.Now;
                    if (item.MetaDataTime.AddHours(24) > DateTime.Now)
                    {
                        await mediaLibrary.AddMediaItemAsync(item);
                        return;
                    }
                    if (FileExtVideo.Contains(ext))
                        await ProcessMetaDataForVideoAsync(scanner, item);
                    if (FileExtAudio.Contains(ext))
                        ProcessMetaDataForAudio(item);

                    item.MetaDataTime = DateTime.Now;
                    await mediaLibrary.AddMediaItemAsync(item);
                }
                finally
                {
                    logger.LogDebug($"MediaItem: {item}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }

        private async Task ProcessMetaDataForVideoAsync(RemoteSourceScanner scanner, MediaItem item)
        {
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.Path);
            var currentFiles = scanner.FindFiles(nfoFolder, $"{fileNameWithoutExt}.*").ToArray();
            foreach (var file in currentFiles)
            {
                logger.LogDebug($"File with same name: {file.Name}");
                var ext = Path.GetExtension(file.Name);
                if (ext == ".nfo")
                    await ProcessNFOForVideoAsync(scanner, item, file);
                else if (ext == ".info")
                    await ProcessNFOForVideoAsync(scanner, item, file);
                else if (FileExtImage.Contains(ext))
                    await ProcessPictureForVideoAsync(scanner, item, file);
            }
            foreach (var file in currentFiles)
            {
                var ext = Path.GetExtension(file.Name);
                if (ext == ".txt")
                    await ProcessTextInfoForVideoAsync(scanner, item, file);
            }
        }

        private async Task ProcessTextInfoForVideoAsync(RemoteSourceScanner scanner, MediaItem item, RemoteFile file)
        {
            if (item.MetaInfo != null)
                return;
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string nfoPath = Path.Combine(nfoFolder, file.Name);
            string nfoName = Path.GetFileName(nfoPath);
            var remoteFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            if (remoteFile == null)
                return;
            string fileContent = scanner.ReadTextFile(nfoPath);
            MediathekInfoFile infoFile = new MediathekInfoFile();
            if (await infoFile.LoadAsync(fileContent))
                ProcessMediathekInfo(scanner, item, file, infoFile);
        }

        private async void ProcessMediathekInfo(
            RemoteSourceScanner scanner,
            MediaItem item,
            RemoteFile remoteFile,
            MediathekInfoFile infoFile)
        {
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string nfoPath = Path.Combine(nfoFolder, remoteFile.Name);
            string nfoName = Path.GetFileName(nfoPath);
            nfoName = Path.ChangeExtension(nfoName, ".nfo");
            nfoPath = Path.Combine(nfoFolder, nfoName);

            var nfoFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            if (nfoFile != null)
                return;

            XmlDocument NfoDoc = new XmlDocument();
            switch (infoFile.Type)
            {
                case MediathekInfoFile.VideoType.TVShow:
                    NfoDoc.LoadXml("<episodedetails/>");
                    NfoDoc.DocumentElement.FindChild("showname", true).InnerText = infoFile.Name;
                    NfoDoc.DocumentElement.FindChild("title", true).InnerText = infoFile.Title;
                    NfoDoc.DocumentElement.FindChild("season", true).InnerText = infoFile.SeasonNo.ToString();
                    NfoDoc.DocumentElement.FindChild("episode", true).InnerText = infoFile.EpisodeNo.ToString();
                    NfoDoc.DocumentElement.FindChild("plot", true).InnerText = infoFile.Plot;
                    break;
                case MediathekInfoFile.VideoType.Movie:
                    NfoDoc.LoadXml("<movie/>");
                    NfoDoc.DocumentElement.FindChild("title", true).InnerText = infoFile.Title;
                    NfoDoc.DocumentElement.FindChild("plot", true).InnerText = infoFile.Plot;
                    break;
            }

            scanner.WriteTextFile(nfoPath, NfoDoc.InnerXml);
            nfoFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            await ProcessNFOForVideoAsync(scanner, item, nfoFile);
        }

        private async Task ProcessNFOForVideoAsync(RemoteSourceScanner scanner, MediaItem item, RemoteFile nfoFile)
        {
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string nfoPath = Path.Combine(nfoFolder, nfoFile.Name);
            string nfoName = Path.GetFileName(nfoPath);
            var remoteFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            if (remoteFile == null)
                return;
            logger.LogDebug($"Load nfo file {nfoFile.Name}");
            XmlDocument XmlDoc = new XmlDocument();
            try
            {
                XmlDoc.LoadXml(scanner.ReadTextFile(nfoPath));
                if (XmlDoc.DocumentElement == null)
                    return;
                switch (XmlDoc.DocumentElement.Name)
                {
                    case "movie":
                        ProcessMovieInformation(item, XmlDoc.DocumentElement);
                        break;
                    case "episodedetails":
                        ProcessEposideInformation(item, XmlDoc.DocumentElement);
                        break;
                    default:
                        return;
                }
                await mediaLibrary.AddMediaItemAsync(item);
            }
            catch (XmlException ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }

        private void ProcessMovieInformation(MediaItem item, XmlElement documentElement)
        {
            MovieInformation Info = new MovieInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText.Trim(),
                Genre = documentElement.FindChild("genre", true).InnerText.Trim(),
                Plot = documentElement.FindChild("plot", true).InnerText.Trim(),
            };
            item.MetaInfo = Info;
        }

        private void ProcessEposideInformation(MediaItem item, XmlElement documentElement)
        {
            EpisodeInformation Info = new EpisodeInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText.Trim(),
                ShowName = documentElement.FindChild("showname", true).InnerText.Trim(),
                Episode = documentElement.FindChild("episode", true).InnerText.Trim(),
                Season = documentElement.FindChild("season", true).InnerText.Trim(),
            };
            item.MetaInfo = Info;
        }

        private async Task ProcessPictureForVideoAsync(RemoteSourceScanner scanner, MediaItem item, RemoteFile picFile)
        {
            string picFolder = Path.GetDirectoryName(item.Path);
            string picPath = Path.Combine(picFolder, picFile.Name);
            string picName = Path.GetFileName(picPath);
            var remoteFile = scanner
                .FindFiles(picFolder, picName)
                .FirstOrDefault();
            if (remoteFile == null)
                return;

            logger.LogDebug($"Caching picture file {picFile.Name}");
            var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
            string cachFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
            scanner.DownloadFile(picPath, cachFile);
            item.PicturePath = cachFile.Remove(0, environment.CacheRootPath.Length + 1);
            item.Picture = ImageSource.FromFile(cachFile);
            await mediaLibrary.AddMediaItemAsync(item);
        }

        private void ProcessMetaDataForAudio(MediaItem item)
        {
            logger.LogDebug($"Processing of meta data for audio is not implemented.");
        }

        private async Task<MediaItemCollection> ProcessFolderAsync(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            RemoteFolder folder)
        {
            try
            {
                logger.LogDebug($"Folder: {folder.Path}");
                var item = await mediaLibrary.FindMediaItemCollectionAsync(source.Id, folder.Path);
                if (item == null)
                    if (($"{folder.Path}") == source.Path)
                    {
                        logger.LogDebug($"Create root collection.");
                        item = new MediaItemCollection()
                        {
                            MediaSourceId = source.Id,
                            Name = source.Name,
                            Path = folder.Path,
                            ParentCollectionId = 0
                        };
                        await mediaLibrary.AddMediaItemCollectionAsync(item);
                    }
                try
                {
                    if (item == null)
                    {
                        logger.LogDebug($"Create new collection.");
                        var folderPath = Path.GetDirectoryName(folder.Path).Replace('\\', source.PathDelimiter);
                        var parentFolder = await mediaLibrary.FindMediaItemCollectionAsync(source.Id, folderPath);
                        if (parentFolder == null)
                            parentFolder = await ProcessFolderAsync(source,
                                                                    scanner,
                                                                    new RemoteFolder()
                                {
                                    Path = folderPath,
                                    Name = Path.GetFileName(folderPath)
                                });
                        item = new MediaItemCollection()
                        {
                            Name = folder.Name,
                            MediaSourceId = source.Id,
                            Path = folder.Path,
                            ParentCollectionId = parentFolder.Id
                        };
                        await mediaLibrary.AddMediaItemCollectionAsync(item);
                    }
                    logger.LogDebug($"MediaItemCollection: {item}");
                    if (item.MetaDataTime.AddHours(24) > DateTime.Now)
                        return item;
                    await ScanCollectionMetaDataAsync(scanner, item);
                    logger.LogDebug($"MediaItemCollection: {item}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
                return item;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return null;
            }
        }

        private async Task ProcessPictureForFolderAsync(RemoteSourceScanner scanner, MediaItemCollection item)
        {
            var picPath = Path.Combine(item.Path, "folder.jpg");
            var picName = Path.GetFileName(picPath);
            var picFolder = Path.GetDirectoryName(picPath);
            var remoteFile = scanner
                .FindFiles(picFolder, picName)
                .FirstOrDefault();
            if (remoteFile == null)
                return;
            logger.LogDebug($"Caching Folder Picture file: {picPath}");
            var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
            string cachFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
            scanner.DownloadFile(picPath, cachFile);
            item.PicturePath = cachFile.Remove(0, environment.CacheRootPath.Length + 1);
            item.Picture = ImageSource.FromFile(cachFile);
            await mediaLibrary.AddMediaItemCollectionAsync(item);
        }

        private async Task ProcessMetaDataForFolderAsync(RemoteSourceScanner scanner, MediaItemCollection item)
        {
            var nfoPath = Path.Combine(item.Path, "tvshow.nfo");
            var nfoName = Path.GetFileName(nfoPath);
            var nfoFolder = Path.GetDirectoryName(nfoPath);
            var remoteFile = scanner
                .FindFiles(nfoFolder, nfoName)
                .FirstOrDefault();
            if (remoteFile == null)
                return;
            logger.LogDebug($"Download TV Show Information file: {nfoPath}");
            string tempFile = Path.Combine(environment.TempFolderPath, nfoName);
            XmlDocument XmlDoc = new XmlDocument();
            XmlDoc.LoadXml(scanner.ReadTextFile(nfoPath));
            if (XmlDoc.DocumentElement == null)
                return;
            switch (XmlDoc.DocumentElement.Name)
            {
                case "tvshow":
                    ProcessTVShowInformation(item, XmlDoc.DocumentElement);
                    break;
                default:
                    return;
            }
            await mediaLibrary.AddMediaItemCollectionAsync(item);
        }

        private void ProcessTVShowInformation(MediaItemCollection item, XmlElement documentElement)
        {
            TVShowInformation info = new TVShowInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText.Trim(),
                Plot = documentElement.FindChild("plot", true).InnerText.Trim(),
            };
            item.MetaInfo = info;
        }

        private ConcurrentQueue<BaseModel> ScanQueue = new ConcurrentQueue<BaseModel>();

        private async Task ScanQueueEntriesAsync()
        {
            while (ScanQueue.TryDequeue(out BaseModel model))
                await ScanNextQueueEntryAsync(model);
        }

        private async Task ScanNextQueueEntryAsync(BaseModel model)
        {
            await ScanMediaItemAsync(model as MediaItem);
        }

        private async Task ScanMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem is null)
                return;
            mediaItem = await mediaLibrary.GetMediaItemAsync(mediaItem.Id);
            mediaItem.MetaDataTime = DateTime.MinValue;
            await mediaLibrary.AddMediaItemAsync(mediaItem);
            var collection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            var source = await mediaLibrary.GetSourceAsync(collection.MediaSourceId);
            foreach (var scanner in _Scanners)
                if (scanner.CanScan(source))
                {
                    scanner.Scan(source, mediaItem);
                    logger.LogInformation($"Scan of source {mediaItem.Name} finished.");
                    return;
                }
        }

        public void Rescan(MediaItem item)
        {
            ScanQueue.Enqueue(item);
            if (!running)
                Start();
        }

    }

}
