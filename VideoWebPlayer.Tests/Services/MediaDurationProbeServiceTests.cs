using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
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
    private readonly FakeScheduleSignal _signal = new();
    private readonly MediaProbeOptions _probeOptions = new();

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
        services.AddSingleton<IProgramScheduleSignal>(_signal);
        services.AddSingleton(Options.Create(_probeOptions));
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
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(95));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, processed);
        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Equal(TimeSpan.FromMinutes(95), reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
        Assert.Equal(0, reloaded.DurationProbeAttempts);
    }

    [Fact]
    public async Task ProbePendingAsync_SuccessfulProbe_NotifiesScheduleSignal()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedItemAsync("movie.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(95));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, _signal.NotifyCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_FailedProbe_DoesNotNotifyScheduleSignal()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedItemAsync("broken.mkv", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.TransientFailure;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(0, _signal.NotifyCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_TransientFailure_RetriedAfterDelay()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("slow.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.TransientFailure;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(1, await service.ProbePendingAsync(10, ct));

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
        Assert.Equal(1, reloaded.DurationProbeAttempts);

        // The retry delay has not elapsed -> the item is not probed again.
        _probe.FileCalls = 0;
        Assert.Equal(0, await service.ProbePendingAsync(10, ct));
        Assert.Equal(0, _probe.FileCalls);

        // Once the delay has elapsed the item is picked up again.
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct)).DurationProbedAt =
            DateTime.UtcNow.AddSeconds(-_probeOptions.EffectiveRetryDelaySeconds - 1);
        await db.SaveChangesAsync(ct);

        Assert.Equal(1, await service.ProbePendingAsync(10, ct));
        Assert.Equal(1, _probe.FileCalls);
        reloaded = await ReloadAsync(item.Id, ct);
        Assert.Equal(2, reloaded.DurationProbeAttempts);
    }

    [Fact]
    public async Task ProbePendingAsync_LegacyFailedItem_IsPickedUpAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        // Simulates an item marked failed by the pre-retry implementation:
        // DurationProbedAt set, Duration null, no attempt counter yet.
        var item = await SeedItemAsync("legacy.mp4", MediaSourceType.LocalDirectory);
        item.DurationProbedAt = DateTime.UtcNow.AddDays(-1);
        await SaveAsync(ct);

        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(80));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(1, await service.ProbePendingAsync(10, ct));

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Equal(TimeSpan.FromMinutes(80), reloaded.Duration);
        Assert.Equal(0, reloaded.DurationProbeAttempts);
        Assert.Equal(1, _signal.NotifyCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_MaxAttemptsReached_NotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("exhausted.mp4", MediaSourceType.LocalDirectory);
        item.DurationProbedAt = DateTime.UtcNow.AddDays(-7);
        item.DurationProbeAttempts = _probeOptions.EffectiveMaxAttempts;
        await SaveAsync(ct);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(0, await service.ProbePendingAsync(10, ct));
        Assert.Equal(0, _probe.FileCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_PermanentFailure_ExhaustsAttemptsAndStops()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("no-duration.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.PermanentFailure;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(1, await service.ProbePendingAsync(10, ct));

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.True(reloaded.DurationProbeAttempts >= _probeOptions.EffectiveMaxAttempts);

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
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromSeconds(30));

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
        // Quelle ohne Credentials → sftp://-URL ist nicht baubar, Pipe-Fallback greift.
        var item = await SeedItemAsync("episode.mkv", MediaSourceType.Sftp);
        _reader.StreamToReturn = new MemoryStream(new byte[] { 1, 2, 3 });
        _probe.StreamResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(22));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(0, _probe.FileCalls);
        Assert.Equal(0, _probe.UriCalls);
        Assert.Equal(1, _probe.StreamCalls);
        Assert.Equal(1, _reader.OpenFileStreamCalls);
        Assert.Equal(item.Name, _reader.LastFileName);
        Assert.Equal(TimeSpan.FromMinutes(22), (await ReloadAsync(item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_WithCredentials_ProbesViaUri()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedSftpItemAsync("episode.mkv", "/remote/media/episode.mkv");
        _probe.UriResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(22));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, _probe.UriCalls);
        Assert.Equal(0, _probe.StreamCalls);
        Assert.Equal(0, _reader.OpenFileStreamCalls);
        Assert.Equal(
            "sftp://user:s3cret@nas.local:22/remote/media/episode.mkv",
            _probe.LastUri);
        Assert.Equal(TimeSpan.FromMinutes(22), (await ReloadAsync(item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_UriUnsupported_FallsBackToStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedSftpItemAsync("episode.mkv", "/remote/media/episode.mkv");
        _reader.StreamToReturn = new MemoryStream(new byte[] { 1, 2, 3 });
        _probe.UriResult = MediaDurationProbeResult.UriUnsupportedFailure;
        _probe.StreamResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(22));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(1, _probe.UriCalls);
        Assert.Equal(1, _probe.StreamCalls);
        Assert.Equal(TimeSpan.FromMinutes(22), (await ReloadAsync(item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_RelativePaths_FallBackToStream()
    {
        var ct = TestContext.Current.CancellationToken;
        // Weder Item- noch Collection-Pfad absolut → kein absoluter sftp://-Pfad → Pipe-Fallback.
        var item = await SeedSftpItemAsync("episode.mkv", "relative/media/episode.mkv", collectionPath: "relative");
        _reader.StreamToReturn = new MemoryStream(new byte[] { 1, 2, 3 });
        _probe.StreamResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(22));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        Assert.Equal(0, _probe.UriCalls);
        Assert.Equal(1, _probe.StreamCalls);
        Assert.Equal(TimeSpan.FromMinutes(22), (await ReloadAsync(item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_UriPermanentFailure_ExhaustsAttempts()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedSftpItemAsync("broken.mkv", "/remote/media/broken.mkv");
        _probe.UriResult = MediaDurationProbeResult.PermanentFailure;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.True(reloaded.DurationProbeAttempts >= _probeOptions.EffectiveMaxAttempts);
    }

    [Fact]
    public async Task ProbePendingAsync_SftpSource_NullStream_CountsTransientAttempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await SeedItemAsync("gone.mp4", MediaSourceType.Sftp);
        _reader.StreamToReturn = null;

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        await service.ProbePendingAsync(10, ct);

        var reloaded = await ReloadAsync(item.Id, ct);
        Assert.Null(reloaded.Duration);
        Assert.NotNull(reloaded.DurationProbedAt);
        Assert.Equal(1, reloaded.DurationProbeAttempts);
        Assert.Equal(0, _probe.StreamCalls);
    }

    [Fact]
    public async Task ProbePendingAsync_ProbeThrows_MarksTransientAndContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var failing = await SeedItemAsync("a-throws.mp4", MediaSourceType.LocalDirectory);
        var succeeding = await SeedItemAsync("b-works.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(5));
        _probe.ThrowOnPathContaining = "a-throws";

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        var processed = await service.ProbePendingAsync(10, ct);

        Assert.Equal(2, processed);
        var failedItem = await ReloadAsync(failing.Id, ct);
        Assert.Null(failedItem.Duration);
        Assert.NotNull(failedItem.DurationProbedAt);
        Assert.Equal(1, failedItem.DurationProbeAttempts);
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
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(10));

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(2, await service.ProbePendingAsync(2, ct));
        Assert.Equal(2, _probe.FileCalls);

        Assert.Equal(2, await service.ProbePendingAsync(2, ct));
        Assert.Equal(0, await service.ProbePendingAsync(2, ct));
    }

    [Fact]
    public async Task ProbePendingAsync_ParallelProbes_RunsConcurrently()
    {
        var ct = TestContext.Current.CancellationToken;
        _probeOptions.ParallelProbes = 4;
        for (var i = 0; i < 4; i++)
            await SeedItemAsync($"parallel-{i}.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(10));
        _probe.Delay = TimeSpan.FromMilliseconds(200);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(4, await service.ProbePendingAsync(4, ct));

        Assert.True(_probe.MaxConcurrentCalls > 1, $"expected concurrent probes, observed max {_probe.MaxConcurrentCalls}");
    }

    [Fact]
    public async Task ProbePendingAsync_SequentialProbes_RunOneAtATime()
    {
        var ct = TestContext.Current.CancellationToken;
        _probeOptions.ParallelProbes = 1;
        for (var i = 0; i < 3; i++)
            await SeedItemAsync($"sequential-{i}.mp4", MediaSourceType.LocalDirectory);
        _probe.FileResult = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(10));
        _probe.Delay = TimeSpan.FromMilliseconds(50);

        var service = _serviceProvider.GetRequiredService<MediaDurationProbeService>();
        Assert.Equal(3, await service.ProbePendingAsync(3, ct));

        Assert.Equal(1, _probe.MaxConcurrentCalls);
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

    /// <summary>
    /// Seeds an SFTP media source with credentials (<c>nas.local:22</c>, <c>user</c>/<c>s3cret</c>),
    /// a collection at <paramref name="collectionPath"/> and one item at <paramref name="itemPath"/>.
    /// </summary>
    private async Task<MediaItem> SeedSftpItemAsync(string name, string itemPath, string collectionPath = "/remote")
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var source = new MediaSource
        {
            Name = $"Sftp-{name}",
            Path = collectionPath,
            Host = "nas.local",
            Port = 22,
            Username = "user",
            Password = "s3cret",
            SourceType = MediaSourceType.Sftp,
            CreatedAt = DateTime.UtcNow
        };
        var collection = new MediaCollection
        {
            Name = "remote",
            Path = collectionPath,
            CreatedAt = DateTime.UtcNow,
            MediaSource = source
        };
        var item = new MediaItem
        {
            Name = name,
            Path = itemPath,
            CreatedAt = DateTime.UtcNow,
            MediaCollection = collection
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
        private int _concurrentCalls;

        public MediaDurationProbeResult FileResult { get; set; } = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(60));
        public MediaDurationProbeResult StreamResult { get; set; } = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(60));
        public MediaDurationProbeResult UriResult { get; set; } = MediaDurationProbeResult.Success(TimeSpan.FromMinutes(60));
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public int FileCalls { get; set; }
        public int StreamCalls { get; set; }
        public int UriCalls { get; set; }
        public int MaxConcurrentCalls { get; private set; }
        public string? LastFilePath { get; private set; }
        public string? LastUri { get; private set; }
        public string? ThrowOnPathContaining { get; set; }

        public async Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
        {
            FileCalls++;
            LastFilePath = filePath;
            if (ThrowOnPathContaining is not null && filePath.Contains(ThrowOnPathContaining, StringComparison.Ordinal))
                throw new IOException("Simulated probe failure.");
            await TrackAsync(cancellationToken);
            return FileResult;
        }

        public async Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
        {
            UriCalls++;
            LastUri = uri;
            await TrackAsync(cancellationToken);
            return UriResult;
        }

        public async Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
        {
            StreamCalls++;
            await TrackAsync(cancellationToken);
            return StreamResult;
        }

        private async Task TrackAsync(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _concurrentCalls);
            lock (this)
            {
                if (current > MaxConcurrentCalls)
                    MaxConcurrentCalls = current;
            }

            try
            {
                if (Delay > TimeSpan.Zero)
                    await Task.Delay(Delay, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCalls);
            }
        }
    }

    private sealed class FakeFfprobeLocator : IFfprobeLocator
    {
        public string? Path { get; set; } = "ffprobe";
        public string? Resolve() => Path;
    }

    private sealed class FakeScheduleSignal : IProgramScheduleSignal
    {
        public int NotifyCalls { get; private set; }

        public void NotifyScheduleChanged() => NotifyCalls++;

        public IDisposable BeginScan() => new NoOpDisposable();

        public ValueTask WaitAsync(CancellationToken cancellationToken)
            => new(Task.Delay(Timeout.Infinite, cancellationToken));

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
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
