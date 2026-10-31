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

    /// <summary>
    /// The stderr phrases that classify an input ffmpeg could not demux at all: the file
    /// ended before the demuxer found usable stream parameters (e.g. an MP4 whose moov
    /// atom sits at the end of a non-seekable input) or the container is corrupt. Unlike
    /// the transport markers these apply on every input kind — file, URL and pipe.
    /// </summary>
    internal static readonly string[] UndecodableInputErrorMarkers =
    {
        "partial file",
        "Invalid data found when processing input",
        "Could not find codec parameters",
        "Cannot determine format",
        "moov atom not found"
    };

    /// <summary>
    /// Determines whether the given stderr output shows the input could not be demuxed —
    /// a content problem that no retry on the same input can fix.
    /// </summary>
    /// <param name="stderr">The captured stderr output (may be <see langword="null"/>).</param>
    /// <returns><see langword="true"/> when an undecodable-input marker was found.</returns>
    internal static bool ContainsUndecodableInputError(string? stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return false;

        foreach (var marker in UndecodableInputErrorMarkers)
        {
            if (stderr.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The stderr phrases that classify a transport or authentication failure of an
    /// <c>sftp://</c> input: connection problems, rejected credentials. Only meaningful
    /// for <c>sftp://</c> inputs — the same words on other inputs mean something else.
    /// </summary>
    internal static readonly string[] SftpInputErrorMarkers =
    {
        "Connection refused",
        "Connection timed out",
        "Connection failed",
        "No route to host",
        "Failed to connect",
        "Socket error",
        "I/O error",
        "Error opening input",
        "Permission denied",
        "Authentication"
    };

    /// <summary>
    /// Determines whether the given stderr output shows a transport or authentication
    /// failure of an <c>sftp://</c> input.
    /// </summary>
    /// <param name="stderr">The captured stderr output (may be <see langword="null"/>).</param>
    /// <returns><see langword="true"/> when an sftp transport/auth marker was found.</returns>
    internal static bool ContainsSftpInputError(string? stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return false;

        foreach (var marker in SftpInputErrorMarkers)
        {
            if (stderr.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The stderr phrases that classify an URI scheme the ffmpeg build does not support —
    /// e.g. an ffmpeg without libssh rejecting <c>sftp://</c> entirely.
    /// </summary>
    internal static readonly string[] UnsupportedSchemeErrorMarkers =
    {
        "Protocol not found",
        "Unknown protocol",
        "not on whitelist"
    };

    /// <summary>
    /// Determines whether the given stderr output shows the input URI scheme is not
    /// supported by this ffmpeg build.
    /// </summary>
    /// <param name="stderr">The captured stderr output (may be <see langword="null"/>).</param>
    /// <returns><see langword="true"/> when an unsupported-scheme marker was found.</returns>
    internal static bool ContainsUnsupportedSchemeError(string? stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return false;

        foreach (var marker in UnsupportedSchemeErrorMarkers)
        {
            if (stderr.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
