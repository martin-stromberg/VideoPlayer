using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
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
        services.AddSingleton<IMediaDurationProbe>(new FixedDurationProbe(TimeSpan.FromMinutes(3)));
        services.AddSingleton<IFfprobeLocator>(new PresentLocator());
        services.AddSingleton(Options.Create(new MediaProbeOptions { IntervalSeconds = 1, BatchSize = 10 }));
        services.AddScoped<MediaDurationProbeService>();
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<GatedScheduleSignal>();
        services.AddSingleton<IProgramScheduleSignal>(sp => sp.GetRequiredService<GatedScheduleSignal>());
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
    public async Task ExecuteAsync_FeatureFlagEvaluatedPerCycle()
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

        // Mehrere Worker-Intervall-Ticks (IntervalSeconds = 1) abwarten:
        // kein Zyklus darf bei ausgeschaltetem Flag etwas messen.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        db.ChangeTracker.Clear();
        var reloaded = await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct);
        Assert.Null(reloaded.DurationProbedAt);
        Assert.Null(reloaded.Duration);

        // Flag zur Laufzeit einschalten: der naechste Takt muss es ohne Worker-Neustart
        // auswerten und das Item messen (deckt ab, dass das Flag pro Zyklus gelesen wird).
        db.ChangeTracker.Clear();
        setup = await db.Setups.SingleAsync(ct);
        setup.TvSenderEnabled = true;
        await db.SaveChangesAsync(ct);

        var deadline = DateTime.UtcNow.AddSeconds(10);
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

        Assert.True(probedAt is not null, "Item wurde nach Freischalten des Flags nicht gemessen.");
        Assert.Equal(TimeSpan.FromMinutes(3), (await db.MediaItems.SingleAsync(i => i.Id == item.Id, ct)).Duration);
    }

    // Issue #290: ProbePendingAsync fires NotifyScheduleChanged once per batch — the worker
    // wraps its backlog loop in the signal's scan scope, so a multi-batch pass wakes the
    // schedule worker exactly once (at scope end) instead of once per batch.
    [Fact]
    public async Task MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake()
    {
        var ct = TestContext.Current.CancellationToken;
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource { Name = "local", Path = _rootDir, SourceType = MediaSourceType.LocalDirectory, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var collection = new MediaCollection { Name = "root", Path = _rootDir, CreatedAt = DateTime.UtcNow, MediaSource = source, MediaSourceId = source.Id };
        db.MediaCollections.Add(collection);
        await db.SaveChangesAsync(ct);
        // Three pending items with batch size 1 → three ProbePendingAsync passes in a single
        // backlog loop, each writing a duration (each would fire its own signal unbundled).
        foreach (var name in new[] { "a.mp4", "b.mp4", "c.mp4" })
            db.MediaItems.Add(new MediaItem { Name = name, Path = Path.Combine(_rootDir, name), CreatedAt = DateTime.UtcNow, MediaCollectionId = collection.Id });
        await db.SaveChangesAsync(ct);

        var signal = _serviceProvider.GetRequiredService<GatedScheduleSignal>();
        signal.HoldDispose();
        // Long interval: only the first pass runs during the test — every observed wake belongs
        // to it.
        var worker = new MediaDurationProbeWorker(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MediaProbeOptions { IntervalSeconds = 3600, BatchSize = 1 }),
            new Helpers.ListLogger<MediaDurationProbeWorker>(new System.Collections.Concurrent.ConcurrentQueue<string>()),
            initialDelay: TimeSpan.Zero,
            signal: signal);

        var wakes = 0;
        using var wakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var wakeCounter = Task.Run(async () =>
        {
            while (!wakeCts.IsCancellationRequested)
            {
                try
                {
                    await signal.WaitAsync(wakeCts.Token);
                    Interlocked.Increment(ref wakes);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await worker.StartAsync(cts.Token);

        try
        {
            // Wait until the worker ran all three batches and asked to close the scan scope —
            // the gate keeps it open, so no wake may legitimately arrive yet.
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!signal.DisposeRequested.IsCompleted
                && Volatile.Read(ref wakes) == 0
                && DateTime.UtcNow < deadline)
                await Task.Delay(50, ct);

            Assert.True(signal.DisposeRequested.IsCompleted, "Der Worker beendete die Backlog-Schleife nicht innerhalb eines Scan-Scopes.");
            Assert.Equal(1, Volatile.Read(ref signal.BeginScanCount));
            Assert.Equal(3, Volatile.Read(ref signal.NotifyCount));

            // Grace window while the scope is still open: an unbundled notification would have
            // landed long ago — Bounded(1)/DropWrite can drop a write into a full channel but
            // never delay one into an empty channel, so zero wakes here proves suppression,
            // not channel coalescing.
            await Task.Delay(400, ct);
            Assert.Equal(0, Volatile.Read(ref wakes));

            // Releasing the scope ends the scan: the suppressed notifications deliver
            // exactly one wake.
            signal.ReleaseDispose();
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref wakes) == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(50, ct);
            // Grace so a wrongly duplicated wake lands before the count is judged.
            await Task.Delay(400, ct);

            Assert.Equal(1, Volatile.Read(ref wakes));
        }
        finally
        {
            // Release + stop even on assertion failure, otherwise the worker hangs in Dispose.
            signal.ReleaseDispose();
            await cts.CancelAsync();
            await worker.StopAsync(CancellationToken.None);
            await wakeCts.CancelAsync();
            try { await wakeCounter; }
            catch (OperationCanceledException) { }
        }

        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.MediaItems.CountAsync(i => i.Duration != null, ct));
    }

    /// <summary>
    /// <see cref="IProgramScheduleSignal"/> that delegates to a real <see cref="ProgramScheduleSignal"/>,
    /// counts notifications and scan scopes and — once armed via <see cref="HoldDispose"/> — keeps a
    /// closing scan scope open until <see cref="ReleaseDispose"/>: the channel can then be observed
    /// while notifications are already fired but still suppressed.
    /// </summary>
    private sealed class GatedScheduleSignal : IProgramScheduleSignal
    {
        private readonly ProgramScheduleSignal _inner = new();
        private readonly TaskCompletionSource _disposeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseDispose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _holdDispose;

        public int BeginScanCount;
        public int NotifyCount;

        public Task DisposeRequested => _disposeRequested.Task;

        public void HoldDispose() => Volatile.Write(ref _holdDispose, true);

        public void ReleaseDispose() => _releaseDispose.TrySetResult();

        public void NotifyScheduleChanged()
        {
            Interlocked.Increment(ref NotifyCount);
            _inner.NotifyScheduleChanged();
        }

        public IDisposable BeginScan()
        {
            Interlocked.Increment(ref BeginScanCount);
            return new HeldScope(this, _inner.BeginScan());
        }

        public ValueTask WaitAsync(CancellationToken cancellationToken) => _inner.WaitAsync(cancellationToken);

        private sealed class HeldScope : IDisposable
        {
            private readonly GatedScheduleSignal _owner;
            private IDisposable? _inner;

            public HeldScope(GatedScheduleSignal owner, IDisposable inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public void Dispose()
            {
                var inner = Interlocked.Exchange(ref _inner, null);
                if (inner is null)
                    return;

                if (!Volatile.Read(ref _owner._holdDispose))
                {
                    inner.Dispose();
                    return;
                }

                _owner._disposeRequested.TrySetResult();
                // Blocks the worker's dispose path until the test releases — the real scope
                // (and with it the suppression) stays open while the test inspects the channel.
                _owner._releaseDispose.Task.GetAwaiter().GetResult();
                inner.Dispose();
            }
        }
    }

}
