using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="LiveStreamMediaResolver"/>: local sources resolve to a physical file
/// path, remote sources to a streamed input, missing media to <see langword="null"/>.
/// </summary>
public sealed class LiveStreamMediaResolverTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vwp-resolver-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;
    private LiveStreamMediaResolver _resolver = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"resolver-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        var local = new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance);
        _resolver = new LiveStreamMediaResolver(_fixture.Db,
            new MediaSourceReaderDispatcher(new SftpMediaSourceReader(), local),
            local, NullLogger<LiveStreamMediaResolver>.Instance);
        Directory.CreateDirectory(_root);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        await Task.CompletedTask;
    }

    private async Task<MediaItem> SeedAsync(MediaSourceType type, string fileName, bool createFile = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = type == MediaSourceType.LocalDirectory ? _root : "/remote";
        var source = new MediaSource { Name = $"S-{Guid.NewGuid():N}", Path = path, SourceType = type, Host = "h", Port = 22 };
        _fixture.Db.MediaSources.Add(source);
        await _fixture.Db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "C", MediaSourceId = source.Id, Path = path, CreatedAt = DateTime.UtcNow };
        _fixture.Db.MediaCollections.Add(collection);
        await _fixture.Db.SaveChangesAsync(ct);
        var item = new MediaItem { Name = fileName, Path = Path.Combine(path, fileName), MediaCollectionId = collection.Id, CreatedAt = DateTime.UtcNow };
        _fixture.Db.MediaItems.Add(item);
        await _fixture.Db.SaveChangesAsync(ct);
        if (createFile && type == MediaSourceType.LocalDirectory)
            await File.WriteAllTextAsync(Path.Combine(_root, fileName), "v", ct);
        return item;
    }

    [Fact]
    public async Task ResolveAsync_LocalSource_ReturnsFilePath()
    {
        var item = await SeedAsync(MediaSourceType.LocalDirectory, "f.mkv");

        var input = await _resolver.ResolveAsync(item.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.Equal(Path.Combine(_root, "f.mkv"), input!.FilePath);
        Assert.False(input.IsPipe);
    }

    [Fact]
    public async Task ResolveAsync_LocalSourceMissingFile_ReturnsNull()
    {
        var item = await SeedAsync(MediaSourceType.LocalDirectory, "missing.mkv", createFile: false);

        Assert.Null(await _resolver.ResolveAsync(item.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_SftpSource_ReturnsStreamInput()
    {
        var item = await SeedAsync(MediaSourceType.Sftp, "r.mkv");

        var input = await _resolver.ResolveAsync(item.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.True(input!.IsPipe);
        Assert.NotNull(input.OpenStreamAsync);
    }

    [Fact]
    public async Task ResolveAsync_UnknownItem_ReturnsNull()
    {
        Assert.Null(await _resolver.ResolveAsync(99999, TestContext.Current.CancellationToken));
    }
}
