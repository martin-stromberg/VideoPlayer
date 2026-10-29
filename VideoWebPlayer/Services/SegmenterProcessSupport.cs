using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>
/// The result of pumping a stream input into a segmenter process's standard input.
/// </summary>
internal enum SegmenterPumpOutcome
{
    /// <summary>The input stream was pumped to its end (or the pipe broke while the
    /// process ended — expected during stops).</summary>
    Completed,

    /// <summary>The input stream could not be opened (source unavailable).</summary>
    SourceUnavailable,

    /// <summary>An unexpected error occurred while pumping (already logged by the helper).</summary>
    Failed
}

/// <summary>
/// Shared plumbing of the ffmpeg segmenter processes (<see cref="FfmpegLiveStreamSegmenter"/>
/// and <see cref="LiveStreamStandbySegmenter"/>): process start, the <c>-i</c> input
/// specification (<c>FilePath</c>/HTTP-URL/<c>pipe:0</c> plus <c>-nostdin</c>), the common
/// H.264/AAC transcode argument block, the stderr tail drain and the stdin pump.
/// </summary>
internal static class SegmenterProcessSupport
{
    /// <summary>The maximum stderr tail kept in memory for the exit diagnosis.</summary>
    internal const int StderrTailCapacityChars = 8192;

    /// <summary>Grace period to collect the stdin pump task once the process ended —
    /// a source blocked in <c>ReadAsync</c> must not hang the caller's teardown (the
    /// pump's <c>CopyToAsync</c> runs without a cancellation token).</summary>
    internal static readonly TimeSpan PumpDrainTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Starts a real <see cref="Process"/> wrapped as <see cref="ISegmenterProcess"/>, or
    /// <see langword="null"/> when the start fails — the failure cause (e.g.
    /// <see cref="System.ComponentModel.Win32Exception"/> „nicht gefunden" vs. „Zugriff
    /// verweigert") is logged so the caller's contextless message stays diagnosable.
    /// </summary>
    /// <param name="startInfo">The process start description.</param>
    /// <param name="logger">Logger for the start failure.</param>
    /// <param name="caller">Log prefix identifying the calling segmenter.</param>
    internal static ISegmenterProcess? StartRealProcess(ProcessStartInfo startInfo, ILogger logger, string caller)
    {
        try
        {
            return new SegmenterProcess(Process.Start(startInfo)!);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Caller}] Prozess '{FileName}' konnte nicht gestartet werden.", caller, startInfo.FileName);
            return null;
        }
    }

    /// <summary>
    /// The ffmpeg <c>-i</c> argument value of an input: quoted file path, quoted HTTP URL
    /// or <c>pipe:0</c> for a piped stream.
    /// </summary>
    internal static string InputSpec(LiveStreamInput input)
        => input.FilePath is { } path
            ? $"\"{path}\""
            : input.HttpUrl is { } url
                ? $"\"{url}\""
                : "pipe:0";

    /// <summary>
    /// <c>-nostdin </c> (with trailing space) for file/URL inputs so ffmpeg cannot read the
    /// inherited console input; empty for pipe inputs, which use stdin for the media data.
    /// </summary>
    internal static string NoStdinArg(LiveStreamInput input)
        => input.IsPipe ? string.Empty : "-nostdin ";

    /// <summary>
    /// The shared transcode block: first video stream, optional audio, H.264 veryfast /
    /// AAC 128k, forced keyframes aligned to the segment length (trailing space included).
    /// </summary>
    internal static string TranscodeArgs(int segmentSeconds)
        => "-map 0:v:0 -map 0:a:0? "
            + "-c:v libx264 -preset veryfast -c:a aac -b:a 128k "
            + $"-force_key_frames \"expr:gte(t,n_forced*{segmentSeconds})\" ";

    /// <summary>
    /// Drains the process's stderr continuously — ffmpeg writes its progress and all
    /// diagnostics there; a pipe buffer that is never read stalls the process. Returns the
    /// last <see cref="StderrTailCapacityChars"/> characters as tail for error diagnosis.
    /// </summary>
    internal static async Task<string> DrainStandardErrorAsync(ISegmenterProcess process)
    {
        var tail = new StringBuilder(StderrTailCapacityChars);
        try
        {
            using var reader = new StreamReader(process.StandardError);
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                tail.Append(buffer, 0, read);
                if (tail.Length > StderrTailCapacityChars)
                    tail.Remove(0, tail.Length - StderrTailCapacityChars);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }

        return tail.ToString();
    }

    /// <summary>
    /// Pumps a piped <see cref="LiveStreamInput"/> into the process's standard input until
    /// the source ends; the stdin handle is always closed afterwards so ffmpeg sees EOF.
    /// A broken pipe while the process ends is expected and counts as completed.
    /// </summary>
    /// <param name="process">The running segmenter process.</param>
    /// <param name="input">The input to pump (must be a pipe input).</param>
    /// <param name="logger">Logger for pump diagnostics.</param>
    /// <param name="caller">Log prefix identifying the calling segmenter (and its context).</param>
    internal static async Task<SegmenterPumpOutcome> PumpInputAsync(
        ISegmenterProcess process, LiveStreamInput input, ILogger logger, string caller)
    {
        try
        {
            var source = await input.OpenStreamAsync!(CancellationToken.None);
            if (source is null)
                return SegmenterPumpOutcome.SourceUnavailable;

            await using (source)
            {
                await source.CopyToAsync(process.StandardInput, CancellationToken.None);
            }

            return SegmenterPumpOutcome.Completed;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The process ending or being killed breaks the pipe — expected during stops,
            // so only the exception type and short message are logged, not the stack trace.
            logger.LogDebug("[{Caller}] Eingabe-Pump beendet ({ExceptionType}: {Reason}).",
                caller, ex.GetType().Name, ex.Message);
            return SegmenterPumpOutcome.Completed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Caller}] Fehler beim Pumpen der Eingabe.", caller);
            return SegmenterPumpOutcome.Failed;
        }
        finally
        {
            try { process.StandardInput.Close(); } catch (Exception) { }
        }
    }

    /// <summary>
    /// Awaits a diagnostic side task (stdin pump, stderr drain) bounded by
    /// <paramref name="timeout"/>: the task only accompanies the process teardown, so a
    /// stalled one must neither hang nor a faulted one break the regular stop. A task
    /// still running past the bound is abandoned with a warning — a later fault is
    /// observed so it does not linger as an unobserved task exception; a faulted task
    /// is likewise logged and swallowed. Returns <see langword="true"/> when the task
    /// completed within the bound.
    /// </summary>
    /// <param name="drainTask">The side task to await.</param>
    /// <param name="timeout">The bounded wait.</param>
    /// <param name="logger">Logger for the timeout/fault diagnostics.</param>
    /// <param name="drainDescription">Log prefix naming the drained task (with caller context).</param>
    internal static async Task<bool> AwaitDrainObservingFaultsAsync(
        Task drainTask, TimeSpan timeout, ILogger logger, string drainDescription)
    {
        try
        {
            await drainTask.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            // Eine spaetere Faultierung des verwaisten Tasks wird beobachtet, damit sie
            // nicht als unbehandelte Task-Ausnahme liegen bleibt.
            _ = drainTask.ContinueWith(
                t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            logger.LogWarning("{Drain} nach {TimeoutSeconds}s nicht eingeholt — laeuft verwaist weiter.",
                drainDescription, timeout.TotalSeconds);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Drain} endete fehlerhaft.", drainDescription);
            return false;
        }
    }

    /// <summary>
    /// <see cref="AwaitDrainObservingFaultsAsync(Task, TimeSpan, ILogger, string)"/> for a
    /// side task producing a result, returned on success — or <see langword="default"/>
    /// when the drain did not complete within the bound.
    /// </summary>
    internal static async Task<T?> AwaitDrainObservingFaultsAsync<T>(
        Task<T> drainTask, TimeSpan timeout, ILogger logger, string drainDescription)
        => await AwaitDrainObservingFaultsAsync((Task)drainTask, timeout, logger, drainDescription)
            ? drainTask.Result
            : default;
}
