using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="MediaDurationProbeService"/> against a real (shared in-memory) SQLite database.
/// </summary>
public class MediaDurationProbeServiceTests : IDisposable
{
    private readonly string _rootDir;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly FakeDurationProbe _probe = new();
    private readonly FakeFfprobeLocator _locator = new();
    private readonly StubMediaSourceReader _reader = new();

    public MediaDurationProbeServiceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-duration-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);

        var connectionString = $"Data Source=file:duration-probe-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton(_connection);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(_reader);
        services.AddSingleton<IMediaDurationProbe>(_probe);
        services.AddSingleton<IFfprobeLocator>(_locator);
        services.AddScoped<MediaDurationProbeService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<MediaDurationProbeService>>(
            NullLogger<MediaDurationProbeService>.Instance);

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ProbePendingAsync_ProbesPendingVideoItems_WritesDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("movie.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = TimeSpan.FromMinutes(95);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, processed);
        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Equal(TimeSpan.FromMinutes(95), reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
    }

    [Fact]
    public async Task ProbePendingAsync_FailedProbe_MarksProbedWithoutDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("broken.mkv", MediaSourceType.LocalDirectory);
        _probe.FileResult = null;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);

        // A failed probe is not retried.
        _probe.FileCalls = 0;
        Assert.Equal(0, await service.ProbePendingAsync(10, ct));
        Assert.Equal(0, _probe.FileCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_SkipsAlreadyProbedItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("known.avi", MediaSourceType.LocalDirectory);
        item.Duration = TimeSpan.FromMinutes(42);
        item.DurationProbedAt = DateTime.UtcNow.AddDays(-1);
        await SaveAsync(ct);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(0, await service.ProbePendingAsync(10, ct));
        Assert.Equal(0, _probe.FileCalls);
        Assert.Equal(0, _probe.StreamCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_SkipsNonVideoItems()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedItemAsync("movie.nfo", MediaSourceType.LocalDirectory);
        await SeedItemAsync("poster.jpg", MediaSourceType.LocalDirectory);
        var video = await SeedItemAsync("movie.mp4", MediaSourceType.LocalDirectory);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, processed);
        Assert.NotNull((await ReloadAsync(video.Id, ct)).DurationProbedAt);
    }

    [Fact]
    public async Task ProbePendingAsync_LocalSource_UsesFileProbe()
    {
        var ct = TestContext.Current.CancellationToken;
        var filePath = Path.Combine(_rootDir, "clip.mp4");
        await File.WriteAllTextAsync(filePath, "video", ct);
        var item = await SeedItemAsync("clip.mp4", MediaSourceType.LocalDirectory, filePath);
        _probe.FileResult = TimeSpan.FromSeconds(30);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, _probe.FileCalls);
        Assert.Equal(filePath, _probe.LastFilePath);
        Assert.Equal(0, _probe.StreamCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_StreamsViaOpenFileStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("episode.mkv", MediaSourceType.Sftp);
        _reader.StreamToReturn = new MemoryStream(new byte[] { 1, 2, 3 });
        _probe.StreamResult = TimeSpan.FromMinutes(22);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(0, _probe.FileCalls);
        Assert.Equal(1, _probe.StreamCalls);
        Assert.Equal(1, _reader.OpenFileStreamCalls);
        Assert.Equal(item.Name, _reader.LastFileName);
        Assert.Equal(TimeSpan.FromMinutes(22), (await ReloadAsync(item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_NullStream_MarksFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("gone.mp4", MediaSourceType.Sftp);
        _reader.StreamToReturn = null;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
        Assert.Equal(0, _probe.StreamCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_ProbeThrows_MarksFailedAndContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var failing = await SeedItemAsync("a-throws.mp4", MediaSourceType.LocalDirectory);
        var succeeding = await SeedItemAsync("b-works.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = TimeSpan.FromMinutes(5);
        _probe.ThrowOnPathContaining = "a-throws";

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(2, processed);
        var failedItem = await ReloadAsync(failing.Id, ct);
        Assert.Null(failedItem.Duration);
        Assert.NotNull(failedItem.DurationProbedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), (await ReloadAsync(succeeding.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_NoFfprobe_LeavesItemsUnprobed()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("movie.mp4", MediaSourceType.LocalDirectory);
        _locator.Path = null;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(0, processed);
        Assert.Equal(0, _probe.FileCalls);
        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.DurationProbedAt);
        Assert.Null(reloaded.Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_RespectsBatchSize()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 4; i++)
            await SeedItemAsync($"movie-{i}.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = TimeSpan.FromMinutes(10);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(2, await service.ProbePendingAsync(2, ct));
        Assert.Equal(2, _probe.FileCalls);

        Assert.Equal(2, await service.ProbePendingAsync(2, ct));
        Assert.Equal(0, await service.ProbePendingAsync(2, ct));
    }

    private async Task<MediaItem> SeedItemAsync(string name, MediaSourceType sourceType, string? path = null)
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.MediaSources.FirstOrDefaultAsync(s => s.SourceType == sourceType);
        if (source is null)
        {
            source = new MediaSource
            {
                Name = $"Source-{sourceType}",
                Path = _rootDir,
                SourceType = sourceType,
                CreatedAt = DateTime.UtcNow
            };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync();
        }

        var collection = await db.MediaCollections.FirstOrDefaultAsync(c => c.MediaSourceId == source.Id);
        if (collection is null)
        {
            collection = new MediaCollection
            {
                Name = "root",
                Path = _rootDir,
                CreatedAt = DateTime.UtcNow,
                MediaSource = source,
                MediaSourceId = source.Id
            };
            db.MediaCollections.Add(collection);
            await db.SaveChangesAsync();
        }

        var item = new MediaItem
        {
            Name = name,
            Path = path ?? Path.Combine(_rootDir, name),
            CreatedAt = DateTime.UtcNow,
            MediaCollectionId = collection.Id
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.SaveChangesAsync(ct);
    }

    private async Task<MediaItem> ReloadAsync(long id, CancellationToken ct)
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();
        return await db.MediaItems.SingleAsync(i => i.Id == id, ct);
    }

    private sealed class FakeDurationProbe : IMediaDurationProbe
    {
        public TimeSpan? FileResult { get; set; } = TimeSpan.FromMinutes(60);
        public TimeSpan? StreamResult { get; set; } = TimeSpan.FromMinutes(60);
        public int FileCalls { get; set; }
        public int StreamCalls { get; set; }
        public string? LastFilePath { get; private set; }
        public string? ThrowOnPathContaining { get; set; }

        public Task<TimeSpan?> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
        {
            FileCalls++;
            LastFilePath = filePath;
            if (ThrowOnPathContaining is not null && filePath.Contains(ThrowOnPathContaining, StringComparison.Ordinal))
                throw new IOException("Simulated probe failure.");
            return Task.FromResult(FileResult);
        }

        public Task<TimeSpan?> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
        {
            StreamCalls++;
            return Task.FromResult(StreamResult);
        }
    }

    private sealed class FakeFfprobeLocator : IFfprobeLocator
    {
        public string? Path { get; set; } = "ffprobe";
        public string? Resolve() => Path;
    }

    private sealed class StubMediaSourceReader : IMediaSourceReader
    {
        public Stream? StreamToReturn { get; set; }
        public int OpenFileStreamCalls { get; private set; }
        public string? LastFileName { get; private set; }

        public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => Enumerable.Empty<MediaEntry>();
        public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => Enumerable.Empty<MediaEntry>();
        public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);
        public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);
        public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);

        public Stream? OpenFileStream(MediaCollection collection, string fileName)
        {
            OpenFileStreamCalls++;
            LastFileName = fileName;
            return StreamToReturn;
        }
    }
}
