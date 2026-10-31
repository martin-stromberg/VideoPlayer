using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>
/// One produced standby segment of a configured warm-up clip.
/// </summary>
/// <param name="FileName">The segment file name inside the standby cache directory.</param>
/// <param name="Duration">The segment's media duration (ffprobe, nominal fallback).</param>
internal sealed record StandbyTranscodedSegment(string FileName, TimeSpan Duration);

/// <summary>
/// One-time ffmpeg transcode of a configured standby clip into pre-made HLS segments:
/// unlike <see cref="FfmpegLiveStreamSegmenter"/>, which runs continuously in real time,
/// this runs a bounded offline pass (<c>-f segment</c>, capped at
/// <see cref="MaxStandbyDurationSeconds"/>) into the standby cache directory
/// (<c>Data/LiveStreamStandby</c>), so the provider can hold the finished segments in
/// memory. The produced durations are probed per segment via ffprobe with a nominal
/// fallback, because the playlist's <c>#EXTINF</c> values must match the real media.
/// </summary>
public sealed class LiveStreamStandbySegmenter
{
    /// <summary>The maximum input duration transcoded into the standby set — a wrongly
    /// picked feature-length title must not blow up the in-memory standby set.</summary>
    internal const int MaxStandbyDurationSeconds = 60;

    /// <summary>The target segment length in seconds (keyframe-aligned).</summary>
    internal const int SegmentSeconds = 4;

    /// <summary>The segment muxer file pattern inside the cache directory.</summary>
    internal const string SegmentFilePattern = "standby_%03d.ts";

    /// <summary>Nominal fallback duration when ffprobe cannot probe a produced segment.</summary>
    internal static readonly TimeSpan NominalSegmentDuration = TimeSpan.FromSeconds(SegmentSeconds);

    /// <summary>
    /// Runtime bound of one transcode run: <c>-t</c> caps only the produced output duration,
    /// not the process runtime — a stalling input (hanging HTTP read, never-ending pipe
    /// stream) would otherwise let ffmpeg run forever and block the worker's start-up
    /// <see cref="LiveStreamStandbyProvider.RefreshAsync"/> await. Offline transcode is far
    /// faster than realtime, so twice the output cap is a generous stall bound.
    /// </summary>
    internal static readonly TimeSpan TranscodeTimeout = TimeSpan.FromSeconds(2 * MaxStandbyDurationSeconds);

    /// <summary>Grace period to collect the stderr drain once the process ended — a process
    /// that could not be killed must not hang <see cref="TranscodeAsync"/> forever.</summary>
    private static readonly TimeSpan StderrDrainTimeout = TimeSpan.FromSeconds(10);

    private readonly IFfmpegLocator _ffmpegLocator;
    private readonly IFfprobeLocator _ffprobeLocator;
    private readonly ILogger<LiveStreamStandbySegmenter> _logger;
    private readonly Func<ProcessStartInfo, ISegmenterProcess?> _startProcess;
    private readonly TimeSpan _transcodeTimeout;
    private readonly TimeSpan _stderrDrainTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStreamStandbySegmenter"/> class.
    /// </summary>
    /// <param name="ffmpegLocator">ffmpeg executable locator.</param>
    /// <param name="ffprobeLocator">ffprobe executable locator (segment durations).</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamStandbySegmenter(
        IFfmpegLocator ffmpegLocator,
        IFfprobeLocator ffprobeLocator,
        ILogger<LiveStreamStandbySegmenter> logger)
        : this(ffmpegLocator, ffprobeLocator, logger,
            startInfo => SegmenterProcessSupport.StartRealProcess(startInfo, logger, nameof(LiveStreamStandbySegmenter)))
    {
    }

    internal LiveStreamStandbySegmenter(
        IFfmpegLocator ffmpegLocator,
        IFfprobeLocator ffprobeLocator,
        ILogger<LiveStreamStandbySegmenter> logger,
        Func<ProcessStartInfo, ISegmenterProcess?> startProcess,
        TimeSpan? transcodeTimeout = null,
        TimeSpan? stderrDrainTimeout = null)
    {
        _ffmpegLocator = ffmpegLocator;
        _ffprobeLocator = ffprobeLocator;
        _logger = logger;
        _startProcess = startProcess;
        _transcodeTimeout = transcodeTimeout ?? TranscodeTimeout;
        _stderrDrainTimeout = stderrDrainTimeout ?? StderrDrainTimeout;
    }

    /// <summary>
    /// Transcodes the resolved standby input once into <c>standby_*.ts</c> segments inside
    /// <paramref name="cacheDirectory"/> (created when missing, stale <c>standby_*.ts</c>
    /// cleared first). Returns the produced segments with their probed durations, or
    /// <see langword="null"/> when ffmpeg is missing, the process fails or produces nothing.
    /// </summary>
    /// <param name="input">The resolved media input (file, HTTP URL or piped stream).</param>
    /// <param name="cacheDirectory">The standby cache directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The produced segment list, or <see langword="null"/>.</returns>
    internal async Task<IReadOnlyList<StandbyTranscodedSegment>?> TranscodeAsync(
        LiveStreamInput input, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        var ffmpeg = _ffmpegLocator.Resolve();
        if (ffmpeg is null)
        {
            _logger.LogWarning("[LiveStreamStandbySegmenter] ffmpeg nicht gefunden — Standby-Clip kann nicht vorbereitet werden.");
            return null;
        }

        Directory.CreateDirectory(cacheDirectory);
        // Nicht loeschbare Alt-Segmente (z. B. noch von einem Player gehalten) werden mit
        // ihrem Aenderungsstempel gemerkt: unveraendert gebliebene Reste duerfen sich
        // nicht in den neuen Satz mischen; ein vom neuen Lauf ueberschriebenes Segment
        // desselben Namens (neuer Stempel) gehoert dagegen zum Ergebnis.
        var staleLeftOver = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var stale in Directory.EnumerateFiles(cacheDirectory, "standby_*.ts"))
        {
            try
            {
                File.Delete(stale);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "[LiveStreamStandbySegmenter] Alt-Segment '{File}' konnte nicht geloescht werden.", stale);
                try { staleLeftOver[Path.GetFileName(stale)] = File.GetLastWriteTimeUtc(stale); }
                catch (Exception) { }
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = BuildArguments(input, cacheDirectory),
            RedirectStandardInput = input.IsPipe,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = _startProcess(startInfo);
        if (process is null)
        {
            _logger.LogWarning("[LiveStreamStandbySegmenter] Der ffmpeg-Prozess konnte nicht gestartet werden.");
            return null;
        }

        string stderrTail;
        using (process)
        {
            var stderrTask = SegmenterProcessSupport.DrainStandardErrorAsync(process);
            var pumpTask = input.IsPipe ? PumpInputAsync(process, input) : Task.CompletedTask;
            var timedOut = false;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(_transcodeTimeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    try { if (!process.HasExited) process.Kill(); } catch (Exception) { }
                    throw;
                }
                catch (OperationCanceledException)
                {
                    // Transcode-Timeout (stallender Input): Prozess töten, Fehlschlag melden.
                    timedOut = true;
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[LiveStreamStandbySegmenter] ffmpeg-Prozess konnte nach dem Transcode-Timeout nicht beendet werden.");
                    }
                }
            }

            // Der stderr-Drain endet erst mit dem Prozess — konnte ffmpeg nicht beendet
            // werden, wuerde ein unbegrenztes Warten TranscodeAsync wieder endlos haengen
            // (genau der Blockierpfad, den TranscodeTimeout beseitigen soll).
            stderrTail = await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
                stderrTask, _stderrDrainTimeout, _logger,
                "[LiveStreamStandbySegmenter] stderr-Drain des ffmpeg-Prozesses")
                ?? string.Empty;
            // Der Pump wartet auf das Quell-Ende — bei stallendem Input nur begrenzt.
            await SegmenterProcessSupport.AwaitDrainObservingFaultsAsync(
                pumpTask, SegmenterProcessSupport.PumpDrainTimeout, _logger,
                "[LiveStreamStandbySegmenter] Eingabe-Pumpe");

            var masked = InternalMediaStreamTokenService.MaskStreamTokenInText(
                SftpProbeUriBuilder.MaskCredentialsInText(stderrTail));
            if (timedOut)
            {
                _logger.LogWarning(
                    "[LiveStreamStandbySegmenter] ffmpeg ueberschritt das Transcode-Timeout von {TimeoutSeconds}s und wurde abgebrochen (stallender Input). stderr: {StderrTail}",
                    _transcodeTimeout.TotalSeconds,
                    string.IsNullOrWhiteSpace(masked) ? "(leer)" : masked.Trim());
                return null;
            }

            var exitCode = process.HasExited ? process.ExitCode : -1;
            if (exitCode != 0)
            {
                _logger.LogWarning(
                    "[LiveStreamStandbySegmenter] ffmpeg endete mit Exit-Code {ExitCode}. stderr: {StderrTail}",
                    exitCode, string.IsNullOrWhiteSpace(masked) ? "(leer)" : masked.Trim());
                return null;
            }
        }

        // Nur die von diesem Lauf produzierten Dateien aufnehmen: ein nicht loeschbares
        // Alt-Segment (unveraenderter Stempel) darf nicht unbemerkt in der Playlist landen.
        var files = Directory.GetFiles(cacheDirectory, "standby_*.ts")
            .Where(f => !staleLeftOver.TryGetValue(Path.GetFileName(f), out var stamp)
                || File.GetLastWriteTimeUtc(f) != stamp)
            .ToArray();
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length == 0)
        {
            _logger.LogWarning("[LiveStreamStandbySegmenter] ffmpeg erzeugte keine Standby-Segmente in '{CacheDirectory}'.", cacheDirectory);
            return null;
        }

        var result = new List<StandbyTranscodedSegment>(files.Length);
        foreach (var file in files)
        {
            var duration = await ProbeDurationAsync(file, cancellationToken) ?? NominalSegmentDuration;
            result.Add(new StandbyTranscodedSegment(Path.GetFileName(file), duration));
        }

        return result;
    }

    /// <summary>
    /// Builds the ffmpeg arguments for the one-time standby transcode: offline speed (no
    /// <c>-re</c>), keyframe-aligned <see cref="SegmentSeconds"/> segments via the segment
    /// muxer and a hard duration cap (<c>-t</c>) so a feature-length pick stays bounded.
    /// </summary>
    internal static string BuildArguments(LiveStreamInput input, string cacheDirectory)
    {
        var pattern = Path.Combine(cacheDirectory, SegmentFilePattern);
        return "-hide_banner -loglevel warning -y "
            + $"{SegmenterProcessSupport.NoStdinArg(input)}-i {SegmenterProcessSupport.InputSpec(input)} "
            + SegmenterProcessSupport.TranscodeArgs(SegmentSeconds)
            + $"-f segment -segment_time {SegmentSeconds} -reset_timestamps 1 "
            + $"-t {MaxStandbyDurationSeconds} \"{pattern}\"";
    }

    private async Task PumpInputAsync(ISegmenterProcess process, LiveStreamInput input)
    {
        var result = await SegmenterProcessSupport.PumpInputAsync(
            process, input, _logger, "LiveStreamStandbySegmenter");
        if (result.Outcome == SegmenterPumpOutcome.Completed)
            return;
        if (result.Outcome == SegmenterPumpOutcome.SourceUnavailable)
            _logger.LogWarning("[LiveStreamStandbySegmenter] Eingabe-Stream nicht verfuegbar.");
        // Ohne (vollstaendige) Eingabe haenge ffmpeg wuerde ewig auf stdin warten — toeten;
        // ein abgebrochener Pump (Truncated) darf ebenso kein Teilmaterial übernehmen.
        try { process.Kill(); } catch (Exception) { }
    }

    private async Task<TimeSpan?> ProbeDurationAsync(string file, CancellationToken cancellationToken)
    {
        var ffprobe = _ffprobeLocator.Resolve();
        if (ffprobe is null)
            return null;

        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobe,
            Arguments = FfprobeMediaDurationProbe.BuildArguments($"\"{file}\""),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        try
        {
            using var process = Process.Start(startInfo)!;
            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception) { }
                return null;
            }

            await Task.WhenAll(stdoutTask, stderrTask);
            return stdoutTask.IsCompletedSuccessfully
                ? FfprobeMediaDurationProbe.TryParseDurationOutput(stdoutTask.Result)
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
