using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Probes media file durations by invoking an external <c>ffprobe</c> process.
/// </summary>
public class FfprobeMediaDurationProbe : IMediaDurationProbe
{
    /// <summary>
    /// ffprobe input argument that reads the media from standard input.
    /// </summary>
    internal const string StandardInputArgument = "pipe:0";

    // HTTP transport, unsupported-scheme and demux error classification is shared with
    // the live stream segmenter and lives in FfmpegErrorMarkers — for an http(s) input
    // transport markers mean the URI itself cannot be served (UriUnsupported), scheme
    // markers latch the scheme as unsupported, an sftp transport/I-O abort is transient
    // like in the segmenter, and demux markers prove a permanently broken file — but only
    // for a seekable input, since piped input produces the same messages when the stream
    // aborts early.

    private readonly IFfprobeLocator _locator;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<FfprobeMediaDurationProbe> _logger;
    private readonly Func<string, string, Stream?, TimeSpan, CancellationToken, Task<FfprobeInvocationResult>> _executeAsync;

    // Once an ffprobe build has rejected an URI scheme, every later probe of that scheme
    // would fail the same way — remembered per scheme so no further dead process invocations
    // are spawned (e.g. a missing sftp protocol must not disable http probing).
    private readonly ConcurrentDictionary<string, byte> _unsupportedSchemes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="FfprobeMediaDurationProbe"/> class.
    /// </summary>
    /// <param name="locator">ffprobe executable locator.</param>
    /// <param name="options">Media probe options.</param>
    /// <param name="logger">Logger instance.</param>
    public FfprobeMediaDurationProbe(
        IFfprobeLocator locator,
        IOptions<MediaProbeOptions> options,
        ILogger<FfprobeMediaDurationProbe> logger)
        : this(locator, options, logger, ExecuteProcessAsync)
    {
    }

    internal FfprobeMediaDurationProbe(
        IFfprobeLocator locator,
        IOptions<MediaProbeOptions> options,
        ILogger<FfprobeMediaDurationProbe> logger,
        Func<string, string, Stream?, TimeSpan, CancellationToken, Task<FfprobeInvocationResult>> executeAsync)
    {
        _locator = locator;
        _options = options.Value;
        _logger = logger;
        _executeAsync = executeAsync;
    }

    /// <inheritdoc />
    public Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
        => ProbeAsync($"\"{filePath}\"", $"\"{filePath}\"", stdin: null, TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds), seekable: true, httpInput: false, scheme: null, cancellationToken);

    /// <inheritdoc />
    public Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
    {
        var scheme = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Scheme : null;
        if (scheme is not null && _unsupportedSchemes.ContainsKey(scheme))
            return Task.FromResult(MediaDurationProbeResult.UriUnsupportedFailure);

        return ProbeAsync(
            $"\"{uri}\"",
            MaskProbeUri(uri),
            stdin: null,
            TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds),
            seekable: true,
            scheme is not null && scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase),
            scheme,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
        => ProbeAsync(StandardInputArgument, StandardInputArgument, input, TimeSpan.FromSeconds(_options.EffectiveStreamTimeoutSeconds), seekable: false, httpInput: false, scheme: null, cancellationToken);

    /// <summary>
    /// Masks credentials <em>and</em> media-stream tokens in a probe URI for log output — the
    /// internal stream token travels in the URI path, so credential-only masking is not enough.
    /// </summary>
    private static string MaskProbeUri(string uri)
        => InternalMediaStreamTokenService.MaskStreamTokenInText(SftpProbeUriBuilder.MaskCredentials(uri));

    /// <param name="inputArgument">The input argument handed to ffprobe — may contain credentials.</param>
    /// <param name="displayInput">Credential-masked representation of <paramref name="inputArgument"/> used for all log output.</param>
    /// <param name="stdin">Stream to pump into ffprobe's standard input, or <see langword="null"/>.</param>
    /// <param name="timeout">Maximum runtime of the ffprobe invocation.</param>
    /// <param name="seekable">
    /// Whether ffprobe can seek in the input (file path or seekable protocol such as
    /// <c>sftp://</c>). Only then do permanent stderr markers prove a broken file — a piped
    /// stream produces the same errors when the feed aborts early.
    /// </param>
    /// <param name="httpInput">
    /// Whether the input is an <c>http(s)</c> URI. Only then do HTTP transport errors prove
    /// the URI itself cannot be served (endpoint unreachable or rejecting), so the result is
    /// <see cref="MediaDurationProbeResult.UriUnsupported"/> — for other URI schemes or piped
    /// input the same words in stderr mean something else.
    /// </param>
    /// <param name="scheme">
    /// URI scheme of the input (<c>http</c>, <c>sftp</c>, …) or <see langword="null"/> for
    /// file/stdin inputs — used to remember per-scheme "protocol not supported" verdicts.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The probe outcome.</returns>
    private async Task<MediaDurationProbeResult> ProbeAsync(
        string inputArgument,
        string displayInput,
        Stream? stdin,
        TimeSpan timeout,
        bool seekable,
        bool httpInput,
        string? scheme,
        CancellationToken cancellationToken)
    {
        var ffprobePath = _locator.Resolve();
        if (ffprobePath is null)
        {
            _logger.LogWarning("ffprobe wurde nicht gefunden; Laufzeitermittlung übersprungen.");
            return MediaDurationProbeResult.TransientFailure;
        }

        var arguments = BuildArguments(inputArgument);

        FfprobeInvocationResult result;
        try
        {
            result = await _executeAsync(ffprobePath, arguments, stdin, timeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe-Aufruf für '{Input}' fehlgeschlagen.", displayInput);
            return MediaDurationProbeResult.TransientFailure;
        }

        if (result.TimedOut)
        {
            _logger.LogWarning("ffprobe-Aufruf für '{Input}' hat das Timeout von {Timeout}s überschritten.", displayInput, timeout.TotalSeconds);
            return MediaDurationProbeResult.TransientFailure;
        }

        if (result.ExitCode != 0)
        {
            // ffprobe echoes the input URI (including embedded credentials or the stream
            // token in the path) in error output — mask before logging or inspecting.
            var stderr = InternalMediaStreamTokenService.MaskStreamTokenInText(
                SftpProbeUriBuilder.MaskCredentialsInText(result.StandardError));

            if (scheme is not null && FfmpegErrorMarkers.ContainsUnsupportedSchemeError(stderr))
            {
                _unsupportedSchemes[scheme] = 1;
                _logger.LogWarning(
                    "ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode}: das Protokoll '{Scheme}' wird von diesem ffprobe-Build nicht unterstützt ({Error}); künftige URI-Probes dieses Schemas werden übersprungen.",
                    displayInput, result.ExitCode, scheme, Truncate(stderr));
                return MediaDurationProbeResult.UriUnsupportedFailure;
            }

            if (httpInput && FfmpegErrorMarkers.ContainsHttpTransportError(stderr))
            {
                _logger.LogWarning(
                    "ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode} und einem HTTP-Transportfehler ({Error}) — es wird auf andere Eingabemethoden zurückgefallen.",
                    displayInput, result.ExitCode, Truncate(stderr));
                return MediaDurationProbeResult.UriUnsupportedFailure;
            }

            // For sftp probes transport wins like in the segmenter: a transfer that
            // aborts mid-way produces the same demux complaints ("Could not find codec
            // parameters") as a corrupt file — but it is transient, so a permanent
            // classification must not withdraw the item from retries.
            if (string.Equals(scheme, "sftp", StringComparison.OrdinalIgnoreCase)
                && FfmpegErrorMarkers.ContainsSftpInputError(stderr))
            {
                _logger.LogWarning(
                    "ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode} und einem sftp-Transportfehler ({Error}) — der Probe wird später erneut versucht.",
                    displayInput, result.ExitCode, Truncate(stderr));
                return MediaDurationProbeResult.TransientFailure;
            }

            if (seekable && FfmpegErrorMarkers.ContainsUndecodableInputError(stderr))
            {
                _logger.LogWarning(
                    "ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode} und einem dauerhaften Fehler ({Error}) — die Datei wird nicht erneut geprüft.",
                    displayInput, result.ExitCode, Truncate(stderr));
                return MediaDurationProbeResult.PermanentFailure;
            }

            _logger.LogWarning(
                "ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode}: {Error}",
                displayInput, result.ExitCode, Truncate(stderr));
            return MediaDurationProbeResult.TransientFailure;
        }

        var duration = TryParseDurationOutput(result.StandardOutput);
        if (duration is null)
        {
            _logger.LogWarning(
                "ffprobe-Aufruf für '{Input}' lieferte keine bestimmbare Laufzeit (dauerhaft fehlgeschlagen).",
                displayInput);
            return MediaDurationProbeResult.PermanentFailure;
        }

        return MediaDurationProbeResult.Success(duration.Value);
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200] + "…";
    }

    /// <summary>
    /// Builds the ffprobe argument string that requests only the container duration.
    /// </summary>
    /// <param name="inputArgument">Input specification (quoted file path, quoted URI or <c>pipe:0</c>).</param>
    /// <returns>The argument string for the ffprobe process.</returns>
    internal static string BuildArguments(string inputArgument)
        => $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {inputArgument}";

    /// <summary>
    /// Parses ffprobe's raw duration output (a decimal number of seconds) into a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="output">The standard output of the ffprobe process.</param>
    /// <returns>The parsed duration, or <c>null</c> for empty, <c>N/A</c> or non-numeric output.</returns>
    internal static TimeSpan? TryParseDurationOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmed = output.Trim();
        if (string.Equals(trimmed, "N/A", StringComparison.OrdinalIgnoreCase))
            return null;

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static async Task<FfprobeInvocationResult> ExecuteProcessAsync(
        string fileName,
        string arguments,
        Stream? stdin,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return new FfprobeInvocationResult(-1, string.Empty, string.Empty, TimedOut: false);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var pumpTask = stdin is null
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                try
                {
                    await stdin.CopyToAsync(process.StandardInput.BaseStream, CancellationToken.None);
                }
                catch (Exception)
                {
                    // Process exited early or stdin broke; the exit result decides the outcome.
                }
                finally
                {
                    try { process.StandardInput.Close(); } catch (Exception) { }
                }
            }, CancellationToken.None);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            TryKill(process);
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, pumpTask);
        }
        catch (Exception)
        {
            // Output collection is best-effort after process termination.
        }

        return new FfprobeInvocationResult(
            process.HasExited ? process.ExitCode : -1,
            await SafeResult(stdoutTask),
            await SafeResult(stderrTask),
            timedOut);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // Process already exited or could not be killed.
        }
    }

    private static async Task<string> SafeResult(Task<string> readTask)
        => readTask.IsCompletedSuccessfully ? await readTask : string.Empty;
}

/// <summary>
/// Result of a single ffprobe process invocation.
/// </summary>
/// <param name="ExitCode">Process exit code (-1 when the process could not be started).</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error (diagnostics only).</param>
/// <param name="TimedOut">Whether the invocation was killed after the configured timeout.</param>
internal sealed record FfprobeInvocationResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
