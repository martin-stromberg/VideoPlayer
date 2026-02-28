using System.IO;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class FakeSftpMediaSourceReader : SftpMediaSourceReader
{
    private readonly string _rootPath;
    private readonly string _fileName;

    public FakeSftpMediaSourceReader(string rootPath, string fileName)
    {
        _rootPath = rootPath;
        _fileName = fileName;
    }

    public override IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
    {
        yield return new MediaCollection
        {
            Name = "Root",
            Path = _rootPath,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source,
            MediaSourceId = source.Id,
            ParentMediaCollectionId = null
        };
    }

    public override IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
    {
        yield return new MediaItem
        {
            Name = _fileName,
            Path = $"{collection.Path}/{_fileName}",
            CreatedAt = DateTime.UtcNow,
            MediaCollectionId = collection.Id
        };
    }

    public override Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult(false);
    }

    public override Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult<string?>(null);
    }

    public override Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult<Stream?>(null);
    }
}
