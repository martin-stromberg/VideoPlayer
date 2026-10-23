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
/// Tests that <see cref="MediaDurationProbeWorker"/> drives <see cref="MediaDurationProbeService"/>
/// without user interaction.
/// </summary>
public class MediaDurationProbeWorkerTests : IDisposable
{
    private readonly string _rootDir;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public MediaDurationProbeWorkerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-probe-worker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);

        var connectionString = $"Data Source=file:probe-worker-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton(_connection);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMediaSourceReader>(new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance));
        services.AddSingleton<IMediaDurationProbe>(new AlwaysProbe());
        services.AddSingleton<IFfprobeLocator>(new PresentLocator());
        services.AddSingleton(Options.Create(new MediaProbeOptions { IntervalSeconds = 60, BatchSize = 10 }));
        services.AddScoped<MediaDurationProbeService>();
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ProgramSettingsService>>(NullLogger<ProgramSettingsService>.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<MediaDurationProbeService>>(NullLogger<MediaDurationProbeService>.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<MediaDurationProbeWorker>>(NullLogger<MediaDurationProbeWorker>.Instance);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion, TvSenderEnabled = true });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesPendingItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource { Name = "local", Path = _rootDir, SourceType = MediaSourceType.LocalDirectory, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "root", Path = _rootDir, CreatedAt = DateTime.UtcNow, MediaSource = source, MediaSourceId = source.Id };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        var item = new MediaItem { Name = "clip.mp4", Path = Path.Combine(_rootDir, "clip.mp4"), CreatedAt = DateTime.UtcNow, MediaCollectionId = collection.Id };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);

        var logMessages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var worker = new MediaDurationProbeWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IOptions<MediaProbeOptions>>(),
            new Helpers.ListLogger<MediaDurationProbeWorker>(logMessages),
            initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        DateTime? probedAt;
        do
        {
            await Task.Delay(100, ct);
            db.ChangeTracker.Clear();
            probedAt = (await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct)).DurationProbedAt;
        }
        while (probedAt is null && DateTime.UtcNow < deadline);

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(probedAt is not null, string.Join(" | ", logMessages));
        Assert.Equal(TimeSpan.FromMinutes(3), (await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct)).Duration);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureNotReleased_LeavesItemsUnprobed()
    {
        var ct = TestContext.Current.CancellationToken;
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Feature-Flag aus: der Worker haelt seinen Takt, fuehrt aber keine Probes aus —
        // die Laufzeitermittlung gehoert ausschliesslich zum TV-Sender.
        var setup = await db.Setups.SingleAsync(ct);
        setup.TvSenderEnabled = false;
        await db.SaveChangesAsync(ct);

        var source = new MediaSource { Name = "local", Path = _rootDir, SourceType = MediaSourceType.LocalDirectory, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "root", Path = _rootDir, CreatedAt = DateTime.UtcNow, MediaSource = source, MediaSourceId = source.Id };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        var item = new MediaItem { Name = "clip.mp4", Path = Path.Combine(_rootDir, "clip.mp4"), CreatedAt = DateTime.UtcNow, MediaCollectionId = collection.Id };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync(ct);

        var worker = new MediaDurationProbeWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IOptions<MediaProbeOptions>>(),
            new Helpers.ListLogger<MediaDurationProbeWorker>(new System.Collections.Concurrent.ConcurrentQueue<string>()),
            initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);

        // Laenger als ein Worker-Intervall-Tick warten: auch ein zweiter Zyklus darf nichts messen.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var reloaded = await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct);
        Assert.Null(reloaded.DurationProbedAt);
        Assert.Null(reloaded.Duration);
    }

    private sealed class AlwaysProbe : IMediaDurationProbe
    {
        public Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.FromMinutes(3)));
        public Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.FromMinutes(3)));
        public Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
            => Task.FromResult(MediaDurationProbeResult.Success(TimeSpan.FromMinutes(3)));
    }

    private sealed class PresentLocator : IFfprobeLocator
    {
        public string? Resolve() => "ffprobe";
    }
}
