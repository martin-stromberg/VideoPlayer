using System;

namespace VideoWebPlayer.Services;

/// <summary>
/// Outcome of a single media duration probe. <see cref="Duration"/> carries the probed duration
/// on success; <see cref="Transient"/> distinguishes a retryable failure (timeout, IO or process
/// errors) from a permanent one (the file ran through ffprobe but has no determinable duration).
/// </summary>
/// <param name="Duration">The probed duration, or <c>null</c> when the probe failed.</param>
/// <param name="Transient"><c>true</c> when a retry may succeed; <c>false</c> for permanent failures.</param>
public sealed record MediaDurationProbeResult(TimeSpan? Duration, bool Transient)
{
    /// <summary>
    /// Gets a value indicating whether the probe input's URI scheme is not supported by the
    /// ffprobe build at all (e.g. <c>sftp</c> without libssh). Callers should fall back to
    /// another input method for the same item instead of treating the probe itself as failed.
    /// </summary>
    public bool UriUnsupported { get; init; }

    /// <summary>
    /// A shared instance for retryable failures (timeout, IO or process errors).
    /// </summary>
    public static readonly MediaDurationProbeResult TransientFailure = new(null, Transient: true);

    /// <summary>
    /// A shared instance for permanent failures (ffprobe ran but found no determinable duration).
    /// </summary>
    public static readonly MediaDurationProbeResult PermanentFailure = new(null, Transient: false);

    /// <summary>
    /// A shared instance for "the URI scheme is not supported" — transient by nature (a different
    /// ffprobe build could handle it), but flagged so the caller can pick another input method.
    /// </summary>
    public static readonly MediaDurationProbeResult UriUnsupportedFailure =
        new(null, Transient: true) { UriUnsupported = true };

    /// <summary>
    /// Creates a successful result carrying the probed <paramref name="duration"/>.
    /// </summary>
    /// <param name="duration">The probed playback duration.</param>
    /// <returns>The success result.</returns>
    public static MediaDurationProbeResult Success(TimeSpan duration) => new(duration, Transient: false);
}
