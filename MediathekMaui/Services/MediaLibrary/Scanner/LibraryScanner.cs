using ImageMagick;
using Mediathek.Extensions;
using Mediathek.Services.MediaLibrary.Scanner.Events;
using Mediathek.Services.MediaLibrary.Scanner.FTP;
using Mediathek.Services.MediaLibrary.Scanner.Http;
using Mediathek.Services.MediaLibrary.Scanner.Models;
using Mediathek.Services.MediaLibrary.Scanner.Samba;
using Mediathek.Services.MediaLibrary.Scanner.Shares;
using Mediathek.Services.MediaLibrary.Scanner.SSH;
using Mediathek.Services.Mediathek;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

namespace Mediathek.Services.MediaLibrary.Scanner
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
                new FtpScanner(),
                new SSHScanner(),
                new HttpScanner()
            };
            foreach (var scanner in _Scanners)
            {
                scanner.BeforeScanFolder += Scanner_BeforeScanFolder;
                scanner.AfterScanFolder += Scanner_AfterScanFolder;
                scanner.FileFound += Scanner_FileFound;
                scanner.FolderFound += Scanner_FolderFound;
                scanner.ScanCompleted += Scanner_ScanCompleted;
            }
        }

        private void Scanner_AfterScanFolder(object sender, FolderScanEventArgs e)
        {
            lastFolderCollections.TryTake(out lastFolderCollection);
            if (e.Success && (lastFolderCollection != null) && (lastFolderCollection.Path == e.Value))
            {
                lastFolderCollection = mediaLibrary.GetMediaItemCollectionAsync(lastFolderCollection.Id)
                                                   .Wait<MediaItemCollection>();
                lastFolderCollection.LastUpdate = DateTime.Now;
                mediaLibrary.AddMediaItemCollectionAsync(lastFolderCollection);
            }
        }

        private void Scanner_BeforeScanFolder(object sender, FolderScanEventArgs e)
        {
            CheckContinue();
            _StatusPublisher.AddStatus($"{e.Value}", false);
            var scanner = sender as RemoteSourceScanner;
            if (e.ScanFiles)
            {
                scanner.CurrentSource.LatestScanPath = e.Value;
                mediaLibrary.AddSourceAsync(scanner.CurrentSource).Wait();
            }
            if ((lastFolderCollection != null) && (lastFolder != null))
            {
                if (!scanner.ProcessAll)
                    if (lastFolderCollection.LastUpdate >= lastFolder.LastWriteTime)
                    {
                        e.ScanFiles = false;
                        e.ScanFolders = false;
                    }
            }
        }

        private MediaItemCollection lastFolderCollection = null;
        private DateTime lastFolderCollectionUpdateTime = DateTime.MinValue;
        private RemoteFolder lastFolder = null;
        private ConcurrentBag<MediaItemCollection> lastFolderCollections = new ConcurrentBag<MediaItemCollection>();

        private void Scanner_ScanCompleted(object sender, EventArgs e)
        {
            var scanner = sender as RemoteSourceScanner;
            scanner.CurrentSource.LatestScanPath = null;
            mediaLibrary.AddSourceAsync(scanner.CurrentSource).Wait();
        }

        private void Scanner_FolderFound(object sender, FolderEventArgs e)
        {
            CheckContinue();
            var scanner = sender as RemoteSourceScanner;
            lastFolderCollection = ProcessFolderAsync(scanner.CurrentSource, scanner, e.Folder)
                                   .Wait<MediaItemCollection>();
            lastFolderCollections.Add(lastFolderCollection);
        }

        private void Scanner_FileFound(object sender, FileEventArgs e)
        {
            CheckContinue();
            _StatusPublisher.AddStatus($"{e.File.Path}", false);
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
            if (!working)
                Scanner_DoWork();
        }

        private async void Scanner_DoWork()
        {
            running = true;
            working = true;
            try
            {
                var clean = false;
                try
                {
                    await SaveMetaInformationAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }
                try
                {
                    clean = await ScanQueueEntriesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }

                try
                {
                    await CleanQueueEntriesAsync(clean).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }

                if (!_Settings.Current.LibraryScan_AutomaticScan)
                    return;

                try
                {
                    await ScanNextSourceAsync();
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }

                try
                {
                    await ScanNextCollectionMediaAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }

                try
                {
                    await RemoveDeletedSourcesAsync();
                }
                catch (Exception ex)
                {
                    _StatusPublisher.AddStatus(ex.Message, true);
                }
            }
            finally
            {
                _StatusPublisher.AddStatus(string.Empty, false);
                working = false;
            }
        }

        private async void Scanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!stopScan)
                await Task.Delay(1000).ConfigureAwait(false);
            if (!stopScan && _Settings.Current.LibraryScan_AutomaticScan)
                Start();
            else if (working)
                Start();
            else if (MetaInfoQueue.Any() || ScanQueue.Any() || CleanQueue.Any())
                Start();
            else
                running = false;
        }

        private bool running = false;
        private bool working = false;

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

        private async Task ScanNextSourceAsync()
        {
            var source = (await mediaLibrary
                .GetSourcesAsync())
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
            await ScanSourceAsync(source);
        }

        private async Task ScanSourceAsync(MediaElementSource source)
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
                        scanner.ProcessAll = source.CompleteNextScan;
                        scanner.Scan(source, false);

                        await FindRemovedFiles(source as RemoteMediaSource, scanner);
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
                source = mediaLibrary.GetSourceAsync(source.Id).Wait<MediaElementSource>();
                source.CompleteNextScan = false;
                source.LastScan = DateTime.Now;
                await mediaLibrary.AddSourceAsync(source);
            }
        }

        private async Task ScanNextCollectionMediaAsync()
        {
            var sources = mediaLibrary
                .GetSourcesAsync()
                .Wait<IEnumerable<MediaElementSource>>()
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
            MediaElementSource source,
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

        private async Task RemoveDeletedSourcesAsync()
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

        private async Task FindRemovedFiles(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            bool deepClean = false)
        {
            if (source == null)
                return;
            CheckContinue();
            var collections = await mediaLibrary.GetMediaItemCollectionsAsync(source.Id);
            long statusId = 0;
            foreach (var collection in collections.OrderBy(c => c.Path))
            {
                statusId = _StatusPublisher.AddStatus($"Bereinige: {collection.Path}", false);

                var currentCollection = await CheckRemovedMediaItemCollectionAsync(source, scanner, collection);
                if (currentCollection == null)
                    continue;

                var mediaItems = await mediaLibrary.GetMediaItemsAsync(currentCollection.Id);
                foreach (var mediaItem in mediaItems.Where(mi =>
                                                           deepClean || (mi.LastConfirmation < source.LastScanStart)))
                    await CheckRemovedMediaItemAsync(source, scanner, mediaItem);
            }
            _StatusPublisher.Clear(statusId);
        }

        private async Task<MediaItemCollection> CheckRemovedMediaItemCollectionAsync(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            MediaItemCollection collection)
        {
            try
            {
                var folderPath = Path.GetDirectoryName(collection.Path).Replace('\\', source.PathDelimiter);
                var fileName = Path.GetFileName(collection.Path);
                var fileExist = scanner.FindFolders(source, folderPath, fileName).Any();
                if (fileExist)
                    return collection;
                await mediaLibrary.RemoveMediaItemCollection(collection);
                return null;
            }
            catch (Exception ex)
            {
                _StatusPublisher.AddStatus(ex.Message, true);
                return null;
            }
        }

        private async Task CheckRemovedMediaItemAsync(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            MediaItem mediaItem)
        {
            try
            {
                CheckContinue();
                var folderPath = Path.GetDirectoryName(mediaItem.Path).Replace('\\', source.PathDelimiter);
                var fileName = Path.GetFileName(mediaItem.Path);
                var fileExist = scanner.FindFiles(source, folderPath, fileName).Any();
                if (!fileExist)
                    await mediaLibrary.RemoveMediaItemAsync(mediaItem);
                else
                {
                    mediaItem.LastConfirmation = DateTime.Now;
                    await mediaLibrary.AddMediaItemAsync(mediaItem);
                }
            }
            catch (Exception ex)
            {
                _StatusPublisher.AddStatus(ex.Message, true);
            }
        }

        private TimeSpan metaDataUpdateDuration = TimeSpan.FromSeconds(1);

        private async Task ProcessFile(RemoteMediaSource source, RemoteSourceScanner scanner, RemoteFile file)
        {
            try
            {
                logger.LogDebug($"File: {file.Path}");
                var ext = Path.GetExtension(file.Name);
                if (!FileExtVideo.Contains(ext) && !FileExtAudio.Contains(ext))
                    return;
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                if (fileName.EndsWith("-trailer"))
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
                    if (source.CompleteNextScan)
                        item.MetaDataTime = DateTime.MinValue;
                    item.LastConfirmation = DateTime.Now;
                    if (item.MetaDataTime.Add(metaDataUpdateDuration) > DateTime.Now)
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
                _StatusPublisher.AddStatus(ex.Message, true);
            }
        }

        private async Task ProcessMetaDataForVideoAsync(RemoteSourceScanner scanner, MediaItem item)
        {
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.Path);
            var currentFiles = scanner.FindFiles(nfoFolder, $"{fileNameWithoutExt}.*")
                                      .Concat(scanner.FindFiles(nfoFolder, $"{fileNameWithoutExt}-thumb.*"))
                                      .Concat(scanner.FindFiles(nfoFolder, $"{fileNameWithoutExt}-poster.*"))
                                      .Concat(scanner.FindFiles(nfoFolder, $"{fileNameWithoutExt}-trailer.*"))
                                      .OrderByDescending(f => f.Name)
                                      .ToArray();
            foreach (var file in currentFiles)
            {
                logger.LogDebug($"File with same name: {file.Name}");
                var ext = Path.GetExtension(file.Name);
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                if (ext == ".nfo")
                    await ProcessNFOForVideoAsync(scanner, item, file);
                else if (ext == ".info")
                    await ProcessNFOForVideoAsync(scanner, item, file);
                else if (FileExtImage.Contains(ext))
                    await ProcessPictureForVideoAsync(scanner, item, file);
                else if (fileName.EndsWith("-trailer"))
                    await ProcessTrailerForVideoAsync(scanner, item, file);
            }
            foreach (var file in currentFiles)
            {
                var ext = Path.GetExtension(file.Name);
                if (ext == ".txt")
                    await ProcessTextInfoForVideoAsync(scanner, item, file);
            }
        }

        private async Task ProcessTrailerForVideoAsync(
            RemoteSourceScanner scanner,
            MediaItem mediaItem,
            RemoteFile file)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Trailer;
            alternateMediaItem.Path = file.Path;

            var trailerItems = (await mediaLibrary.GetAlternateMediaItemsAsync(mediaItem.Id))
                .Where(mi => mi.CopyType == MediaItemCopyType.Trailer);
            var existing = trailerItems.FirstOrDefault(mi => mi.Path == alternateMediaItem.Path);
            if (existing != null)
                return;

            await mediaLibrary.AddMediaItemAsync(alternateMediaItem);
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

            if (!string.IsNullOrWhiteSpace(infoFile.ImageURL))
                try
                {
                    var imageFolder = Path.GetDirectoryName(item.Path);
                    string imageFolderPath = Path.Combine(imageFolder, remoteFile.Name);
                    string imageName = Path.GetFileName(imageFolderPath);
                    imageName = Path.ChangeExtension(nfoName, ".jpg");
                    imageFolderPath = Path.Combine(imageFolder, imageName);

                    var imageFile = scanner.FindFiles(imageFolder, imageName).FirstOrDefault();
                    if (imageFile == null)
                    {
                        scanner.SavePictureFromUri(infoFile.ImageURL, imageFolderPath);
                        imageFile = scanner.FindFiles(imageFolder, imageName).FirstOrDefault();
                        await ProcessPictureForVideoAsync(scanner, item, imageFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

            var nfoFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            if (nfoFile == null)
                try
                {
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
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
        }

        private async Task ProcessNFOForVideoAsync(RemoteSourceScanner scanner, MediaItem item, RemoteFile nfoFile)
        {
            string nfoFolder = Path.GetDirectoryName(item.Path);
            string nfoPath = Path.Combine(nfoFolder, nfoFile.Name);
            string nfoName = Path.GetFileName(nfoPath);
            var remoteFile = scanner.FindFiles(nfoFolder, nfoName).FirstOrDefault();
            if (remoteFile == null)
                return;

            if (item.MetaInfo is MediaInformation)
                if (!scanner.ProcessAll)
                    if (((MediaInformation)item.MetaInfo).LastUpdate > nfoFile.LastWriteTime)
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
                ReleaseDate = documentElement.FindChild("releasedate", true).InnerText.Trim().ToDateTime(),
                Year = documentElement.FindChild("year", true).InnerText.Trim().ToInt32(),
                Genres = documentElement.FindChildren("genre").Select(node => node.InnerText.Trim()).ToArray(),
                PremieredAt = documentElement.FindChild("premiered", true).InnerText.ToDateTime(),
                Language = documentElement.FindChild("language", true).InnerText.Trim(),
                Countries = documentElement.FindChildren("country").Select(node => node.InnerText.Trim()).ToArray(),
                LastUpdate = DateTime.Now
            };
            if ((Info.Year == 0) && (Info.ReleaseDate != default(DateTime)))
                Info.Year = Info.ReleaseDate.Year;
            item.MetaInfo = Info;
        }

        private void ProcessEposideInformation(MediaItem item, XmlElement documentElement)
        {
            Regex regex = new Regex("\\((\\d+)\\)");
            Match match = regex.Match(item.Name);

            EpisodeInformation Info = new EpisodeInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText.Trim(),
                ShowName = documentElement.FindChild("showname", true).InnerText.Trim(),
                Episode = documentElement.FindChild("episode", true).InnerText.Trim(),
                Part = match.Success ? match.Groups[1].Value : string.Empty,
                Season = documentElement.FindChild("season", true).InnerText.Trim(),
                Plot = documentElement.FindChild("plot", true).InnerText.Trim(),
                AiredAt = documentElement.FindChild("aired", true).InnerText.ToDateTime(),
                LastUpdate = DateTime.Now
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
            if (item.PictureTime >= picFile.LastWriteTime)
                return;

            logger.LogDebug($"Caching picture file {picFile.Name}");
            var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
            string cacheFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
            scanner.DownloadFile(picPath, cacheFile);
            item.PicturePath = cacheFile.Remove(0, environment.CacheRootPath.Length + 1);
            item.PictureTime = picFile.LastWriteTime;

            var thumbnailFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
            string thumbnailFile = Path.Combine(environment.CacheFolderPath, thumbnailFileName);
            scanner.DownloadThumbnail(remoteFile.Path,
                                      thumbnailFile,
                                      _Settings.Current.ThumbnailWidth,
                                      _Settings.Current.ThumbnailHeight);

            item.PictureThumbnailPath = File.Exists(thumbnailFile) ? thumbnailFile.Remove(0, environment.CacheRootPath.Length + 1) : string.Empty;
            item.Picture = ImageSource.FromFile(thumbnailFile ?? cacheFile);
            await mediaLibrary.AddMediaItemAsync(item);
        }

        private string CreateThumbnail(string sourceFilePath)
        {
            string thumbFile = Path.ChangeExtension(sourceFilePath, $".thumbnail{Path.GetExtension(sourceFilePath)}");
            try
            {
                using (var image = new MagickImage(sourceFilePath))
                {
                    image.Format = image.Format; // Get or Set the format of the image.
                    image.Resize(200, 240); // fit the image into the requested width and height. 
                    image.Quality = 10; // This is the Compression level.
                    image.Write(thumbFile);
                }
            }
            catch
            {
                return string.Empty;
            }
            return thumbFile;
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
                lastFolderCollectionUpdateTime = DateTime.MinValue;
                lastFolder = folder;
                var item = await mediaLibrary.FindMediaItemCollectionAsync(source.Id, folder.Path);
                if (item != null)
                    lastFolderCollectionUpdateTime = item.MetaDataTime;
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
            var changed = false;
            var picPath = string.Empty;
            var picName = string.Empty;
            var picFolder = string.Empty;
            RemoteFile remoteFile = null;
            foreach (var ext in FileExtImage)
            {
                picPath = Path.Combine(item.Path, $"folder{ext}");
                picName = Path.GetFileName(picPath);
                picFolder = Path.GetDirectoryName(picPath);
                remoteFile = scanner
                    .FindFiles(picFolder, picName)
                    .FirstOrDefault();

                if (remoteFile != null)
                {
                    logger.LogDebug($"Caching Folder Picture file: {picPath}");
                    var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
                    string cachFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
                    scanner.DownloadFile(picPath, cachFile);
                    item.PicturePath = cachFile.Remove(0, environment.CacheRootPath.Length + 1);
                    item.Picture = ImageSource.FromFile(cachFile);
                    changed = true;
                    break;
                }
            }

            if (!changed)
                foreach (var ext in FileExtImage)
                {
                    picPath = Path.Combine(item.Path, $"poster{ext}");
                    picName = Path.GetFileName(picPath);
                    picFolder = Path.GetDirectoryName(picPath);
                    remoteFile = scanner
                        .FindFiles(picFolder, picName)
                        .FirstOrDefault();
                    if (remoteFile != null)
                    {
                        logger.LogDebug($"Caching Poster Picture file: {picPath}");
                        var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
                        string cachFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
                        scanner.DownloadFile(picPath, cachFile);
                        item.PicturePath = cachFile.Remove(0, environment.CacheRootPath.Length + 1);
                        item.Picture = ImageSource.FromFile(cachFile);
                        changed = true;
                        break;
                    }
                }

            foreach (var ext in FileExtImage)
            {
                picPath = Path.Combine(item.Path, $"banner{ext}");
                picName = Path.GetFileName(picPath);
                picFolder = Path.GetDirectoryName(picPath);
                remoteFile = scanner
                    .FindFiles(picFolder, picName)
                    .FirstOrDefault();
                if (remoteFile != null)
                {
                    logger.LogDebug($"Caching Poster Picture file: {picPath}");
                    var cacheFileName = $"{Guid.NewGuid()}{Path.GetExtension(picName)}";
                    string cachFile = Path.Combine(environment.CacheFolderPath, cacheFileName);
                    scanner.DownloadFile(picPath, cachFile);
                    item.BannerPath = cachFile.Remove(0, environment.CacheRootPath.Length + 1);
                    item.Banner = ImageSource.FromFile(cachFile);
                    changed = true;
                    break;
                }
            }
            if (changed)
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
                Genres = documentElement.FindChildren("genre").Select(node => node.InnerText.Trim()).ToArray(),
                PremieredAt = documentElement.FindChild("premiered", true).InnerText.ToDateTime(),
                Language = documentElement.FindChild("language", true).InnerText.Trim(),
            };
            item.MetaInfo = info;
        }

        private struct QueueEntry
        {

            public BaseModel Entry;
            public object[] Arguments;

        }

        private ConcurrentQueue<QueueEntry> ScanQueue = new ConcurrentQueue<QueueEntry>();
        private ConcurrentQueue<BaseModel> CleanQueue = new ConcurrentQueue<BaseModel>();

        private async Task<bool> ScanQueueEntriesAsync()
        {
            var result = false;
            while (ScanQueue.TryDequeue(out QueueEntry model))
                result = (await ScanNextQueueEntryAsync(model.Entry, (bool)model.Arguments[0])) || result;
            return result;
        }

        private async Task<bool> ScanNextQueueEntryAsync(BaseModel model, bool all)
        {
            return (await ScanMediaItemAsync(model as MediaItem))
                || (await ScanMediaItemAsync(model as MediaElementSource, all));
        }

        private async Task<bool> ScanMediaItemAsync(MediaElementSource mediaSource, bool all)
        {
            if (mediaSource is null)
                return false;

            mediaSource = await mediaLibrary.GetSourceAsync(mediaSource.Id);
            mediaSource.LastScan = DateTime.MinValue;
            foreach (var scanner in _Scanners)
                if (scanner.CanScan(mediaSource))
                {
                    scanner.ProcessAll = all;
                    scanner.Scan(mediaSource, true);
                    logger.LogInformation($"Scan of source {mediaSource.Name} finished.");
                    return true;
                }
            return false;
        }

        private async Task<bool> ScanMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem is null)
                return false;
            mediaItem = await mediaLibrary.GetMediaItemAsync(mediaItem.Id);
            mediaItem.MetaDataTime = DateTime.MinValue;
            await mediaLibrary.AddMediaItemAsync(mediaItem);
            var collection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            var source = await mediaLibrary.GetSourceAsync(collection.MediaSourceId);
            foreach (var scanner in _Scanners)
                if (scanner.CanScan(source))
                {
                    throw new NotImplementedException();

                    // logger.LogInformation($"Scan of source {mediaItem.Name} finished.");
                    // return true;
                }
            return false;
        }

        private async Task CleanQueueEntriesAsync(bool categorizedEntries)
        {
            while (CleanQueue.TryDequeue(out BaseModel model))
                await CleanQueueEntriesAsync(model);
            if (categorizedEntries)
                await CleanCategorizedEntries();
            _StatusPublisher.AddStatus(string.Empty, false);
        }

        private async Task CleanQueueEntriesAsync(BaseModel model)
        {
            await CleanQueueEntriesAsync(model as MediaElementSource);
        }

        private async Task CleanQueueEntriesAsync(MediaElementSource mediaSource)
        {
            if (mediaSource is null)
                return;

            mediaSource = await mediaLibrary.GetSourceAsync(mediaSource.Id);
            foreach (var scanner in _Scanners)
                if (scanner.CanScan(mediaSource))
                {
                    await FindRemovedFiles(mediaSource as RemoteMediaSource, scanner, true);
                    logger.LogInformation($"Cleaning of source {mediaSource.Name} finished.");
                    return;
                }
        }

        private async Task CleanCategorizedEntries()
        {
            _StatusPublisher.AddStatus($"Bereinige kategoriesierte Filme", false);
            await CleanMovieCollection(0);
            var collections = await mediaLibrary.GetMovieCollections();
            foreach (var collection in collections)
                await CleanMovieCollection(collection);
            _StatusPublisher.AddStatus($"Bereinige kategoriesierte Serien", false);
            var shows = await mediaLibrary.GetTVShows();
            foreach (var show in shows)
                await CleanTVShow(show);
        }

        private async Task CleanTVShow(TVShow show)
        {
            _StatusPublisher.AddStatus($"Bereinige {show.Name}", false);
            var seasons = await mediaLibrary.GetTVShowSeasons(show.Id);
            foreach (var season in seasons)
                await CleanTVShowSeason(season);
            seasons = await mediaLibrary.GetTVShowSeasons(show.Id);
            if (seasons.Any())
                return;
            await mediaLibrary.RemoveTVShowAsync(show);
        }

        private async Task CleanTVShowEpisode(TVShowEpisode episode)
        {
            if (episode.MediaItems.Any())
                return;
            await mediaLibrary.RemoveTVShowEpisodeAsync(episode);
        }

        private async Task CleanTVShowSeason(TVShowSeason season)
        {
            CheckContinue();
            var episodes = await mediaLibrary.GetTVShowEpisodes(season.Id);
            foreach (var episode in episodes)
                await CleanTVShowEpisode(episode);
            episodes = await mediaLibrary.GetTVShowEpisodes(season.Id);
            if (episodes.Any())
                return;
            await mediaLibrary.RemoveTVShowSeasonAsync(season);
        }

        private async Task CleanMovieCollection(MovieCollection collection)
        {
            CheckContinue();
            await CleanMovieCollection(collection.Id);
            var movies = await mediaLibrary.GetMovies(collection.Id);
            if (movies.Any())
                return;
            await mediaLibrary.RemoveMovieCollectionAsync(collection);
        }

        private async Task CleanMovieCollection(long collectionId)
        {
            var movies = await mediaLibrary.GetMovies(collectionId);
            foreach (var movie in movies)
                if (!movie.MediaItems.Any())
                    await mediaLibrary.RemoveMovieAsync(movie);
        }

        public void Rescan(MediaItem item)
        {
            ScanQueue.Enqueue(new QueueEntry() { Entry = item, Arguments = new object[] { false } });
            if (!running)
                Start();
        }

        public void Rescan(MediaElementSource mediaSource, bool all)
        {
            ScanQueue.Enqueue(new QueueEntry() { Entry = mediaSource, Arguments = new object[] { all } });
            if (!running)
                Start();
        }

        public void StartCleaning(MediaElementSource mediaSource)
        {
            CleanQueue.Enqueue(mediaSource);
            if (!running)
                Start();
        }

        private struct MetaInfo
        {

            public MediaItem Item { get; set; }

            public MediaInformation Info { get; set; }

        }

        private ConcurrentQueue<MetaInfo> MetaInfoQueue = new ConcurrentQueue<MetaInfo>();

        public void SaveMetaInformation(MediaItem item, MediaInformation metaInfo)
        {
            MetaInfoQueue.Enqueue(new MetaInfo() { Item = item, Info = metaInfo });
            if (!running)
                Start();
        }

        private async Task SaveMetaInformationAsync()
        {
            while (MetaInfoQueue.TryDequeue(out MetaInfo info))
                await SaveMetaInformationAsync(info);
        }

        private async Task SaveMetaInformationAsync(MetaInfo info)
        {
            info.Item = await mediaLibrary.GetMediaItemAsync(info.Item.Id);
            info.Item.MetaDataTime = DateTime.MinValue;
            info.Item.MetaInfo = info.Info;
            await mediaLibrary.AddMediaItemAsync(info.Item);
            Rescan(info.Item);
        }

        public void TestConnection(MediaElementSource mediaSource)
        {
            foreach (var scanner in _Scanners)
            {
                if (scanner.CanScan(mediaSource))
                    if (scanner.TestConnection(mediaSource))
                        return;
            }
            throw new InvalidOperationException();
        }

    }

}
