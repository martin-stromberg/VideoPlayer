using System;
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

    private readonly IFfprobeLocator _locator;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<FfprobeMediaDurationProbe> _logger;
    private readonly Func<string, string, Stream?, TimeSpan, CancellationToken, Task<FfprobeInvocationResult>> _executeAsync;

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
    public Task<TimeSpan?> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
        => ProbeAsync($"\"{filePath}\"", stdin: null, cancellationToken);

    /// <inheritdoc />
    public Task<TimeSpan?> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
        => ProbeAsync(StandardInputArgument, input, cancellationToken);

    private async Task<TimeSpan?> ProbeAsync(string inputArgument, Stream? stdin, CancellationToken cancellationToken)
    {
        var ffprobePath = _locator.Resolve();
        if (ffprobePath is null)
        {
            _logger.LogWarning("ffprobe wurde nicht gefunden; Laufzeitermittlung übersprungen.");
            return null;
        }

        var arguments = BuildArguments(inputArgument);
        var timeout = TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds);

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
            _logger.LogWarning(ex, "ffprobe-Aufruf für '{Input}' fehlgeschlagen.", inputArgument);
            return null;
        }

        if (result.TimedOut)
        {
            _logger.LogWarning("ffprobe-Aufruf für '{Input}' hat das Timeout von {Timeout}s überschritten.", inputArgument, timeout.TotalSeconds);
            return null;
        }

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("ffprobe-Aufruf für '{Input}' endete mit Exit-Code {ExitCode}.", inputArgument, result.ExitCode);
            return null;
        }

        return TryParseDurationOutput(result.StandardOutput);
    }

    /// <summary>
    /// Builds the ffprobe argument string that requests only the container duration.
    /// </summary>
    /// <param name="inputArgument">Input specification (quoted file path or <c>pipe:0</c>).</param>
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
            return new FfprobeInvocationResult(-1, string.Empty, TimedOut: false);
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

        return new FfprobeInvocationResult(process.HasExited ? process.ExitCode : -1, await SafeResult(stdoutTask), timedOut);
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
/// <param name="TimedOut">Whether the invocation was killed after the configured timeout.</param>
internal sealed record FfprobeInvocationResult(int ExitCode, string StandardOutput, bool TimedOut);
