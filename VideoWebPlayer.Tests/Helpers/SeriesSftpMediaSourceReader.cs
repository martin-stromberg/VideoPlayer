using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class SeriesSftpMediaSourceReader : SftpMediaSourceReader
{
    private static readonly Regex EpisodeRegex = new(@"^S(?<season>\d{2})E(?<episode>\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static char[] PathSeparators = new char[] { '/', '\\' };
    private readonly string _rootPath;
    private SourceFolder _rootFolder;

    private struct SourceFolder
    {
        public DateTime CreatedAt;
        public string Name;
        public Dictionary<string, SourceFolder> ChildFolders;
        public List<SourceFile> Files;
    }
    private struct SourceFile
    {
        public DateTime CreatedAt;
        public string Name;
        public byte[] Data;
    }

    public SeriesSftpMediaSourceReader(string rootPath, string showName = "", int seasonCount = 0, int episodesPerSeason = 0)
    {
        _rootPath = rootPath;
        _rootFolder = new SourceFolder()
        {
            Name = rootPath.Split("/\\").Last(),
            CreatedAt = DateTime.UtcNow,
            ChildFolders = new Dictionary<string, SourceFolder>(),
            Files = new List<SourceFile>()
        };
        if (!string.IsNullOrWhiteSpace(showName))
            AddShow(showName, seasonCount, episodesPerSeason);        
    }
    public void AddShow(string showName, int seasonCount = 0, int episodesPerSeason = 0)
    {
        _rootFolder.ChildFolders.Add(showName, new SourceFolder()
        {
            Name = showName,
            CreatedAt = DateTime.UtcNow,
            ChildFolders = new Dictionary<string, SourceFolder>(),
            Files = new List<SourceFile>()
            {
                new SourceFile()
                {
                    Name = "tvshow.nfo",
                    CreatedAt = DateTime.UtcNow,
                    Data = System.Text.Encoding.UTF8.GetBytes("<tvshow><title>Test Show</title></tvshow>")
                },
                new SourceFile() 
                {
                    Name = "poster.jpg",
                    CreatedAt = DateTime.UtcNow,
                    Data = GetDummyPicture()
                }
            }
        });
        for (;  seasonCount > 0; seasonCount-- )
            AddSeason(showName, episodesPerSeason);
    }

    private byte[] GetDummyPicture()
    {
        return new byte[]
        {
            // BMP file header (14 bytes)
            0x42,0x4D,             // 'B','M'
            0x5A,0x00,0x00,0x00,   // file size = 90 bytes
            0x00,0x00,             // reserved
            0x00,0x00,             // reserved
            0x36,0x00,0x00,0x00,   // offset to pixel data = 54
            // DIB header (BITMAPINFOHEADER, 40 bytes)
            0x28,0x00,0x00,0x00,   // header size = 40
            0x03,0x00,0x00,0x00,   // width = 3
            0x03,0x00,0x00,0x00,   // height = 3
            0x01,0x00,             // planes = 1
            0x18,0x00,             // bit count = 24 (RGB)
            0x00,0x00,0x00,0x00,   // compression = 0 (BI_RGB)
            0x24,0x00,0x00,0x00,   // image size = 36 bytes
            0x00,0x00,0x00,0x00,   // X pixels per meter
            0x00,0x00,0x00,0x00,   // Y pixels per meter
            0x00,0x00,0x00,0x00,   // colors used
            0x00,0x00,0x00,0x00,   // important colors
            // Pixel data (bottom-to-top rows), each row padded to 4 bytes
            // Bottom row: red pixels (B,G,R) = 00,00,FF
            0x00,0x00,0xFF, 0x00,0x00,0xFF, 0x00,0x00,0xFF, 0x00,0x00,0x00,
            // Middle row: green pixels = 00,FF,00
            0x00,0xFF,0x00, 0x00,0xFF,0x00, 0x00,0xFF,0x00, 0x00,0x00,0x00,
            // Top row: blue pixels = FF,00,00
            0xFF,0x00,0x00, 0xFF,0x00,0x00, 0xFF,0x00,0x00, 0x00,0x00,0x00
        };
    }

    public void AddSeason(string showName, int episodesPerSeason = 0)
    {
        var maxSeasonNo = _rootFolder.ChildFolders[showName].ChildFolders.Keys
            .Where(k => k.StartsWith("Season", StringComparison.OrdinalIgnoreCase) && int.TryParse(k.Substring(6), out _))
            .Select(k => Convert.ToInt32(k.Substring(6)))
            .DefaultIfEmpty(0)
            .Max();
        var newSeasonNo = maxSeasonNo + 1;
        var seasonName = $"Season{newSeasonNo.ToString("00")}";
        _rootFolder.ChildFolders[showName].ChildFolders.Add(seasonName, new SourceFolder()
        {
            Name = seasonName,
            CreatedAt = DateTime.UtcNow,
            ChildFolders = new Dictionary<string, SourceFolder>(),
            Files = new List<SourceFile>()
        });
        // Update timestamps so external scanners detect a change on the show/root level
        _rootFolder.CreatedAt = DateTime.UtcNow;
        if (_rootFolder.ChildFolders.TryGetValue(showName, out var showFolder))
        {
            showFolder.CreatedAt = DateTime.UtcNow;
            _rootFolder.ChildFolders[showName] = showFolder;
        }
        for (; episodesPerSeason > 0; episodesPerSeason--)
            AddEpisode(showName, newSeasonNo);
    }

    private void AddEpisode(string showName, int newSeasonNo)
    {
        var maxEpisodeNo = _rootFolder.ChildFolders[showName].ChildFolders[$"Season{newSeasonNo:00}"].Files
            .Where(f => EpisodeRegex.IsMatch(Path.GetFileNameWithoutExtension(f.Name)))
            .Select(f => Convert.ToInt32(EpisodeRegex.Match(Path.GetFileNameWithoutExtension(f.Name)).Groups["episode"].Value))
            .DefaultIfEmpty(0)
            .Max();
        var newEpisodeNo = maxEpisodeNo + 1;
        var baseName = $"S{newSeasonNo:00}E{newEpisodeNo:00}";
        _rootFolder.ChildFolders[showName].ChildFolders[$"Season{newSeasonNo:00}"].Files.AddRange(new[]
        {
            new SourceFile()
            {
                Name = $"{baseName}.mp4",
                CreatedAt = DateTime.UtcNow,
                Data = new byte[] { 1 }
            },
            new SourceFile()
            {
                Name = $"{baseName}.nfo",
                CreatedAt = DateTime.UtcNow,
                Data = System.Text.Encoding.UTF8.GetBytes($"<episodedetails><title>Episode {newEpisodeNo}</title><season>{newSeasonNo}</season><episode>{newEpisodeNo}</episode></episodedetails>")
            },
            new SourceFile()
            {
                Name = $"{baseName}-thumb.jpg",
                CreatedAt = DateTime.UtcNow,
                Data = GetDummyPicture()
            }
        });
    }

    public override IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
    {
        yield return new MediaCollection
        {
            Name = string.IsNullOrEmpty(_rootPath) ? "/" : _rootFolder.Name,
            Path = _rootPath,
            CreatedAt = _rootFolder.CreatedAt,
            MediaSource = source,
            MediaSourceId = source.Id,
            ParentMediaCollectionId = null
        };
    }

    public override IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
    {
        var currentFolder = GetCollectionFolder(collection);
        if (currentFolder is null)
        {
            yield break;
        }

        foreach (var folder in currentFolder.Value.ChildFolders.Values)
        {
            yield return new MediaCollection
            {
                Name = folder.Name,
                Path = $"{collection.Path}/{folder.Name}",
                CreatedAt = folder.CreatedAt,
                MediaSource = collection.MediaSource,
                MediaSourceId = collection.MediaSourceId,
                ParentMediaCollectionId = collection.Id
            };
        }
        foreach (var file in currentFolder.Value.Files)
        {
            yield return new MediaItem
            {
                Name = file.Name,
                Path = $"{collection.Path}/{file.Name}",
                CreatedAt = file.CreatedAt,
                MediaCollectionId = collection.Id
            };
        }
    }

    private SourceFolder? GetCollectionFolder(MediaCollection collection)
    {
        var rootCollection = ReadRootDirectory(collection.MediaSource).First();
        if (collection.Path.StartsWith(rootCollection.Path, StringComparison.OrdinalIgnoreCase))
        {
            var remainingPath = collection.Path.Substring(rootCollection.Path.Length).TrimStart(PathSeparators).ToString();
            var pathParts = remainingPath.Split(PathSeparators);
            var currentFolder = _rootFolder;
            if (!string.IsNullOrWhiteSpace(remainingPath))
                foreach (var pathPart in pathParts)
                {
                    if (!currentFolder.ChildFolders.TryGetValue(pathPart, out var nextFolder))
                    {
                        return null;
                    }
                    currentFolder = nextFolder;
                }
            return currentFolder;
        }
        return null;
    }
    private SourceFile? GetFile(MediaCollection collection, string fileName)
    {
        var folder = GetCollectionFolder(collection);
        if (folder is null)
            return null;

        // FirstOrDefault returns the default(struct) when not found which is not null for a nullable
        // struct. Detect that case and return null so callers don't receive a SourceFile with null data.
        var file = folder.Value.Files.FirstOrDefault(f => string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(file.Name))
            return null;
        return file;
    }

    public override Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
    {
        var file = GetFile(collection, fileName);
        return Task.FromResult(file is not null);
    }

    public override Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
    {
        var file = GetFile(collection, fileName);
        if (file is null || file.Value.Data is null)
            return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(System.Text.Encoding.UTF8.GetString(file.Value.Data));
    }

    public override Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
    {
        var file = GetFile(collection, fileName);
        if (file is null || file.Value.Data is null)
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new MemoryStream(file.Value.Data));
    }

}
