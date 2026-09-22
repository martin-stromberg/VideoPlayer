using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public class MediaSourceReaderDispatcherTests : IDisposable
{
    private readonly string _rootDir;

    public MediaSourceReaderDispatcherTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-dispatcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public void ReadRootDirectory_WithLocalDirectorySource_UsesLocalReader()
    {
        var dispatcher = CreateDispatcher();
        var source = new MediaSource
        {
            Name = "Local",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory
        };

        var root = dispatcher.ReadRootDirectory(source).OfType<MediaCollection>().Single();

        Assert.Equal(_rootDir, root.Path);
        Assert.Equal(new DirectoryInfo(_rootDir).Name, root.Name);
        Assert.Equal(Directory.GetLastWriteTimeUtc(_rootDir), root.CreatedAt);
    }

    [Fact]
    public void ReadRootDirectory_WithSftpSource_UsesSftpReader()
    {
        var dispatcher = CreateDispatcher();
        var source = new MediaSource
        {
            Name = "Sftp",
            Path = "/remote",
            SourceType = MediaSourceType.Sftp
        };

        var root = dispatcher.ReadRootDirectory(source).OfType<MediaCollection>().Single();

        Assert.Equal("Root", root.Name);
        Assert.Equal("/remote", root.Path);
    }

    [Fact]
    public void ReadDirectoryEntries_WithNullMediaSource_UsesSftpReader()
    {
        var dispatcher = CreateDispatcher();
        var collection = new MediaCollection
        {
            Name = "Collection",
            Path = "/remote/collection",
            MediaSource = null!
        };

        var entries = dispatcher.ReadDirectoryEntries(collection).ToList();

        var item = Assert.Single(entries.OfType<MediaItem>());
        Assert.Equal("marker.mp4", item.Name);
    }

    private MediaSourceReaderDispatcher CreateDispatcher()
        => new(new FakeSftpMediaSourceReader("/remote", "marker.mp4"), new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
}
