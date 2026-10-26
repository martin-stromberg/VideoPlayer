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
/// The HLS muxer appends to the existing playlist (<c>append_list</c>), so a restarted run —
/// slot change, drift resync or crash recovery — keeps the prior segments listed, continues
/// the media sequence and marks the boundary with <c>#EXT-X-DISCONTINUITY</c> instead of
/// resetting the playlist (which froze hls.js players at the transition, N17).
/// </summary>
public sealed class FfmpegLiveStreamSegmenter : ILiveStreamSegmenter
{
    /// <summary>The HLS playlist file name inside the segment directory.</summary>
    public const string PlaylistFileName = "live.m3u8";

    /// <summary>The number of segments the rolling playlist window lists.</summary>
    internal const int PlaylistWindowSegmentCount = 12;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Grace period to wait for the exit code once stderr reaches end of stream.</summary>
    private static readonly TimeSpan ExitDetectionTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<FfmpegLiveStreamSegmenter> _logger;
    private readonly Func<ProcessStartInfo, ISegmenterProcess?> _startProcess;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ISegmenterProcess? _process;
    private LiveStreamJob? _job;
    private DateTimeOffset _startedAt;
    private volatile LiveStreamExitInfo? _lastUnexpectedExit;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegLiveStreamSegmenter"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public FfmpegLiveStreamSegmenter(ILogger<FfmpegLiveStreamSegmenter> logger, TimeProvider? timeProvider = null)
        : this(logger, SegmenterProcessSupport.StartRealProcess, timeProvider)
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

    /// <inheritdoc />
    public LiveStreamExitInfo? LastUnexpectedExit => _lastUnexpectedExit;

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
            _lastUnexpectedExit = null;
            _logger.LogInformation(
                "[FfmpegLiveStreamSegmenter] Segmentierung fuer Sendeplatz {SlotId} gestartet (Position {Position}s).",
                job.SlotId, job.PositionSeconds);
            _logger.LogDebug(
                "[FfmpegLiveStreamSegmenter] ffmpeg-Aufruf: {Arguments}",
                InternalMediaStreamTokenService.MaskStreamTokenInText(
                    SftpProbeUriBuilder.MaskCredentialsInText(startInfo.Arguments)));

            _ = Task.Run(() => DrainStandardErrorAsync(process, job, _startedAt), CancellationToken.None);
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
    /// Drains the ffmpeg process's stderr continuously (the loop itself lives in
    /// <see cref="SegmenterProcessSupport"/>) and classifies the end of the stream: when it
    /// ends while the process is still the current job and it finished well before the
    /// slot's remaining air time, the exit was unexpected — its exit code and the captured
    /// stderr tail are logged (stream tokens and credentials masked) and reported through
    /// <see cref="LastUnexpectedExit"/>, because a silently dying ffmpeg was the N15
    /// root-cause blind spot.
    /// </summary>
    /// <param name="process">The process whose stderr is drained.</param>
    /// <param name="job">The job the process was started for.</param>
    /// <param name="startedAt">When the process was started (premature-exit reference).</param>
    private async Task DrainStandardErrorAsync(ISegmenterProcess process, LiveStreamJob job, DateTimeOffset startedAt)
    {
        var tail = await SegmenterProcessSupport.DrainStandardErrorAsync(process);
        ReportProcessExit(process, job, startedAt, tail);
    }

    /// <summary>
    /// Classifies a just-ended process: deliberate stops (<see cref="StopCoreAsync"/> clears
    /// <see cref="_process"/> before killing) and runs reaching the end of the slot's air time
    /// are expected — anything earlier is an unexpected exit worth a warning and the
    /// <see cref="LastUnexpectedExit"/> diagnosis for the coordinator's input fallback.
    /// </summary>
    /// <param name="process">The process whose stderr stream just ended.</param>
    /// <param name="job">The job the process was started for.</param>
    /// <param name="startedAt">When the process was started.</param>
    /// <param name="stderrTail">The captured stderr tail (unmasked).</param>
    private void ReportProcessExit(ISegmenterProcess process, LiveStreamJob job, DateTimeOffset startedAt, string stderrTail)
    {
        var maskedTail = InternalMediaStreamTokenService.MaskStreamTokenInText(
            SftpProbeUriBuilder.MaskCredentialsInText(stderrTail));

        var exitCode = ReadExitCode(process);
        if (!ReferenceEquals(_process, process))
        {
            _logger.LogDebug(
                "[FfmpegLiveStreamSegmenter] ffmpeg-Prozess fuer Sendeplatz {SlotId} wurde angehalten (Exit-Code {ExitCode}).",
                job.SlotId, exitCode);
            return;
        }

        // A run that ends within the last moments of the slot's air time finished as
        // planned (-t bound) — only a substantially earlier exit is a failure.
        var elapsed = _timeProvider.GetUtcNow() - startedAt;
        var tolerance = TimeSpan.FromSeconds(Math.Max(10, 2 * job.SegmentSeconds));
        if (elapsed.TotalSeconds >= job.RemainingSeconds - tolerance.TotalSeconds)
        {
            _logger.LogDebug(
                "[FfmpegLiveStreamSegmenter] ffmpeg-Prozess fuer Sendeplatz {SlotId} endete planmaessig (Exit-Code {ExitCode}).",
                job.SlotId, exitCode);
            return;
        }

        var httpTransportFailure = job.Input.HttpUrl is not null
            && FfmpegErrorMarkers.ContainsHttpTransportError(maskedTail);

        _lastUnexpectedExit = new LiveStreamExitInfo(
            job.SlotId, exitCode, httpTransportFailure, _timeProvider.GetUtcNow());

        _logger.LogWarning(
            "[FfmpegLiveStreamSegmenter] ffmpeg-Prozess fuer Sendeplatz {SlotId} beendete sich unerwartet nach {Elapsed:F1}s (Exit-Code {ExitCode}, HTTP-Transportfehler: {HttpTransportFailure}). stderr: {StderrTail}",
            job.SlotId, elapsed.TotalSeconds, exitCode, httpTransportFailure,
            string.IsNullOrWhiteSpace(maskedTail) ? "(leer)" : maskedTail.Trim());
    }

    private int ReadExitCode(ISegmenterProcess process)
    {
        try
        {
            if (!process.HasExited)
                process.WaitForExitAsync(CancellationToken.None).Wait(ExitDetectionTimeout);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private async Task PumpInputAsync(ISegmenterProcess process, LiveStreamJob job)
    {
        var outcome = await SegmenterProcessSupport.PumpInputAsync(
            process, job.Input, _logger, $"FfmpegLiveStreamSegmenter (Slot {job.SlotId})");
        if (outcome == SegmenterPumpOutcome.SourceUnavailable)
            _logger.LogWarning("[FfmpegLiveStreamSegmenter] Eingabe-Stream fuer Sendeplatz {SlotId} nicht verfuegbar.", job.SlotId);
    }

    /// <summary>
    /// Builds the ffmpeg argument string of a job: realtime read (<c>-re</c>), start offset
    /// (<c>-ss</c>), H.264/AAC transcode with segment-aligned forced keyframes, rolling HLS
    /// window with self-deleting segments that continues across runs (<c>append_list</c>,
    /// with <c>discont_start</c> marking each run's first segment and
    /// <c>program_date_time</c> anchoring the wall clock for live-edge sync), unique per-run
    /// segment names and a duration bound (<c>-t</c>) covering the slot's remaining air time.
    /// </summary>
    /// <param name="job">The segmentation job.</param>
    /// <returns>The argument string.</returns>
    internal static string BuildArguments(LiveStreamJob job)
    {
        var position = job.PositionSeconds.ToString("F3", CultureInfo.InvariantCulture);
        var remaining = Math.Max(1, job.RemainingSeconds).ToString("F3", CultureInfo.InvariantCulture);
        // The timestamp is formatted in C# — a strftime `%Y…` pattern cannot coexist with the
        // `%0Nd` segment counter under `-strftime 1` (`%0` is an invalid strftime conversion
        // and aborts the muxer with EINVAL on the very first segment).
        var segmentPattern = Path.Combine(job.SegmentDirectory, $"seg_{DateTime.Now:yyyyMMddHHmmss}_{job.SlotId}_%05d.ts");
        var playlist = Path.Combine(job.SegmentDirectory, PlaylistFileName);

        return "-hide_banner -loglevel warning -y "
            + $"{SegmenterProcessSupport.NoStdinArg(job.Input)}-re -ss {position} -i {SegmenterProcessSupport.InputSpec(job.Input)} "
            + SegmenterProcessSupport.TranscodeArgs(job.SegmentSeconds)
            + $"-f hls -hls_time {job.SegmentSeconds} -hls_list_size {PlaylistWindowSegmentCount} "
            + "-hls_flags delete_segments+independent_segments+append_list+discont_start+program_date_time "
            + $"-hls_segment_filename \"{segmentPattern}\" "
            + $"-t {remaining} \"{playlist}\"";
    }
}
