using System;

namespace VideoWebPlayer.Services;

/// <summary>
/// Shared stderr markers of the ffmpeg/ffprobe tool family: phrases that — for an
/// <c>http(s)</c> input — mean the HTTP layer itself failed (endpoint unreachable, rejected
/// response status), so the file content was never reached. Callers treat such failures as
/// "the HTTP input cannot be served" and fall back to another input method instead of
/// misreading them as a defective file or a retryable read hiccup. For other URI schemes or
/// piped input the same words can mean something else, so the markers must only be applied
/// to <c>http(s)</c> inputs.
/// </summary>
internal static class FfmpegErrorMarkers
{
    /// <summary>The stderr phrases that classify an HTTP transport failure.</summary>
    internal static readonly string[] HttpTransportErrorMarkers =
    {
        "HTTP error",
        "Server returned",
        "Connection refused",
        "Connection timed out",
        "No route to host",
        "Failed to connect"
    };

    /// <summary>
    /// Determines whether the given stderr output contains an HTTP transport error marker.
    /// </summary>
    /// <param name="stderr">The captured stderr output (may be <see langword="null"/>).</param>
    /// <returns><see langword="true"/> when a transport marker was found.</returns>
    internal static bool ContainsHttpTransportError(string? stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return false;

        foreach (var marker in HttpTransportErrorMarkers)
        {
            if (stderr.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
