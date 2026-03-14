using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using WebPlayerApi.Models;
using WebPlayerApi.Service.Data.SFtp;

namespace WebPlayerApi.Services
{
    public class SourceScanner
    {
        private MediaDirectory source;
        private readonly ILogger logger;
        private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi"];
        private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp"];


        public SourceScanner(MediaDirectory source, ILogger logger)
        {
            this.source = source;
            this.logger = logger;
        }

        protected SFTPSourceReader CreateReader(MediaDirectory dir)
        {
            return new SFTPSourceReader(dir);
        }

        public event EventHandler<MediaItem> ItemScanned;

        internal void Scan(CancellationToken stoppingToken)
        {
            var mediaItemsCollection = new List<MediaItem>();
            var mediaItems = new List<MediaItem>();
            var reader = CreateReader(source);
            var reader2 = CreateReader(source);

            reader.CollectMediaItems((e) =>
            {
                if (stoppingToken.IsCancellationRequested)
                    throw new HostAbortedException();

                logger.LogInformation($"Check File {e.File}.");
                var skipFolder = false;
                var mediaItem = ProcessTVShow(reader2, ref skipFolder, e);
                if (mediaItem is null)
                {
                    mediaItem = ProcessVideoFile(reader2, e);
                    if (mediaItem is null)
                        return;

                    mediaItemsCollection.Add(mediaItem);
                    var folderPaths = mediaItemsCollection.Select(mi => Path.GetDirectoryName(mi.FilePath)).Distinct().ToArray();
                    if (folderPaths.Length > 1)
                    {
                        var items = mediaItemsCollection.Take(mediaItemsCollection.Count - 1).ToArray();
                        mediaItemsCollection.RemoveRange(0, mediaItemsCollection.Count - 1);
                        mediaItem = CheckMovieEntry(reader2, items);
                    }
                    else
                        mediaItem = null;
                    if (mediaItem is null)
                        return;
                }
                e.SkipCurrentFolder = skipFolder;
                mediaItem.Picture = LoadPicture(reader2, mediaItem);
                mediaItem.Source = source;
                mediaItems.Add(mediaItem);
                CompleteItem(mediaItem);

                ItemScanned?.Invoke(this, mediaItem);
            });


        }
        #region Complete
        private void CompleteItem(MediaItem item)
        {
            try
            {
                switch (item.Type)
                {
                    case MediaType.Series:
                        CompleteSeries(item);
                        break;
                    case MediaType.MovieCollection:
                        CompleteMovieCollection(item);
                        break;
                    case MediaType.Movie:
                        CompleteMovie(item);
                        break;
                }
            }
            catch (Exception ex) { logger.LogError(ex, ex.Message); }
        }
        private void CompleteMovie(MediaItem item)
        {
            item.Children = new MediaItem[] { new MediaItem() {
                FilePath = item.FilePath,
                ImagePaths = item.ImagePaths,
                NfoContent = item.NfoContent,
                ParentId = item.ParentId,
                Picture = item.Picture,
                Plot = item.Plot,
                Source = item.Source,
                Title = item.Title,
                Type = item.Type,
                ReleaseDate = item.ReleaseDate,
                Id = Guid.NewGuid().ToString()
            } };
        }
        private void CompleteMovieCollection(MediaItem item)
        {
            var reader = CreateReader(item.Source);
            var reader2 = CreateReader(item.Source);
            var path = item.FilePath;
            if (path.StartsWith(item.Source.Password))
                path = path.Remove(0, item.Source.Path.Length);
            List<MediaItem> items = new List<MediaItem>();
            reader.CollectMediaItems(path.Replace("\\", "/"), (e) =>
            {
                var mediaItem = ProcessMovieFile(reader2, e);
                if (mediaItem is null)
                    return;
                mediaItem.Id = Guid.NewGuid().ToString();
                items.Add(mediaItem);
                if ((mediaItem.ReleaseDate != DateTime.MinValue && item.ReleaseDate > mediaItem.ReleaseDate) || (item.ReleaseDate == DateTime.MinValue))
                    item.ReleaseDate = mediaItem.ReleaseDate;
            });
            item.Children = items.OrderBy(i => i.ReleaseDate).ThenBy(i => i.Title).ToArray();
        }
        private MediaItem ProcessMovieFile(SFTPSourceReader reader2, SFTPSourceReader.ScanResultEventArgs e)
        {
            var mediaItem = ProcessVideoFile(reader2, e);
            if (mediaItem is null) return null;
            if (mediaItem.Type != MediaType.Movie)
                return null;

            var xml = XDocument.Parse(mediaItem.NfoContent);
            var plot = xml.Root?.Element("plot")?.Value;
            var title = xml.Root?.Element("title")?.Value;
            if (string.IsNullOrWhiteSpace(title))
                return mediaItem;
            mediaItem.Plot = plot;
            mediaItem.Title = title;
            var releaseDateText = xml.Root?.Element("releasedate")?.Value;
            if (DateTime.TryParse(releaseDateText, out var releaseDate))
                mediaItem.ReleaseDate = releaseDate;
            mediaItem.Picture = LoadPicture(reader2, mediaItem);
            return mediaItem;
        }
        private void CompleteSeries(MediaItem item)
        {
            var reader = CreateReader(item.Source);
            var reader2 = CreateReader(item.Source);
            var path = Path.GetDirectoryName(item.FilePath);
            var relPath = path.Remove(0, item.Source.Path.Length).Replace("\\", "/");
            List<MediaItem> items = new List<MediaItem>();
            reader.CollectMediaItems(relPath, (e) =>
            {
                var mediaItem = ProcessEpisode(reader2, e);
                if (mediaItem is null)
                    return;
                items.Add(mediaItem);
            });
            item.Children = items.OrderBy(i => i.Title).Select(i => { i.Id = Guid.NewGuid().ToString(); return i; }).ToArray();
        }
        private MediaItem ProcessEpisode(SFTPSourceReader reader2, SFTPSourceReader.ScanResultEventArgs e)
        {
            var media = ProcessVideoFile(reader2, e);
            if (media is null)
                return media;
            if (media.Type != MediaType.Episode)
                return null;

            var xml = XDocument.Parse(media.NfoContent);
            if (!int.TryParse(xml.Root?.Element("season")?.Value, out var season))
                season = -1;
            if (!int.TryParse(xml.Root?.Element("episode")?.Value, out var episode))
                episode = -1;
            var title = xml.Root?.Element("title")?.Value;
            var plot = xml.Root?.Element("plot")?.Value;
            if (string.IsNullOrWhiteSpace(title))
                return media;
            media.Plot = plot;
            media.Title = title;
            if (season > 0 && episode > 0)
                media.Title = $"S{season.ToString().PadLeft(2, '0')}E{episode.ToString().PadLeft(2, '0')} {media.Title}";
            else if (season == 0 && episode > 0)
                media.Title = $"Special {episode.ToString().PadLeft(2, '0')} {media.Title}";
            media.Picture = LoadPicture(reader2, media);
            return media;
        }
        #endregion

        #region Processing
        private string? ReadNfoFile(SFTPSourceReader reader2, string videoFilePath)
        {
            var baseName = Path.Combine(Path.GetDirectoryName(videoFilePath)!, Path.GetFileNameWithoutExtension(videoFilePath)).Replace("\\", "/");
            var nfoPath = baseName + ".nfo";

            try
            {
                var nfo = reader2.ReadTextFile(new MediaItem()
                {
                    FilePath = nfoPath,
                });
                return nfo;
            }
            catch (FileDeletedException)
            {
                return null;
            }
        }
        private byte[] LoadPicture(SFTPSourceReader reader2, MediaItem media)
        {
            foreach (var imagePath in media.ImagePaths)
                try
                {
                    var remotePath = (imagePath).Replace("\\", "/");
                    if (remotePath.StartsWith(reader2.MediaSource.Path))
                        remotePath = remotePath.Remove(0, reader2.MediaSource.Path.Length);
                    var tempFile = reader2.Download(new MediaItem() { FilePath = remotePath }, (p) => { });
                    if (tempFile.Exists)
                        try
                        {
                            return File.ReadAllBytes(tempFile.FullName);
                        }
                        finally
                        {
                            tempFile.Delete();
                        }
                }
                catch { }
            return new byte[0];
        }
        private MediaType DetectMediaType(SFTPSourceReader reader2, string videoFilePath)
        {
            var nfoPath = ReadNfoFile(reader2, videoFilePath);
            if (nfoPath is null) return MediaType.None;

            if (nfoPath != null)
            {
                var xml = XDocument.Parse(nfoPath);
                if (xml.Root?.Name == "movie") return MediaType.Movie;
                if (xml.Root?.Name == "episodedetails") return MediaType.Episode;
                if (xml.Root?.Name == "tvshow") return MediaType.Series;
            }

            var dir = Path.GetDirectoryName(videoFilePath);
            var filesInDir = reader2.ReadFiles(new Service.Data.SourceFolder() { FullPath = dir })
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f.Name).ToLower()))
                                      .ToList();
            return filesInDir.Count > 1 ? MediaType.MovieCollection : MediaType.Movie;
        }
        private MediaItem? ProcessTVShow(SFTPSourceReader reader2, ref bool skipFolder, SFTPSourceReader.ScanResultEventArgs e)
        {
            if (e.File.Name.ToLower() != "tvshow.nfo")
                return null;
            var nfoContent = ReadNfoFile(reader2, e.File.Path);
            var xml = XDocument.Parse(nfoContent);
            var media = new MediaItem
            {
                FilePath = e.File.FullPath,
                Title = xml.Root.Nodes().OfType<XElement>().FirstOrDefault(n => n.Name == "title")?.Value,
                Type = MediaType.Series
            };
            if (string.IsNullOrWhiteSpace(media.Title))
                return null;
            skipFolder = true;
            media.NfoContent = nfoContent;
            media.ImagePaths = FindImageFiles(reader2, e.File.Path);
            return media;
        }
        private MediaItem ProcessVideoFile(SFTPSourceReader reader2, SFTPSourceReader.ScanResultEventArgs e)
        {
            if (!VideoExtensions.Contains(Path.GetExtension(e.File.Name).ToLower()))
                return null;
            var media = new MediaItem
            {
                FilePath = e.File.Path,
                Title = Path.GetFileNameWithoutExtension(e.File.FullPath),
                Type = DetectMediaType(reader2, e.File.Path)
            };
            if (media.Type == MediaType.None)
                return null;
            media.NfoContent = ReadNfoFile(reader2, e.File.Path);
            media.ImagePaths = FindImageFiles(reader2, e.File.Path);
            return media;
        }
        private MediaItem CheckMovieEntry(SFTPSourceReader reader2, MediaItem[] items)
        {
            var firstItem = items.First();
            if (items.Length == 1)
                return firstItem;
            var folderPath = Path.GetDirectoryName(firstItem.FilePath);
            return new MediaItem
            {
                FilePath = folderPath,
                Title = Path.GetFileName(folderPath),
                Type = MediaType.MovieCollection,
                ImagePaths = FindImageFiles(reader2, firstItem.FilePath, true)
            };
        }
        private List<string> FindImageFiles(SFTPSourceReader reader2, string videoFilePath, bool favorizeGeneralFiles = false)
        {
            var dir = Path.GetDirectoryName(videoFilePath)!.Replace("\\", "/");
            var baseName = Path.GetFileNameWithoutExtension(videoFilePath);
            var patterns = new[]
            {
                $"{baseName}.*",
                $"{baseName}-fanart.*",
                $"{baseName}-poster.*",
                $"{baseName}-banner.*",
                $"{baseName}-thumb.*",
                $"{baseName}-fanart-*x*.*",
                $"{baseName}-poster-*x*.*",
                $"{baseName}-banner-*x*.*",
                $"{baseName}-thumb-*x*.*"
            };
            var patternsGeneral = new[]
            {
                "folder.*",
                "poster.*",
                "banner.*"
            };

            var generalFiles = reader2.ReadFiles(new Service.Data.SourceFolder() { FullPath = $"{reader2.MediaSource.Path}{dir}", Path = dir })
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f.Name).ToLower()))
                .Where(f => patternsGeneral.Any(p => Path.GetFileName(f.Name).StartsWith(p.Split('*')[0], StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Path)
                .ToList();
            var currentFiles = reader2.ReadFiles(new Service.Data.SourceFolder() { FullPath = $"{reader2.MediaSource.Path}{dir}", Path = dir })
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f.Name).ToLower()))
                .Where(f => patterns.Any(p => Path.GetFileName(f.Name).StartsWith(p.Split('*')[0], StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Path)
                .ToList();

            if (!currentFiles.Any() || (favorizeGeneralFiles && generalFiles.Any()))
                currentFiles = generalFiles;
            return currentFiles.OrderBy(f =>
            {
                if (f.Contains("poster"))
                    return 1;
                if (f.Contains("fanart"))
                    return 2;
                if (f.Contains("thumb"))
                    return 3;
                if (f.Contains("banner"))
                    return 4;
                return 99;
            })
            .ThenBy(f => f)
            .ToList();
        }
        #endregion
    }
}
