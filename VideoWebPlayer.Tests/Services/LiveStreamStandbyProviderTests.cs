using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="LiveStreamStandbyProvider"/>: the rotating live playlist (media
/// sequence, window, PROGRAM-DATE-TIME, no ENDLIST), the strict segment lookup, the
/// embedded default set, the degradation without assets, the transition flag and the
/// <c>PrepareAsync</c> paths (success, failure, cache hit, back-to-default).
/// </summary>
public sealed class LiveStreamStandbyProviderTests : IAsyncLifetime
{
    private sealed class FakeFfmpegLocator : IFfmpegLocator
    {
        public string? Resolve() => null;
    }

    private sealed class FakeFfprobeLocator : IFfprobeLocator
    {
        public string? Resolve() => null;
    }

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-standby-{Guid.NewGuid():N}");
    private string _cacheDir => Path.Combine(_contentRoot, "Data", "LiveStreamStandby");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static IServiceScopeFactory EmptyScopeFactory()
        => new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static LiveStreamStandbySegmenter CreateSegmenter()
        => new(new FakeFfmpegLocator(), new FakeFfprobeLocator(), NullLogger<LiveStreamStandbySegmenter>.Instance);

    private LiveStreamStandbyProvider CreateProvider(
        ListLogger<LiveStreamStandbyProvider>? logger = null,
        TimeProvider? timeProvider = null,
        Func<StandbySegmentSet?>? loadDefaultSet = null,
        Func<long, CancellationToken, Task<LiveStreamInput?>>? resolveMediaInputAsync = null,
        Func<LiveStreamInput, string, CancellationToken, Task<IReadOnlyList<StandbyTranscodedSegment>?>>? transcodeAsync = null)
        => new(new TestWebHostEnvironment { ContentRootPath = _contentRoot },
            EmptyScopeFactory(), CreateSegmenter(),
            (ILogger<LiveStreamStandbyProvider>?)logger ?? NullLogger<LiveStreamStandbyProvider>.Instance,
            timeProvider, loadDefaultSet, resolveMediaInputAsync, transcodeAsync);

    private static StandbySegmentSet TestSet(long? mediaItemId = null, int generation = 0, double[]? durations = null)
    {
        durations ??= [4, 4, 2];
        return new StandbySegmentSet(generation, mediaItemId,
            durations.Select((d, i) => new StandbySegment(
                LiveStreamStandbyProvider.SegmentName(generation, i),
                TimeSpan.FromSeconds(d),
                [0x47, 0x11, (byte)(0x22 + i)])).ToList());
    }

    [Fact]
    public void BuildPlaylist_RotatesWindowAndAdvancesMediaSequence()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(timeProvider: clock, loadDefaultSet: () => TestSet());

        var first = provider.BuildPlaylist(null)!;
        Assert.Contains("#EXT-X-MEDIA-SEQUENCE:0", first);
        Assert.Contains("#EXT-X-TARGETDURATION:4", first);
        Assert.Contains("#EXT-X-PROGRAM-DATE-TIME:", first);
        Assert.DoesNotContain("#EXT-X-ENDLIST", first);
        Assert.Contains("standby_0_00.ts", first);

        clock.Advance(TimeSpan.FromSeconds(4.5));
        var second = provider.BuildPlaylist(null)!;
        Assert.Contains("#EXT-X-MEDIA-SEQUENCE:1", second);
        Assert.True(second.IndexOf("standby_0_01.ts", StringComparison.Ordinal)
                  < second.IndexOf("standby_0_02.ts", StringComparison.Ordinal));

        clock.Advance(TimeSpan.FromSeconds(10));
        var third = provider.BuildPlaylist(null)!;
        Assert.Contains("#EXT-X-MEDIA-SEQUENCE:4", third);
    }

    [Fact]
    public void TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull()
    {
        var provider = CreateProvider(loadDefaultSet: () => TestSet());

        var bytes = provider.TryGetSegment("standby_0_00.ts");
        Assert.NotNull(bytes);
        Assert.Equal(0x47, bytes![0]);

        Assert.Null(provider.TryGetSegment("standby_1_00.ts"));
        Assert.Null(provider.TryGetSegment("seg_001.ts"));
        Assert.Null(provider.TryGetSegment("../standby_0_00.ts"));
        Assert.Null(provider.TryGetSegment("not-there.ts"));
    }

    [Fact]
    public void EmbeddedDefault_LoadsOnConstruction()
    {
        // Kein injizierter Satz — die committeten Embedded-Resources aus der Assembly.
        var provider = CreateProvider();

        var playlist = provider.BuildPlaylist(null);
        Assert.NotNull(playlist);
        Assert.Contains("standby_0_", playlist);

        var segment = provider.TryGetSegment("standby_0_00.ts");
        Assert.NotNull(segment);
        Assert.Equal(0x47, segment![0]);
    }

    [Fact]
    public void MissingDefaultAssets_BuildPlaylistReturnsNull_LogsError()
    {
        var logger = new ListLogger<LiveStreamStandbyProvider>(new());
        var provider = CreateProvider(logger: logger, loadDefaultSet: () => null);

        Assert.Null(provider.BuildPlaylist(null));
        Assert.Null(provider.TryGetSegment("standby_0_00.ts"));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task BuildPlaylist_ConfiguredIdDiffers_ServesActiveSet_AndTriggersPrepare()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolveCalls = 0;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                return Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile("/media/clip.mkv"));
            },
            transcodeAsync: (input, dir, _) =>
            {
                File.WriteAllBytes(Path.Combine(dir, "standby_000.ts"), [0x47, 0x01]);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(
                    [new StandbyTranscodedSegment("standby_000.ts", TimeSpan.FromSeconds(4))]);
            });

        var standbyNow = provider.BuildPlaylist(7)!;
        Assert.Contains("standby_0_", standbyNow); // der aktive Satz antwortet sofort weiter

        // Die Hintergrund-Vorbereitung schaltet den Satz auf die konfigurierte ID um —
        // der Aufrufer übergibt wie der Controller immer die konfigurierte ID (null würde
        // das Zurückschalten auf den Default auslösen).
        var custom = "";
        for (var i = 0; i < 100 && !custom.Contains("standby_1_"); i++)
        {
            await Task.Delay(50, ct);
            custom = provider.BuildPlaylist(7) ?? string.Empty;
        }

        Assert.Equal(1, resolveCalls);
        Assert.Contains("standby_1_00.ts", custom);
    }

    [Fact]
    public async Task PrepareAsync_Success_SwitchesToCustomSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) => Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile("/media/clip.mkv")),
            transcodeAsync: (input, dir, _) =>
            {
                File.WriteAllBytes(Path.Combine(dir, "standby_000.ts"), [0x47, 0xAA]);
                File.WriteAllBytes(Path.Combine(dir, "standby_001.ts"), [0x47, 0xBB]);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(
                [
                    new StandbyTranscodedSegment("standby_000.ts", TimeSpan.FromSeconds(4)),
                    new StandbyTranscodedSegment("standby_001.ts", TimeSpan.FromSeconds(2))
                ]);
            });

        await provider.PrepareAsync(7, ct);

        var playlist = provider.BuildPlaylist(7)!;
        Assert.Contains("standby_1_00.ts", playlist);
        Assert.DoesNotContain("standby_0_", playlist);
        Assert.Equal(new byte[] { 0x47, 0xAA }, provider.TryGetSegment("standby_1_00.ts"));
        Assert.Equal(new byte[] { 0x47, 0xBB }, provider.TryGetSegment("standby_1_01.ts"));
    }

    [Fact]
    public async Task PrepareAsync_Failure_KeepsPreviousSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new ListLogger<LiveStreamStandbyProvider>(new());
        var resolveCalls = 0;
        var provider = CreateProvider(
            logger: logger,
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                return Task.FromResult<LiveStreamInput?>(null);
            });

        await provider.PrepareAsync(7, ct);
        await provider.PrepareAsync(7, ct); // gemerkte Fehlschlag-ID — kein Sofort-Retry

        Assert.Equal(1, resolveCalls);
        Assert.Contains("standby_0_", provider.BuildPlaylist(null));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);

        var transcodeCalls = 0;
        var provider2 = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) => Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile("/media/clip.mkv")),
            transcodeAsync: (input, dir, _) =>
            {
                Interlocked.Increment(ref transcodeCalls);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(null);
            });
        await provider2.PrepareAsync(9, ct);
        await provider2.PrepareAsync(9, ct);

        Assert.Equal(1, transcodeCalls);
        Assert.Contains("standby_0_", provider2.BuildPlaylist(null));
    }

    [Fact]
    public async Task PrepareAsync_NullTarget_SwitchesBackToDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolveCalls = 0;
        var transcodeCalls = 0;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                return Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile("/media/clip.mkv"));
            },
            transcodeAsync: (input, dir, _) =>
            {
                Interlocked.Increment(ref transcodeCalls);
                File.WriteAllBytes(Path.Combine(dir, "standby_000.ts"), [0x47, 0x01]);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(
                    [new StandbyTranscodedSegment("standby_000.ts", TimeSpan.FromSeconds(4))]);
            });

        await provider.PrepareAsync(7, ct);
        Assert.Contains("standby_1_", provider.BuildPlaylist(7));

        var callsBefore = resolveCalls + transcodeCalls;
        await provider.PrepareAsync(null, ct);

        Assert.Equal(callsBefore, resolveCalls + transcodeCalls);
        Assert.Contains("standby_0_", provider.BuildPlaylist(null));
    }

    [Fact]
    public async Task PrepareAsync_CacheHit_LoadsFromDisk_WithoutFfmpeg()
    {
        var ct = TestContext.Current.CancellationToken;
        var clipPath = await SeedSourceClipAsync();
        await WriteCacheAsync(clipPath, File.GetLastWriteTimeUtc(clipPath));

        var resolveCalls = 0;
        var transcodeCalls = 0;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                return Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile(clipPath));
            },
            transcodeAsync: (input, dir, _) =>
            {
                Interlocked.Increment(ref transcodeCalls);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(null);
            });

        await provider.PrepareAsync(7, ct);

        // Die Aufloesung laeuft vor dem Cache (Existenzpruefung des MediaItems), ffmpeg bleibt aus.
        Assert.Equal(1, resolveCalls);
        Assert.Equal(0, transcodeCalls);
        Assert.Equal(new byte[] { 0x47, 0x77 }, provider.TryGetSegment("standby_1_00.ts"));
        Assert.Contains("standby_1_00.ts", provider.BuildPlaylist(7));
    }

    [Fact]
    public async Task PrepareAsync_CacheMiss_WhenSourceFileChanged_Transcodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var clipPath = await SeedSourceClipAsync();
        // Marker mit veraltetem Aenderungsstempel — die Quelldatei wurde ersetzt.
        await WriteCacheAsync(clipPath, File.GetLastWriteTimeUtc(clipPath).AddDays(-1));

        var transcodeCalls = 0;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) => Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile(clipPath)),
            transcodeAsync: (input, dir, _) =>
            {
                Interlocked.Increment(ref transcodeCalls);
                File.WriteAllBytes(Path.Combine(dir, "standby_000.ts"), [0x47, 0x55]);
                return Task.FromResult<IReadOnlyList<StandbyTranscodedSegment>?>(
                    [new StandbyTranscodedSegment("standby_000.ts", TimeSpan.FromSeconds(4))]);
            });

        await provider.PrepareAsync(7, ct);

        Assert.Equal(1, transcodeCalls);
        Assert.Equal(new byte[] { 0x47, 0x55 }, provider.TryGetSegment("standby_1_00.ts"));
    }

    [Fact]
    public async Task PrepareAsync_DeletedMediaItemWithCache_FallsBackToDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var clipPath = await SeedSourceClipAsync();
        await WriteCacheAsync(clipPath, File.GetLastWriteTimeUtc(clipPath));

        var resolveCalls = 0;
        var provider = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                return Task.FromResult<LiveStreamInput?>(null); // MediaItem geloescht
            });

        await provider.PrepareAsync(7, ct);

        // Der veraltete Cache-Satz darf nicht bedient werden — Default bleibt aktiv.
        Assert.Equal(1, resolveCalls);
        Assert.Contains("standby_0_", provider.BuildPlaylist(7));
        Assert.Null(provider.TryGetSegment("standby_1_00.ts"));

        // Die Fehlschlag-ID ist gemerkt: kein erneuter Aufloesungslauf.
        await provider.PrepareAsync(7, ct);
        Assert.Equal(1, resolveCalls);
    }

    [Fact]
    public async Task PrepareAsync_ResolverOrTranscodeThrows_MarksFailed_SuppressesRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new ListLogger<LiveStreamStandbyProvider>(new());
        var resolveCalls = 0;
        var throwingResolve = CreateProvider(
            logger: logger,
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) =>
            {
                Interlocked.Increment(ref resolveCalls);
                throw new InvalidOperationException("resolver boom");
            });

        await throwingResolve.PrepareAsync(7, ct); // darf nicht propagieren
        await throwingResolve.PrepareAsync(7, ct);

        Assert.Equal(1, resolveCalls);
        Assert.Contains("standby_0_", throwingResolve.BuildPlaylist(7));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);

        var transcodeCalls = 0;
        var throwingTranscode = CreateProvider(
            loadDefaultSet: () => TestSet(),
            resolveMediaInputAsync: (id, _) => Task.FromResult<LiveStreamInput?>(LiveStreamInput.ForFile("/media/clip.mkv")),
            transcodeAsync: (input, dir, _) =>
            {
                Interlocked.Increment(ref transcodeCalls);
                throw new InvalidOperationException("transcode boom");
            });

        await throwingTranscode.PrepareAsync(9, ct);
        await throwingTranscode.PrepareAsync(9, ct);

        Assert.Equal(1, transcodeCalls);
        Assert.Contains("standby_0_", throwingTranscode.BuildPlaylist(9));
    }

    /// <summary>Legt eine echte Quelldatei an, damit Pfad + Aenderungsstempel im
    /// Cache-Marker geprueft werden koennen.</summary>
    private async Task<string> SeedSourceClipAsync()
    {
        var clipPath = Path.Combine(_contentRoot, "standby-src.mkv");
        Directory.CreateDirectory(_contentRoot);
        await File.WriteAllBytesAsync(clipPath, [1, 2, 3], TestContext.Current.CancellationToken);
        return clipPath;
    }

    /// <summary>Schreibt Cache-Segment + Marker fuer MediaItem 7 mit gegebenem Stempel.</summary>
    private async Task WriteCacheAsync(string clipPath, DateTime sourceWriteTimeUtc)
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(_cacheDir);
        await File.WriteAllBytesAsync(Path.Combine(_cacheDir, "standby_000.ts"), [0x47, 0x77], ct);
        var markerJson = JsonSerializer.Serialize(new
        {
            mediaItemId = 7L,
            source = clipPath,
            sourceWriteTimeUtc,
            files = new[] { new { fileName = "standby_000.ts", durationSeconds = 4.0 } }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllTextAsync(Path.Combine(_cacheDir, LiveStreamStandbyProvider.CacheMarkerFileName), markerJson, ct);
    }

    [Fact]
    public void TransitionFlag_NoteAndConsume()
    {
        var provider = CreateProvider(loadDefaultSet: () => TestSet());

        Assert.False(provider.ConsumePendingStandbyTransition());
        provider.NoteStandbyPlaylistServed();
        Assert.True(provider.ConsumePendingStandbyTransition());
        Assert.False(provider.ConsumePendingStandbyTransition());
    }
}
