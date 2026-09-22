using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

public class LocalMediaSourceReaderTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _rootDir;
    private readonly string _collectionDir;
    private readonly string _outsideDir;
    private readonly LocalMediaSourceReader _reader = new(NullLogger<LocalMediaSourceReader>.Instance);

    public LocalMediaSourceReaderTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"vwp-localreader-{Guid.NewGuid():N}");
        _rootDir = Path.Combine(_baseDir, "root");
        _collectionDir = Path.Combine(_rootDir, "collection");
        Directory.CreateDirectory(_collectionDir);

        File.WriteAllText(Path.Combine(_baseDir, "outside.txt"), "outside");
        _outsideDir = Path.Combine(Path.GetTempPath(), "vwp-localreader-outside");
        Directory.CreateDirectory(_outsideDir);
        File.WriteAllText(Path.Combine(_outsideDir, "outside.txt"), "outside");
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDir, recursive: true); } catch { }
        try { Directory.Delete(_outsideDir, recursive: true); } catch { }
    }

    [Fact]
    public void ReadRootDirectory_ReturnsLocalRoot()
    {
        var source = CreateSource();

        var root = _reader.ReadRootDirectory(source).OfType<MediaCollection>().Single();

        Assert.Equal(_rootDir, root.Path);
        Assert.Equal(new DirectoryInfo(_rootDir).Name, root.Name);
        Assert.Equal(Directory.GetLastWriteTimeUtc(_rootDir), root.CreatedAt);
        Assert.Null(root.ParentMediaCollectionId);
    }

    [Theory]
    [MemberData(nameof(InvalidSourcePaths))]
    public void ReadRootDirectory_InvalidSourcePath_ReturnsEmpty(string sourcePath)
    {
        // Regressionstest: ein leerer oder ungültiger Quellpfad darf keine
        // Exception werfen, damit der Root-Scan anderer Quellen nicht abbricht.
        var source = new MediaSource
        {
            Name = "Broken",
            Path = sourcePath,
            SourceType = MediaSourceType.LocalDirectory
        };

        var entries = _reader.ReadRootDirectory(source).ToList();

        Assert.Empty(entries);
    }

    [Fact]
    public void ReadRootDirectory_InvalidSourcePath_LogsWarning()
    {
        var messages = new ConcurrentQueue<string>();
        var reader = new LocalMediaSourceReader(new ListLogger<LocalMediaSourceReader>(messages));
        var source = new MediaSource
        {
            Name = "Broken",
            Path = string.Empty,
            SourceType = MediaSourceType.LocalDirectory
        };

        var entries = reader.ReadRootDirectory(source).ToList();

        Assert.Empty(entries);
        Assert.NotEmpty(messages);
    }

    public static TheoryData<string> InvalidSourcePaths()
        => new()
        {
            string.Empty,
            "   ",
            "bad\0path"
        };

    [Fact]
    public void ReadDirectoryEntries_ListsFoldersAndFiles_IgnoresDotEntries()
    {
        Directory.CreateDirectory(Path.Combine(_collectionDir, "SubFolder"));
        File.WriteAllText(Path.Combine(_collectionDir, "movie.mp4"), "video");
        File.WriteAllText(Path.Combine(_collectionDir, ".hidden"), "hidden");

        var collection = CreateCollection();

        var entries = _reader.ReadDirectoryEntries(collection).ToList();

        var folder = Assert.Single(entries.OfType<MediaCollection>());
        Assert.Equal(Path.Combine(_collectionDir, "SubFolder"), folder.Path);
        Assert.Equal("SubFolder", folder.Name);
        Assert.Equal(collection.Id, folder.ParentMediaCollectionId);

        var item = Assert.Single(entries.OfType<MediaItem>());
        Assert.Equal(Path.Combine(_collectionDir, "movie.mp4"), item.Path);
        Assert.Equal("movie.mp4", item.Name);
        Assert.Equal(collection.Id, item.MediaCollectionId);
        Assert.Equal(File.GetLastWriteTimeUtc(item.Path), item.CreatedAt);

        Assert.DoesNotContain(entries, e => e.Name.StartsWith('.'));
    }

    [Fact]
    public void ReadDirectoryEntries_SkipsReparsePoints()
    {
        var outsideDir = Path.Combine(_baseDir, "outside-target");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.txt"), "secret");

        var junctionPath = Path.Combine(_collectionDir, "junction");
        Assert.True(TryCreateJunction(junctionPath, outsideDir), "Junction konnte nicht erstellt werden.");

        File.WriteAllText(Path.Combine(_collectionDir, "regular.txt"), "data");

        var collection = CreateCollection();

        var entries = _reader.ReadDirectoryEntries(collection).ToList();

        Assert.Single(entries);
        Assert.Equal("regular.txt", entries[0].Name);
    }

    [Fact]
    public async Task FileExistsAsync_MatchesCaseInsensitive()
    {
        File.WriteAllText(Path.Combine(_collectionDir, "tvshow.nfo"), "<tvshow />");
        var collection = CreateCollection();

        Assert.True(await _reader.FileExistsAsync(collection, "TVSHOW.NFO"));
        Assert.False(await _reader.FileExistsAsync(collection, "missing.nfo"));
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsContent_AndNullWhenMissing()
    {
        File.WriteAllText(Path.Combine(_collectionDir, "movie.nfo"), "<movie><title>Test</title></movie>");
        var collection = CreateCollection();

        var content = await _reader.ReadFileAsync(collection, "movie.nfo");
        var missing = await _reader.ReadFileAsync(collection, "missing.nfo");

        Assert.Equal("<movie><title>Test</title></movie>", content);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ReadFileAsync_ReadsRelativeSubPath()
    {
        var actorsDir = Path.Combine(_collectionDir, ".actors");
        Directory.CreateDirectory(actorsDir);
        File.WriteAllText(Path.Combine(actorsDir, "jane.jpg"), "image-bytes");
        var collection = CreateCollection();

        var content = await _reader.ReadFileAsync(collection, Path.Combine(".actors", "jane.jpg"));

        Assert.Equal("image-bytes", content);
    }

    [Theory]
    [MemberData(nameof(OutsideRootFileNames))]
    public async Task FileExistsAsync_RejectsTraversalOutsideRoot(string fileName)
    {
        var collection = CreateCollection();

        Assert.False(await _reader.FileExistsAsync(collection, fileName));
    }

    [Theory]
    [MemberData(nameof(OutsideRootFileNames))]
    public async Task ReadFileAsync_RejectsTraversalOutsideRoot(string fileName)
    {
        var collection = CreateCollection();

        Assert.Null(await _reader.ReadFileAsync(collection, fileName));
    }

    [Theory]
    [MemberData(nameof(OutsideRootFileNames))]
    public async Task ReadFileStreamAsync_RejectsTraversalOutsideRoot(string fileName)
    {
        var collection = CreateCollection();

        Assert.Null(await _reader.ReadFileStreamAsync(collection, fileName));
    }

    [Theory]
    [MemberData(nameof(OutsideRootFileNames))]
    public void OpenFileStream_RejectsTraversalOutsideRoot(string fileName)
    {
        var collection = CreateCollection();

        Assert.Null(_reader.OpenFileStream(collection, fileName));
    }

    public static TheoryData<string> OutsideRootFileNames()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "vwp-localreader-outside");
        return new TheoryData<string>
        {
            Path.Combine("..", "..", "outside.txt"),
            Path.Combine(baseDir, "outside.txt")
        };
    }

    [Fact]
    public async Task FileExistsAsync_RejectsReparsePointPath()
    {
        var outsideDir = Path.Combine(_baseDir, "outside-target");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.txt"), "secret");

        var junctionPath = Path.Combine(_collectionDir, "junction");
        Assert.True(TryCreateJunction(junctionPath, outsideDir), "Junction konnte nicht erstellt werden.");

        var collection = CreateCollection();

        Assert.False(await _reader.FileExistsAsync(collection, Path.Combine("junction", "secret.txt")));
        Assert.Null(await _reader.ReadFileAsync(collection, Path.Combine("junction", "secret.txt")));
        Assert.Null(_reader.OpenFileStream(collection, Path.Combine("junction", "secret.txt")));
    }

    [Fact]
    public async Task FileExistsAsync_InvalidFileName_LogsAndReturnsFalse()
    {
        // Regressionstest für fehlendes Fehler-Logging: nicht auflösbare Pfade
        // müssen protokolliert werden statt still geschluckt zu werden.
        var messages = new ConcurrentQueue<string>();
        var reader = new LocalMediaSourceReader(new ListLogger<LocalMediaSourceReader>(messages));
        var collection = CreateCollection();

        var result = await reader.FileExistsAsync(collection, "bad\0name.txt");

        Assert.False(result);
        Assert.NotEmpty(messages);
    }

    [Fact]
    public void OpenFileStream_ReturnsReadableFileStream()
    {
        var filePath = Path.Combine(_collectionDir, "video.mp4");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3, 4 });
        var collection = CreateCollection();

        var stream = _reader.OpenFileStream(collection, "video.mp4");

        Assert.NotNull(stream);
        Assert.IsType<FileStream>(stream);
        Assert.True(stream.CanSeek);
        using (stream)
        {
            var buffer = new byte[4];
            var read = stream.Read(buffer, 0, buffer.Length);
            Assert.Equal(4, read);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
        }

        Assert.Null(_reader.OpenFileStream(collection, "missing.mp4"));
    }

    private MediaSource CreateSource()
        => new()
        {
            Name = "Local Source",
            Path = _rootDir,
            SourceType = MediaSourceType.LocalDirectory
        };

    private MediaCollection CreateCollection()
    {
        var source = CreateSource();
        return new MediaCollection
        {
            Id = 1,
            Name = "collection",
            Path = _collectionDir,
            MediaSource = source,
            MediaSourceId = source.Id
        };
    }

    private static bool TryCreateJunction(string junctionPath, string targetDir)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (process is null)
            return false;
        process.WaitForExit(10_000);
        return process.ExitCode == 0 && Directory.Exists(junctionPath);
    }
}
