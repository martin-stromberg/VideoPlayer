using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="LiveStreamMediaResolver"/>: local sources resolve to a physical file
/// path, remote sources preferrably to the range-capable HTTP stream URI (token lifetime
/// sized from the run duration) and otherwise to the streamed stdin input.
/// </summary>
public sealed class LiveStreamMediaResolverTests : IAsyncLifetime
{
    private sealed class StubStreamTokenService : IInternalMediaStreamTokenService
    {
        public string? UriToReturn { get; set; } =
            "http://127.0.0.1:5002/api/internal/media-stream/" + new string('d', 64);
        public int StreamUriCalls { get; private set; }
        public TimeSpan LastLifetime { get; private set; }

        public string? TryCreateProbeUri(long mediaItemId) => null;

        public string? TryCreateStreamUri(long mediaItemId, TimeSpan lifetime)
        {
            StreamUriCalls++;
            LastLifetime = lifetime;
            return UriToReturn;
        }

        public long? ResolveMediaItemId(string token) => null;
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vwp-resolver-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;
    private StubStreamTokenService _streamTokens = null!;
    private LiveStreamOptions _options = null!;
    private LiveStreamMediaResolver _resolver = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"resolver-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        _streamTokens = new StubStreamTokenService();
        _options = new LiveStreamOptions();
        _resolver = CreateResolver();
        Directory.CreateDirectory(_root);
    }

    private LiveStreamMediaResolver CreateResolver()
    {
        var local = new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance);
        return new LiveStreamMediaResolver(_fixture.Db,
            new MediaSourceReaderDispatcher(new SftpMediaSourceReader(), local),
            local, _streamTokens, Options.Create(_options),
            NullLogger<LiveStreamMediaResolver>.Instance);
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

        var input = await _resolver.ResolveAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.Equal(Path.Combine(_root, "f.mkv"), input!.FilePath);
        Assert.False(input.IsPipe);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task ResolveAsync_LocalSourceMissingFile_ReturnsNull()
    {
        var item = await SeedAsync(MediaSourceType.LocalDirectory, "missing.mkv", createFile: false);

        Assert.Null(await _resolver.ResolveAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_RemoteSource_ReturnsHttpUrl()
    {
        var item = await SeedAsync(MediaSourceType.Sftp, "r.mkv");

        var input = await _resolver.ResolveAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.Equal(_streamTokens.UriToReturn, input!.HttpUrl);
        Assert.False(input.IsPipe);
        Assert.Equal(1, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task ResolveAsync_UseHttpInputFalse_ReturnsPipe()
    {
        var item = await SeedAsync(MediaSourceType.Sftp, "r2.mkv");

        var input = await _resolver.ResolveAsync(item.Id, useHttpInput: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.True(input!.IsPipe);
        Assert.Null(input.HttpUrl);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task ResolveAsync_TokenServiceNull_FallsBackToPipe()
    {
        _streamTokens.UriToReturn = null;
        var item = await SeedAsync(MediaSourceType.Sftp, "r3.mkv");

        var input = await _resolver.ResolveAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.True(input!.IsPipe);
        Assert.NotNull(input.OpenStreamAsync);
    }

    [Fact]
    public async Task ResolveAsync_OptionDisabled_FallsBackToPipe()
    {
        _options.UseHttpInput = false;
        var item = await SeedAsync(MediaSourceType.Sftp, "r4.mkv");

        var input = await _resolver.ResolveAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.True(input!.IsPipe);
        Assert.Equal(0, _streamTokens.StreamUriCalls);
    }

    [Fact]
    public async Task ResolveAsync_RunDuration_SizesTokenLifetime()
    {
        var item = await SeedAsync(MediaSourceType.Sftp, "r5.mkv");

        await _resolver.ResolveAsync(item.Id, TimeSpan.FromMinutes(90),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(105), _streamTokens.LastLifetime);
    }

    [Fact]
    public async Task ResolveAsync_UnknownItem_ReturnsNull()
    {
        Assert.Null(await _resolver.ResolveAsync(99999, cancellationToken: TestContext.Current.CancellationToken));
    }
}
