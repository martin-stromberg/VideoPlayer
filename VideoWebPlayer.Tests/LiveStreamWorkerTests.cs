using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for <see cref="LiveStreamWorker"/>: the startup cleanup of the segment directory and
/// the first synchronization tick against fake services.
/// </summary>
public sealed class LiveStreamWorkerTests : IAsyncLifetime
{
    private sealed class FakeGuide : IProgramGuideService
    {
        public Task<DtoProgramGuideNow> GetNowAsync(DateTimeOffset utcNow, CancellationToken ct = default)
            => Task.FromResult(new DtoProgramGuideNow());
        public Task<DtoProgramGuideEntry[]> GetUpcomingAsync(DateTimeOffset utcNow, TimeSpan preview, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<DtoProgramGuideEntry>());
    }

    private sealed class FakeLocator : IFfmpegLocator
    {
        public string? Resolve() => "/usr/bin/ffmpeg";
    }

    private sealed class FakeSegmenter : ILiveStreamSegmenter
    {
        public bool Running;
        public bool IsRunning => Running;
        public long? CurrentSlotId => null;
        public double? ExpectedPositionSeconds => null;
        public LiveStreamExitInfo? LastUnexpectedExit => null;
        public int StopCalls;
        public int StartCalls;
        public Task StartAsync(LiveStreamJob job, CancellationToken ct = default) { StartCalls++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) { StopCalls++; Running = false; return Task.CompletedTask; }
    }

    private sealed class NullStreamTokenService : IInternalMediaStreamTokenService
    {
        public string? TryCreateProbeUri(long mediaItemId) => null;
        public string? TryCreateStreamUri(long mediaItemId, TimeSpan lifetime) => null;
        public long? ResolveMediaItemId(string token) => null;
    }

    private sealed class NoToolLocator : IFfmpegLocator, IFfprobeLocator
    {
        public string? Resolve() => null;
    }

    // Echter Provider über den internen Overload: resolveMediaInputAsync zählt die
    // Refresh-Aufrufe (Setup-Konfiguration), ohne ffmpeg/lokale Medien zu brauchen.
    private LiveStreamStandbyProvider CreateStandby(
        IServiceScopeFactory scopeFactory, Action<long>? onResolve = null)
        => new(new TestWebHostEnvironment { ContentRootPath = _contentRoot }, scopeFactory,
            new LiveStreamStandbySegmenter(new NoToolLocator(), new NoToolLocator(),
                NullLogger<LiveStreamStandbySegmenter>.Instance),
            NullLogger<LiveStreamStandbyProvider>.Instance, null, null,
            (id, _) =>
            {
                onResolve?.Invoke(id);
                return Task.FromResult<LiveStreamInput?>(null);
            },
            null);

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-worker-{Guid.NewGuid():N}");
    private ProgramScheduleTestDb _fixture = null!;

    public async ValueTask InitializeAsync()
        => _fixture = await ProgramScheduleTestDb.CreateAsync($"worker-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks()
    {
        var ct = TestContext.Current.CancellationToken;
        var segDir = Path.Combine(_contentRoot, "Data", "LiveStream");
        Directory.CreateDirectory(segDir);
        var stale = Path.Combine(segDir, "seg_stale.ts");
        await File.WriteAllTextAsync(stale, "stale", ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture.Db);
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot });
        services.AddScoped<LiveStreamSegmentStore>();
        services.AddSingleton<IProgramGuideService>(new FakeGuide());
        services.AddSingleton<ILiveStreamSegmenter>(new FakeSegmenter());
        services.AddSingleton<IFfmpegLocator>(new FakeLocator());
        var local = new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance);
        services.AddSingleton(local);
        services.AddSingleton(new SftpMediaSourceReader());
        services.AddSingleton<IMediaSourceReader>(sp => new MediaSourceReaderDispatcher(
            sp.GetRequiredService<SftpMediaSourceReader>(), sp.GetRequiredService<LocalMediaSourceReader>()));
        services.AddSingleton<IInternalMediaStreamTokenService>(new NullStreamTokenService());
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new VideoWebPlayer.Configuration.LiveStreamOptions()));
        services.AddScoped<LiveStreamMediaResolver>();
        services.AddScoped<LiveStreamCoordinator>();
        await using var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var segmenter = (FakeSegmenter)provider.GetRequiredService<ILiveStreamSegmenter>();
        var worker = new LiveStreamWorker(scopeFactory,
            NullLogger<LiveStreamWorker>.Instance, CreateStandby(scopeFactory),
            signal: null, initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        await worker.StartAsync(cts.Token);
        try
        {
            for (var i = 0; i < 100 && File.Exists(stale); i++)
                await Task.Delay(100, ct);
            Assert.False(File.Exists(stale));

            for (var i = 0; i < 50 && segmenter.StopCalls == 0; i++)
                await Task.Delay(100, ct);
            Assert.True(segmenter.StopCalls >= 1, "Der Worker hat keinen Synchronisations-Tick ausgeführt.");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts()
    {
        var ct = TestContext.Current.CancellationToken;

        // Feature-Flag aus (Fixture seedet true → hier umkippen): ein noch laufender ffmpeg-Lauf
        // wird gestoppt, die Synchronisation startet nie — kein Zombie-Segmenter nach dem Sperren.
        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        setup.TvSenderEnabled = false;
        await _fixture.Db.SaveChangesAsync(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture.Db);
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot });
        services.AddScoped<LiveStreamSegmentStore>();
        services.AddSingleton<IProgramGuideService>(new FakeGuide());
        var segmenter = new FakeSegmenter { Running = true };
        services.AddSingleton<ILiveStreamSegmenter>(segmenter);
        services.AddSingleton<IFfmpegLocator>(new FakeLocator());
        var local = new LocalMediaSourceReader(NullLogger<LocalMediaSourceReader>.Instance);
        services.AddSingleton(local);
        services.AddSingleton(new SftpMediaSourceReader());
        services.AddSingleton<IMediaSourceReader>(sp => new MediaSourceReaderDispatcher(
            sp.GetRequiredService<SftpMediaSourceReader>(), sp.GetRequiredService<LocalMediaSourceReader>()));
        services.AddSingleton<IInternalMediaStreamTokenService>(new NullStreamTokenService());
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new VideoWebPlayer.Configuration.LiveStreamOptions()));
        services.AddScoped<LiveStreamMediaResolver>();
        services.AddScoped<LiveStreamCoordinator>();
        await using var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new LiveStreamWorker(scopeFactory,
            NullLogger<LiveStreamWorker>.Instance, CreateStandby(scopeFactory),
            signal: null, initialDelay: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        await worker.StartAsync(cts.Token);
        try
        {
            for (var i = 0; i < 50 && segmenter.StopCalls == 0; i++)
                await Task.Delay(100, ct);
            Assert.True(segmenter.StopCalls >= 1, "Der Worker hat den laufenden Segmenter nicht gestoppt.");
            Assert.False(segmenter.Running, "Der Segmenter laeuft trotz gesperrtem Sender weiter.");
            Assert.Equal(0, segmenter.StartCalls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay()
    {
        var ct = TestContext.Current.CancellationToken;
        var segDir = Path.Combine(_contentRoot, "Data", "LiveStream");
        Directory.CreateDirectory(segDir);
        var stale = Path.Combine(segDir, "seg_stale.ts");
        await File.WriteAllTextAsync(stale, "stale", ct);

        // Konfigurierter Standby-Clip: die Refresh-Auflösung beweist, dass der
        // RefreshAsync-Aufruf vor der Startverzögerung lief.
        (await _fixture.Db.Setups.FirstAsync(ct)).LiveStreamStandbyMediaItemId = 777;
        await _fixture.Db.SaveChangesAsync(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture.Db);
        services.AddScoped<ProgramSettingsService>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new TestWebHostEnvironment { ContentRootPath = _contentRoot });
        services.AddScoped<LiveStreamSegmentStore>();
        await using var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var resolveCalls = 0;
        var standby = CreateStandby(scopeFactory, _ => Interlocked.Increment(ref resolveCalls));
        var timeProvider = new ControllableTimeProvider();
        var worker = new LiveStreamWorker(scopeFactory,
            NullLogger<LiveStreamWorker>.Instance, standby,
            signal: null, initialDelay: TimeSpan.FromMinutes(5), timeProvider: timeProvider);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        await worker.StartAsync(cts.Token);
        try
        {
            // Vor dem ersten Timer-Tick (Delay läuft nur über den steuerbaren Provider):
            // Altsegment gelöscht UND Standby-Refresh bis zur Auflösung durchgelaufen.
            for (var i = 0; i < 100 && (File.Exists(stale) || resolveCalls == 0); i++)
                await Task.Delay(50, ct);
            Assert.False(File.Exists(stale));
            Assert.Equal(1, resolveCalls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
