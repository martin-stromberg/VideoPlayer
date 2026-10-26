using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>
/// One standby segment held in memory: served name (generation-prefixed), media duration
/// and the complete MPEG-TS payload.
/// </summary>
internal sealed record StandbySegment(string Name, TimeSpan Duration, byte[] Data);

/// <summary>
/// One immutable standby segment generation: atomically swapped as a whole so a set change
/// can never surface a name the new playlist no longer advertises. <see cref="MediaItemId"/>
/// is <see langword="null"/> for the embedded default clip.
/// </summary>
internal sealed record StandbySegmentSet(int Generation, long? MediaItemId, IReadOnlyList<StandbySegment> Segments)
{
    /// <summary>Gets the loop duration of the whole set.</summary>
    public TimeSpan TotalDuration { get; } = TimeSpan.FromTicks(Segments.Sum(s => s.Duration.Ticks));

    private readonly Dictionary<string, StandbySegment> _byName =
        Segments.ToDictionary(s => s.Name, StringComparer.Ordinal);

    /// <summary>Strict dictionary lookup of a served segment name — no path access.</summary>
    public StandbySegment? Find(string name)
        => _byName.TryGetValue(name, out var segment) ? segment : null;
}

/// <summary>
/// Holds the warm-up standby stream in memory: while no real <c>live.m3u8</c> with a
/// referenced segment exists, the controller serves this provider's rotating live playlist
/// and its generation-prefixed segments instead of answering 404. The default set is the
/// embedded Funki clip „Gleich geht's los!"; an admin-configured media item is prepared
/// lazily (or at worker start via <see cref="RefreshAsync"/>) by resolving it through
/// <see cref="LiveStreamMediaResolver"/> and transcoding it once via
/// <see cref="LiveStreamStandbySegmenter"/> into the disk cache
/// <c>Data/LiveStreamStandby</c>. A marker file (<c>standby-source.json</c>) lets a restart
/// reuse the cache without re-running ffmpeg.
/// </summary>
public sealed class LiveStreamStandbyProvider
{
    /// <summary>The name prefix of all standby segments.</summary>
    internal const string SegmentNamePrefix = "standby_";

    /// <summary>The number of entries the rotating standby playlist advertises.</summary>
    internal const int PlaylistWindowSegmentCount = 3;

    /// <summary>The cache marker file inside <c>Data/LiveStreamStandby</c>.</summary>
    internal const string CacheMarkerFileName = "standby-source.json";

    // Die eingebetteten Default-Segmente (standby_00..02.ts unter Assets/LiveStreamStandby/)
    // wurden einmalig aus dem Funki-Clip „Gleich geht's los!" erzeugt:
    //   ffmpeg -y -i funki-gleich-gehts-los.mp4 -map 0:v:0 -map 0:a:0? -c:v libx264
    //     -preset veryfast -c:a aac -b:a 128k -force_key_frames "expr:gte(t,n_forced*4)"
    //     -f segment -segment_time 4 -reset_timestamps 1 standby_%02d.ts
    // Dauern per ffprobe gemessen — Reihenfolge = Dateiname aufsteigend.
    private static readonly TimeSpan[] DefaultSegmentDurations =
        [TimeSpan.FromSeconds(4.023), TimeSpan.FromSeconds(4.076), TimeSpan.FromSeconds(2.059)];

    private static readonly string CacheRelativeDirectory = Path.Combine("Data", "LiveStreamStandby");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveStreamStandbyProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _origin;
    private readonly StandbySegmentSet? _defaultSet;
    private readonly Func<long, CancellationToken, Task<LiveStreamInput?>> _resolveMediaInputAsync;
    private readonly Func<LiveStreamInput, string, CancellationToken, Task<IReadOnlyList<StandbyTranscodedSegment>?>> _transcodeAsync;
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly object _prepareTriggerLock = new();

    private volatile StandbySegmentSet? _activeSet;
    private long? _failedMediaItemId;
    private int _generation;
    private int _standbyServed;
    private Task? _prepareTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStreamStandbyProvider"/> class.
    /// </summary>
    /// <param name="environment">Host environment (content root for the standby cache).</param>
    /// <param name="scopeFactory">Scope factory to resolve scoped services lazily.</param>
    /// <param name="segmenter">The one-time standby transcode runner.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public LiveStreamStandbyProvider(
        IWebHostEnvironment environment,
        IServiceScopeFactory scopeFactory,
        LiveStreamStandbySegmenter segmenter,
        ILogger<LiveStreamStandbyProvider> logger,
        TimeProvider? timeProvider = null)
        : this(environment, scopeFactory, segmenter, logger, timeProvider,
            loadDefaultSet: null, resolveMediaInputAsync: null, transcodeAsync: null)
    {
    }

    internal LiveStreamStandbyProvider(
        IWebHostEnvironment environment,
        IServiceScopeFactory scopeFactory,
        LiveStreamStandbySegmenter segmenter,
        ILogger<LiveStreamStandbyProvider> logger,
        TimeProvider? timeProvider,
        Func<StandbySegmentSet?>? loadDefaultSet,
        Func<long, CancellationToken, Task<LiveStreamInput?>>? resolveMediaInputAsync,
        Func<LiveStreamInput, string, CancellationToken, Task<IReadOnlyList<StandbyTranscodedSegment>?>>? transcodeAsync)
    {
        _environment = environment;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _origin = _timeProvider.GetUtcNow();
        _resolveMediaInputAsync = resolveMediaInputAsync ?? ResolveMediaInputDefaultAsync;
        _transcodeAsync = transcodeAsync ?? ((input, dir, ct) => segmenter.TranscodeAsync(input, dir, ct));
        _defaultSet = (loadDefaultSet ?? LoadDefaultSetFromResources)();
        if (_defaultSet is null || _defaultSet.Segments.Count == 0)
        {
            _logger.LogError("[LiveStreamStandbyProvider] Kein nutzbarer Standard-Standby-Satz (eingebettete Assets fehlen) — die Standby-Auslieferung degradiert auf 404.");
            _defaultSet = null;
        }

        _activeSet = _defaultSet;
    }

    /// <summary>
    /// Builds the rotating standby live playlist of the currently active set, or
    /// <see langword="null"/> when no usable set exists (missing embedded assets). When
    /// <paramref name="configuredMediaItemId"/> differs from the active set's item, the
    /// preparation of the configured clip is kicked off in the background — deduplicated —
    /// while the current set keeps answering in the meantime.
    /// </summary>
    /// <param name="configuredMediaItemId">The configured standby media item, or <see langword="null"/>.</param>
    /// <returns>The playlist text, or <see langword="null"/>.</returns>
    public string? BuildPlaylist(long? configuredMediaItemId)
    {
        var active = _activeSet;
        if (configuredMediaItemId != active?.MediaItemId)
            TriggerPrepare(configuredMediaItemId);
        if (active is null || active.Segments.Count == 0)
            return null;

        NoteStandbyPlaylistServed();
        return BuildPlaylistText(active, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns the bytes of a segment of the currently active set — independent of the real
    /// playlist's state, so a client finishing the last advertised standby playlist never
    /// hits a segment 404.
    /// </summary>
    /// <param name="fileName">The requested segment name.</param>
    /// <returns>The segment bytes, or <see langword="null"/>.</returns>
    public byte[]? TryGetSegment(string fileName)
        => _activeSet?.Find(fileName)?.Data;

    /// <summary>Marks that a standby playlist was just served to a client.</summary>
    public void NoteStandbyPlaylistServed() => Interlocked.Exchange(ref _standbyServed, 1);

    /// <summary>
    /// Returns <see langword="true"/> exactly once after a standby playlist was served — the
    /// first real playlist after it carries <c>#EXT-X-DISCONTINUITY</c>.
    /// </summary>
    public bool ConsumePendingStandbyTransition() => Interlocked.Exchange(ref _standbyServed, 0) == 1;

    /// <summary>
    /// Reads the configured standby media item and prepares it when it differs from the
    /// active set — called once by the worker during start-up so a cached clip is warm
    /// before the first request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        long? configuredId;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            configuredId = await scope.ServiceProvider
                .GetRequiredService<ProgramSettingsService>()
                .GetLiveStreamStandbyMediaItemIdAsync(cancellationToken);
        }

        await PrepareAsync(configuredId, cancellationToken);
    }

    /// <summary>
    /// Prepares the standby set for <paramref name="targetMediaItemId"/>: <see langword="null"/>
    /// switches back to the embedded default; a disk-cache hit loads without ffmpeg; otherwise
    /// the item is resolved and transcoded once. A failed id is remembered
    /// (<see cref="_failedMediaItemId"/>) so no retry storm runs against ffmpeg — it resets
    /// only on success or a different target. Deduplicated process-wide by a gate.
    /// </summary>
    /// <param name="targetMediaItemId">The media item to prepare, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task PrepareAsync(long? targetMediaItemId, CancellationToken cancellationToken = default)
    {
        if (!await _prepareGate.WaitAsync(0, cancellationToken))
            return;

        try
        {
            if (targetMediaItemId is null)
            {
                _activeSet = _defaultSet;
                _failedMediaItemId = null;
                return;
            }

            var target = targetMediaItemId.Value;
            if (_activeSet?.MediaItemId == target || _failedMediaItemId == target)
                return;

            // Das MediaItem wird vor dem Cache geprueft: ein geloeschtes Item mit
            // vorhandenem Cache-Marker darf nicht weiter ausgeliefert werden, sondern
            // faellt auf den bisherigen Satz (Default) zurueck — die Aufloesung ist zudem
            // die Quell-Referenz fuer die Cache-Invalidierung (Pfad + Aenderungsstempel).
            var input = await _resolveMediaInputAsync(target, cancellationToken);
            if (input is null)
            {
                _logger.LogWarning("[LiveStreamStandbyProvider] Standby-MediaItem {MediaItemId} konnte nicht aufgeloest werden — bisheriger Satz bleibt aktiv.", target);
                _failedMediaItemId = target;
                return;
            }

            if (await TryLoadFromCacheAsync(target, input, cancellationToken))
                return;

            var cacheDir = GetCacheDirectory();
            Directory.CreateDirectory(cacheDir);
            var segments = await _transcodeAsync(input, cacheDir, cancellationToken);
            if (segments is null || segments.Count == 0)
            {
                _logger.LogWarning("[LiveStreamStandbyProvider] Standby-MediaItem {MediaItemId} konnte nicht transkodiert werden — bisheriger Satz bleibt aktiv.", target);
                _failedMediaItemId = target;
                return;
            }

            var set = await LoadSetFromDirectoryAsync(cacheDir, target, segments, cancellationToken);
            if (set is null)
            {
                _logger.LogWarning("[LiveStreamStandbyProvider] Standby-Segmente fuer MediaItem {MediaItemId} konnten nicht aus dem Cache geladen werden.", target);
                _failedMediaItemId = target;
                return;
            }

            WriteCacheMarker(cacheDir, target, input, segments);
            _activeSet = set;
            _failedMediaItemId = null;
            _logger.LogInformation(
                "[LiveStreamStandbyProvider] Standby-Clip von MediaItem {MediaItemId} vorbereitet ({Count} Segmente, Generation {Generation}).",
                target, set.Segments.Count, set.Generation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Jeder Fehlpfad (Dateilesen, Aufloesung, Transcode) muss die Fehlschlag-ID
            // setzen — sonst loest jeder Playlist-Poll einen kompletten erneuten
            // Vorbereitungslauf inklusive ffmpeg aus (Retry-Sturm).
            _logger.LogWarning(ex, "[LiveStreamStandbyProvider] Vorbereitung von MediaItem {MediaItemId} fehlgeschlagen — bisheriger Satz bleibt aktiv.", targetMediaItemId);
            if (targetMediaItemId is { } failed)
                _failedMediaItemId = failed;
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    internal static string SegmentName(int generation, int index)
        => $"standby_{generation}_{index:00}.ts";

    private void TriggerPrepare(long? targetMediaItemId)
    {
        lock (_prepareTriggerLock)
        {
            if (_prepareTask is { IsCompleted: false })
                return;
            var target = targetMediaItemId;
            _prepareTask = Task.Run(async () =>
            {
                try
                {
                    await PrepareAsync(target, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LiveStreamStandbyProvider] Hintergrund-Vorbereitung des Standby-Clips fehlgeschlagen.");
                }
            });
        }
    }

    private async Task<LiveStreamInput?> ResolveMediaInputDefaultAsync(long mediaItemId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<LiveStreamMediaResolver>();
        return await resolver.ResolveAsync(mediaItemId, runDuration: null, useHttpInput: true, cancellationToken);
    }

    private string GetCacheDirectory()
        => Path.Combine(_environment.ContentRootPath, CacheRelativeDirectory);

    private async Task<bool> TryLoadFromCacheAsync(long target, LiveStreamInput input, CancellationToken cancellationToken)
    {
        var cacheDir = GetCacheDirectory();
        var markerPath = Path.Combine(cacheDir, CacheMarkerFileName);
        if (!File.Exists(markerPath))
            return false;

        StandbyCacheMarker? marker;
        try
        {
            marker = JsonSerializer.Deserialize<StandbyCacheMarker>(
                await File.ReadAllTextAsync(markerPath, cancellationToken), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }

        if (marker?.MediaItemId != target || marker.Files is not { Count: > 0 })
            return false;
        if (!CacheMarkerMatchesInput(marker, input))
            return false;
        if (marker.Files.Any(f => !File.Exists(Path.Combine(cacheDir, f.FileName))))
            return false;

        var generation = Interlocked.Increment(ref _generation);
        var segments = new List<StandbySegment>(marker.Files.Count);
        for (var i = 0; i < marker.Files.Count; i++)
        {
            var entry = marker.Files[i];
            var bytes = await File.ReadAllBytesAsync(Path.Combine(cacheDir, entry.FileName), cancellationToken);
            var duration = entry.DurationSeconds > 0
                ? TimeSpan.FromSeconds(entry.DurationSeconds)
                : LiveStreamStandbySegmenter.NominalSegmentDuration;
            segments.Add(new StandbySegment(SegmentName(generation, i), duration, bytes));
        }

        _activeSet = new StandbySegmentSet(generation, target, segments);
        _failedMediaItemId = null;
        _logger.LogInformation(
            "[LiveStreamStandbyProvider] Standby-Clip von MediaItem {MediaItemId} aus dem Cache geladen ({Count} Segmente).",
            target, segments.Count);
        return true;
    }

    private async Task<StandbySegmentSet?> LoadSetFromDirectoryAsync(
        string cacheDir, long target, IReadOnlyList<StandbyTranscodedSegment> segments, CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref _generation);
        var loaded = new List<StandbySegment>(segments.Count);
        for (var i = 0; i < segments.Count; i++)
        {
            var path = Path.Combine(cacheDir, segments[i].FileName);
            if (!File.Exists(path))
                return null;
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var duration = segments[i].Duration > TimeSpan.Zero ? segments[i].Duration : LiveStreamStandbySegmenter.NominalSegmentDuration;
            loaded.Add(new StandbySegment(SegmentName(generation, i), duration, bytes));
        }

        return new StandbySegmentSet(generation, target, loaded);
    }

    /// <summary>
    /// Cache-Invalidierung: der Marker muss zur frisch aufgeloesten Quelle passen — bei
    /// Datei-Quellen inklusive Aenderungsstempel, damit eine ersetzte Quelldatei desselben
    /// MediaItems nicht dauerhaft die alten Segmente ausliefert. HTTP-/Pipe-Quellen haben
    /// keinen Stempel; dort reicht der Deskriptor (die Item-Existenz ist bereits geprueft).
    /// </summary>
    private static bool CacheMarkerMatchesInput(StandbyCacheMarker marker, LiveStreamInput input)
    {
        if (!string.Equals(marker.Source, SourceDescriptor(input), StringComparison.Ordinal))
            return false;
        if (input.FilePath is not { } sourcePath)
            return true;
        return File.Exists(sourcePath)
            && marker.SourceWriteTimeUtc == File.GetLastWriteTimeUtc(sourcePath);
    }

    /// <summary>The stored source descriptor of an input: file path, or a marker word.</summary>
    private static string SourceDescriptor(LiveStreamInput input)
        => input.FilePath ?? (input.HttpUrl is not null ? "http" : "stream");

    private void WriteCacheMarker(string cacheDir, long target, LiveStreamInput input, IReadOnlyList<StandbyTranscodedSegment> segments)
    {
        try
        {
            var marker = new StandbyCacheMarker
            {
                MediaItemId = target,
                Source = SourceDescriptor(input),
                SourceWriteTimeUtc = input.FilePath is { } p && File.Exists(p) ? File.GetLastWriteTimeUtc(p) : (DateTime?)null,
                Files = segments.Select(s => new StandbyCacheFile
                {
                    FileName = s.FileName,
                    DurationSeconds = s.Duration.TotalSeconds
                }).ToList()
            };
            File.WriteAllText(Path.Combine(cacheDir, CacheMarkerFileName), JsonSerializer.Serialize(marker, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "[LiveStreamStandbyProvider] Cache-Marker konnte nicht geschrieben werden.");
        }
    }

    private StandbySegmentSet? LoadDefaultSetFromResources()
    {
        var assembly = typeof(LiveStreamStandbyProvider).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("LiveStreamStandby", StringComparison.Ordinal)
                     && n.EndsWith(".ts", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var segments = new List<StandbySegment>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            using var stream = assembly.GetManifestResourceStream(names[i]);
            if (stream is null)
                continue;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var duration = i < DefaultSegmentDurations.Length ? DefaultSegmentDurations[i] : LiveStreamStandbySegmenter.NominalSegmentDuration;
            segments.Add(new StandbySegment(SegmentName(0, i), duration, memory.ToArray()));
        }

        return segments.Count == 0 ? null : new StandbySegmentSet(0, null, segments);
    }

    private string BuildPlaylistText(StandbySegmentSet set, DateTimeOffset utcNow)
    {
        var count = set.Segments.Count;
        var totalSeconds = set.TotalDuration.TotalSeconds;
        var elapsed = Math.Max(0, (utcNow - _origin).TotalSeconds);
        var loopIndex = (long)Math.Floor(elapsed / totalSeconds);
        var positionInLoop = elapsed - loopIndex * totalSeconds;

        var index = 0;
        double accumulated = 0;
        while (index < count - 1 && accumulated + set.Segments[index].Duration.TotalSeconds <= positionInLoop)
        {
            accumulated += set.Segments[index].Duration.TotalSeconds;
            index++;
        }

        var mediaSequence = loopIndex * count + index;
        var segmentStart = utcNow - TimeSpan.FromSeconds(positionInLoop - accumulated);
        var window = Math.Min(PlaylistWindowSegmentCount, count);
        var targetDuration = (int)Math.Ceiling(set.Segments.Max(s => s.Duration.TotalSeconds));

        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");
        builder.AppendLine("#EXT-X-VERSION:3");
        builder.AppendLine($"#EXT-X-TARGETDURATION:{targetDuration.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{mediaSequence.ToString(CultureInfo.InvariantCulture)}");
        for (var w = 0; w < window; w++)
        {
            var segment = set.Segments[(index + w) % count];
            builder.AppendLine($"#EXT-X-PROGRAM-DATE-TIME:{segmentStart.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}");
            builder.AppendLine($"#EXTINF:{segment.Duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)},");
            builder.AppendLine(segment.Name);
            segmentStart += segment.Duration;
        }

        return builder.ToString();
    }

    private sealed class StandbyCacheMarker
    {
        public long MediaItemId { get; set; }
        public string? Source { get; set; }
        public DateTime? SourceWriteTimeUtc { get; set; }
        public List<StandbyCacheFile> Files { get; set; } = [];
    }

    private sealed class StandbyCacheFile
    {
        public string FileName { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
    }
}
