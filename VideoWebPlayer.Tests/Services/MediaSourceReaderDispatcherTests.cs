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
    public void ReadDirectoryEntries_WithNullMediaSource_ThrowsInvalidOperationException()
    {
        // Regressionstest: Ein nicht geladenes MediaSource-Navigation-Property darf nicht
        // still auf den SFTP-Reader abgebildet werden, sondern muss eine klare
        // InvalidOperationException mit Hinweis auf das erforderliche Eager-Loading werfen.
        var dispatcher = CreateDispatcher();
        var collection = new MediaCollection
        {
            Name = "Collection",
            Path = "/remote/collection",
            MediaSource = null!
        };

        var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.ReadDirectoryEntries(collection).ToList());
        Assert.Contains("MediaSource", ex.Message);
    }

    [Fact]
    public async Task CollectionMethods_WithNullMediaSource_ThrowInvalidOperationException()
    {
        var dispatcher = CreateDispatcher();
        var collection = new MediaCollection
        {
            Name = "Collection",
            Path = "/remote/collection",
            MediaSource = null!
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.FileExistsAsync(collection, "movie.nfo"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.ReadFileAsync(collection, "movie.nfo"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.ReadFileStreamAsync(collection, "movie.nfo"));
        Assert.Throws<InvalidOperationException>(() => dispatcher.OpenFileStream(collection, "movie.nfo"));
    }

    private MediaSourceReaderDispatcher CreateDispatcher()
        => new(new FakeSftpMediaSourceReader("/remote", "marker.mp4"), new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
}
