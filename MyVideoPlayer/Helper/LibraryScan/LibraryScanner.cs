#if IOS || ANDROID || MACCATALYST
#elif WINDOWS
#endif
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using VideoPlayerLib;
using VideoPlayerLib.Extensions;
using VideoPlayerLib.Services.Common;
using VideoPlayerLib.Services.FTP;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;
using VideoPlayerLib.Services.MediaLibrary.Models.Meta;
using VideoPlayerLib.Services.Samba;

namespace MyVideoPlayer.Helper.LibraryScan
{
    public class LibraryScanner: ILibraryScanner
    {

        private readonly IServiceProvider serviceProvider;
        private readonly IMediaLibrary mediaLibrary;
        private readonly ILogger<LibraryScanner> logger;
        private readonly LibraryScannerSettings settings;
        private string[] FileExtVideo = { ".avi", ".mkv", ".mp4", ".mov" };
        private string[] FileExtAudio = { ".mp3", ".wav", ".ogg" };
        private string[] FileExtImage = { ".jpg", ".png" };

        public LibraryScanner(
            IServiceProvider serviceProvider,
            IMediaLibrary mediaLibrary,
            ILogger<LibraryScanner> logger,
            LibraryScannerSettings settings)
        {
            this.serviceProvider = serviceProvider;
            this.mediaLibrary = mediaLibrary;
            this.logger = logger;
            this.settings = settings;
        }

        private BackgroundWorker scanner = null;
        private bool stopScan = false;

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
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
        }

        private async void Scanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (stopScan)
                return;
            await Task.Delay(1000);
            if (!stopScan)
                Start();
        }

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
            stopScan = true;
        }

        public event EventHandler<MessageEventArgs> StatusChanged;

        private void OnStatusChanged(string statusMessage)
        {
            logger.LogInformation(statusMessage);
            StatusChanged?.Invoke(this, new MessageEventArgs(statusMessage));
        }

        private void ScanNextSource()
        {
            var source = mediaLibrary
                .GetSourcesAsync()
                .Wait<IEnumerable<MediaSource>>()
                .OrderBy(s => s.LastScan)
                .FirstOrDefault();
            if (source == null)
                return;
            if (source.LastScan.AddHours(24) >= DateTime.Now)
            {
                OnStatusChanged(string.Empty);
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

                if (source is SmbMediaSource)
                    ScanSambaSource(source as SmbMediaSource);
                if (source is FtpMediaSource)
                    ScanFtp(source as FtpMediaSource);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                OnStatusChanged(ex.Message);
            }
            finally
            {
                logger.LogInformation($"Scan of source {source.Name} finished.");
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
                await ScannCollectionMetaDataAsync(scanner, collection);
        }

        private async Task ScannCollectionMetaDataAsync(RemoteSourceScanner scanner, MediaItemCollection collection)
        {
            await ProcessMetaDataForFolderAsync(scanner, collection);
            await ProcessPictureForFolderAsync(scanner, collection);
            collection.MetaDataTime = DateTime.Now;
            await mediaLibrary.AddMediaItemCollectionAsync(collection);
        }

        #region samba Share
        private void ScanSambaSource(SmbMediaSource source)
        {
            SambaShare share = new SambaShare(source.ServerName, source.Username, source.Password);
            SambaShareScanner smbScanner = new SambaShareScanner(share);
            ScanRemoteSource(smbScanner, source);
        }

        private async Task ScanNextSmbShareCollectionMediaAsync(SmbMediaSource smbSource, DateTime refDate)
        {
            SambaShare share = new SambaShare(smbSource.ServerName, smbSource.Username, smbSource.Password);
            SambaShareScanner smbScanner = new SambaShareScanner(share);
            try
            {
                share.Connect();
                await ScanRemoteCollectionMediaSyncAsync(smbScanner, smbSource, refDate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                share.Disconnect();
            }
        }
        #endregion

        #region Ftp
        private void ScanFtp(FtpMediaSource source)
        {
            FtpShare share = new FtpShare(source.ServerName, source.Username, source.Password);
            FtpScanner scanner = new FtpScanner(share);
            ScanRemoteSource(scanner, source);
        }

        private async Task ScanNextFtpShareCollectionMediaAsync(FtpMediaSource source, DateTime refDate)
        {
            FtpShare share = new FtpShare(source.ServerName, source.Username, source.Password);
            FtpScanner smbScanner = new FtpScanner(share);
            await ScanRemoteCollectionMediaSyncAsync(smbScanner, source, refDate);
        }
        #endregion

        private async Task FindRemovedFiles(RemoteMediaSource source, RemoteSourceScanner scanner)
        {
            var collections = await mediaLibrary.GetMediaItemCollectionsAsync(source.Id);
            foreach (var collection in collections)
            {
                OnStatusChanged($"{collection.Path}");
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

        private void ScanRemoteSource(RemoteSourceScanner scanner, RemoteMediaSource source)
        {
            if (stopScan)
                return;
            var currSkipPath = source.LatestScanPath?.Remove(0, source.Path.Length);
            var skipPathParts = source.LatestScanPath?.Remove(0, source.Path.Length).Split(source.PathDelimiter);
            var latestScanPathReached = skipPathParts == null;
            scanner.BeforeScanFolder += (sender, e) =>
            {
                if (stopScan)
                    throw new ApplicationException("Scan canceled");
                var isParentLevelFolder = false;
                if (!latestScanPathReached)
                    if (skipPathParts == null)
                        latestScanPathReached = true;
                    else
                    {
                        var currRelPath = e.Value.Remove(0, source.Path.Length);
                        isParentLevelFolder = currSkipPath.StartsWith(currRelPath);
                        latestScanPathReached = currSkipPath == currRelPath;
                    }

                e.ScanFolders = latestScanPathReached || isParentLevelFolder;
                e.ScanFiles = latestScanPathReached;
            };
            scanner.FileFound += (sender, e) =>
            {
                if (stopScan)
                    throw new ApplicationException("Scan canceled");
                ProcessFile(source, scanner, e.File).Wait();
            };
            scanner.FolderFound += (sender, e) =>
            {
                if (stopScan)
                    throw new ApplicationException("Scan canceled");
                OnStatusChanged($"{e.Folder.Path}");
                ProcessFolderAsync(source, scanner, e.Folder).Wait();

                if (latestScanPathReached)
                    source.LatestScanPath = e.Folder.Path;
                mediaLibrary.AddSourceAsync(source).Wait();
            };
            scanner.ScanCompleted += (sender, e) =>
            {
                source.LatestScanPath = null;
                mediaLibrary.AddSourceAsync(source).Wait();

                if (!stopScan)
                    FindRemovedFiles(source, scanner).Wait();
            };
            scanner.Scan(source.Path);
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
                                                          new Folder()
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
                Title = documentElement.FindChild("title", true).InnerText,
                Genre = documentElement.FindChild("genre", true).InnerText,
                Plot = documentElement.FindChild("plot", true).InnerText,
            };
            item.MetaInfo = Info;
        }

        private void ProcessEposideInformation(MediaItem item, XmlElement documentElement)
        {
            EpisodeInformation Info = new EpisodeInformation()
            {
                Title = documentElement.FindChild("title", true).InnerText,
                ShowName = documentElement.FindChild("showname", true).InnerText,
                Episode = documentElement.FindChild("episode", true).InnerText,
                Season = documentElement.FindChild("season", true).InnerText,
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
            string cachFile = Path.Combine(settings.CacheFolderPath, cacheFileName);
            scanner.DownloadFile(picPath, cachFile);
            item.PicturePath = cachFile;
            await mediaLibrary.AddMediaItemAsync(item);
        }

        private void ProcessMetaDataForAudio(MediaItem item)
        {
            logger.LogDebug($"Processing of meta data for audio is not implemented.");
        }

        private async Task<MediaItemCollection> ProcessFolderAsync(
            RemoteMediaSource source,
            RemoteSourceScanner scanner,
            Folder folder)
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
                                                                    new Folder()
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
                    await ScannCollectionMetaDataAsync(scanner, item);
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
            string cachFile = Path.Combine(settings.CacheFolderPath, cacheFileName);
            scanner.DownloadFile(picPath, cachFile);
            item.PicturePath = cachFile;
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
            string tempFile = Path.Combine(settings.TempFolderPath, nfoName);
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
                Title = documentElement.FindChild("title", true).InnerText,
                Plot = documentElement.FindChild("plot", true).InnerText,
            };
            item.MetaInfo = info;
        }

    }

}
