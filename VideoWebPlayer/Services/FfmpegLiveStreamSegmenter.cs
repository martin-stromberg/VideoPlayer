using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>
/// Abstraction over the running ffmpeg process so tests can substitute a fake process
/// (mirrors the <see cref="FfprobeMediaDurationProbe"/> injectable-execution pattern).
/// </summary>
internal interface ISegmenterProcess : IDisposable
{
    /// <summary>Gets a value indicating whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Gets the process exit code, or -1 while running.</summary>
    int ExitCode { get; }

    /// <summary>Gets the process standard input stream for piped media input.</summary>
    Stream StandardInput { get; }

    /// <summary>Gets the process standard error stream (must be drained to avoid pipe-buffer stalls).</summary>
    Stream StandardError { get; }

    /// <summary>Waits for the process to exit.</summary>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Kills the process and its children.</summary>
    void Kill();
}

/// <summary>
/// Default <see cref="ISegmenterProcess"/> over <see cref="System.Diagnostics.Process"/>.
/// </summary>
internal sealed class SegmenterProcess : ISegmenterProcess
{
    private readonly Process _process;

    /// <summary>Wraps a started process.</summary>
    public SegmenterProcess(Process process) => _process = process;

    /// <inheritdoc />
    public bool HasExited => _process.HasExited;

    /// <inheritdoc />
    public int ExitCode => _process.HasExited ? _process.ExitCode : -1;

    /// <inheritdoc />
    public Stream StandardInput => _process.StandardInput.BaseStream;

    /// <inheritdoc />
    public Stream StandardError => _process.StandardError.BaseStream;

    /// <inheritdoc />
    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _process.WaitForExitAsync(cancellationToken);

    /// <inheritdoc />
    public void Kill()
    {
        if (!_process.HasExited)
            _process.Kill(entireProcessTree: true);
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}

/// <summary>
/// Runs the single continuously segmenting ffmpeg process of the automated TV channel: the
/// process transcodes the currently airing broadcast to HLS in real time (<c>-re</c>), starting
/// at the slot's current playback position (<c>-ss</c>) and ending with the slot (<c>-t</c>).
/// Unique per-run segment names keep every restart a clean discontinuity for players.
/// </summary>
public sealed class FfmpegLiveStreamSegmenter : ILiveStreamSegmenter
{
    /// <summary>The HLS playlist file name inside the segment directory.</summary>
    public const string PlaylistFileName = "live.m3u8";

    /// <summary>The number of segments the rolling playlist window lists.</summary>
    internal const int PlaylistWindowSegmentCount = 12;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<FfmpegLiveStreamSegmenter> _logger;
    private readonly Func<ProcessStartInfo, ISegmenterProcess?> _startProcess;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ISegmenterProcess? _process;
    private LiveStreamJob? _job;
    private DateTimeOffset _startedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegLiveStreamSegmenter"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public FfmpegLiveStreamSegmenter(ILogger<FfmpegLiveStreamSegmenter> logger, TimeProvider? timeProvider = null)
        : this(logger, StartRealProcess, timeProvider)
    {
    }

    internal FfmpegLiveStreamSegmenter(
        ILogger<FfmpegLiveStreamSegmenter> logger,
        Func<ProcessStartInfo, ISegmenterProcess?> startProcess,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _startProcess = startProcess;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsRunning => _process is { HasExited: false };

    /// <inheritdoc />
    public long? CurrentSlotId => IsRunning ? _job?.SlotId : null;

    /// <summary>
    /// Gets the playback position the running process is expected to have reached, or
    /// <see langword="null"/> when no job is running.
    /// </summary>
    public double? ExpectedPositionSeconds => IsRunning && _job is not null
        ? _job.PositionSeconds + (_timeProvider.GetUtcNow() - _startedAt).TotalSeconds
        : null;

    /// <inheritdoc />
    public async Task StartAsync(LiveStreamJob job, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = job.FfmpegPath,
                Arguments = BuildArguments(job),
                RedirectStandardInput = job.Input.IsPipe,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = _startProcess(startInfo);
            if (process is null)
                throw new InvalidOperationException("Der ffmpeg-Prozess konnte nicht gestartet werden.");

            _process = process;
            _job = job;
            _startedAt = _timeProvider.GetUtcNow();
            _logger.LogInformation(
                "[FfmpegLiveStreamSegmenter] Segmentierung fuer Sendeplatz {SlotId} gestartet (Position {Position}s).",
                job.SlotId, job.PositionSeconds);

            _ = Task.Run(() => DrainStandardErrorAsync(process), CancellationToken.None);
            if (job.Input.IsPipe)
                _ = Task.Run(() => PumpInputAsync(process, job), CancellationToken.None);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var process = _process;
        var job = _job;
        _process = null;
        _job = null;

        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill();
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(StopTimeout);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "[FfmpegLiveStreamSegmenter] ffmpeg-Prozess fuer Sendeplatz {SlotId} konnte nicht sauber beendet werden.", job?.SlotId);
        }
        finally
        {
            process.Dispose();
        }

        if (job is not null)
            _logger.LogInformation("[FfmpegLiveStreamSegmenter] Segmentierung fuer Sendeplatz {SlotId} beendet.", job.SlotId);
    }

    /// <summary>
    /// Drain die stderr-Ausgabe des ffmpeg-Prozesses kontinuierlich weg: ffmpeg loggt seinen
    /// Fortschritt auf stderr — wird der Pipe-Puffer nie gelesen, blockiert der Prozess und der
    /// Live-Stream bleibt bis zum Drift-Neustart stehen.
    /// </summary>
    private async Task DrainStandardErrorAsync(ISegmenterProcess process)
    {
        try
        {
            var buffer = new byte[8192];
            while (await process.StandardError.ReadAsync(buffer, CancellationToken.None) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    private async Task PumpInputAsync(ISegmenterProcess process, LiveStreamJob job)
    {
        try
        {
            var source = await job.Input.OpenStreamAsync!(CancellationToken.None);
            if (source is null)
            {
                _logger.LogWarning("[FfmpegLiveStreamSegmenter] Eingabe-Stream fuer Sendeplatz {SlotId} nicht verfuegbar.", job.SlotId);
                return;
            }

            await using (source)
            {
                await source.CopyToAsync(process.StandardInput, CancellationToken.None);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The process ending or being killed breaks the pipe — expected during stops.
            _logger.LogDebug(ex, "[FfmpegLiveStreamSegmenter] Eingabe-Pump fuer Sendeplatz {SlotId} beendet.", job.SlotId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FfmpegLiveStreamSegmenter] Fehler beim Pumpen der Eingabe fuer Sendeplatz {SlotId}.", job.SlotId);
        }
        finally
        {
            try { process.StandardInput.Close(); } catch (Exception) { }
        }
    }

    /// <summary>
    /// Builds the ffmpeg argument string of a job: realtime read (<c>-re</c>), start offset
    /// (<c>-ss</c>), H.264/AAC transcode with segment-aligned forced keyframes, rolling HLS
    /// window with self-deleting segments, unique per-run segment names and a duration bound
    /// (<c>-t</c>) covering the slot's remaining air time.
    /// </summary>
    /// <param name="job">The segmentation job.</param>
    /// <returns>The argument string.</returns>
    internal static string BuildArguments(LiveStreamJob job)
    {
        var position = job.PositionSeconds.ToString("F3", CultureInfo.InvariantCulture);
        var remaining = Math.Max(1, job.RemainingSeconds).ToString("F3", CultureInfo.InvariantCulture);
        var input = job.Input.FilePath is { } path ? $"\"{path}\"" : "pipe:0";
        var noStdin = job.Input.IsPipe ? string.Empty : "-nostdin ";
        var segmentPattern = Path.Combine(job.SegmentDirectory, $"seg_%Y%m%d%H%M%S_{job.SlotId}_%05d.ts");
        var playlist = Path.Combine(job.SegmentDirectory, PlaylistFileName);

        return "-hide_banner -loglevel warning -y "
            + $"{noStdin}-re -ss {position} -i {input} "
            + "-map 0:v:0 -map 0:a:0? "
            + "-c:v libx264 -preset veryfast -c:a aac -b:a 128k "
            + $"-force_key_frames \"expr:gte(t,n_forced*{job.SegmentSeconds})\" "
            + $"-f hls -hls_time {job.SegmentSeconds} -hls_list_size {PlaylistWindowSegmentCount} "
            + "-hls_flags delete_segments+independent_segments "
            + $"-strftime 1 -hls_segment_filename \"{segmentPattern}\" "
            + $"-t {remaining} \"{playlist}\"";
    }

    private static ISegmenterProcess? StartRealProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return new SegmenterProcess(Process.Start(startInfo)!);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
