namespace VideoWebPlayer.Services;

/// <summary>
/// Issues short-lived, single-item stream tokens for server-internal calls and resolves
/// them back to the media item they authorize. Used to let ffprobe reach the internal
/// media-stream endpoint over loopback HTTP — without any user account or long-lived
/// credential appearing in the probe's process arguments.
/// </summary>
public interface IInternalMediaStreamTokenService
{
    /// <summary>
    /// Issues a stream token for the given media item and returns the absolute loopback
    /// probe URI for it, or <see langword="null"/> when the HTTP probe path is disabled or
    /// no usable HTTP address of this server instance can be determined. The URI can be
    /// handed to ffprobe directly; the embedded token authorizes streaming of exactly that
    /// one media item for a short lifetime.
    /// </summary>
    /// <param name="mediaItemId">The media item the token authorizes.</param>
    /// <returns>The probe URI, or <see langword="null"/> when HTTP probing is unavailable.</returns>
    string? TryCreateProbeUri(long mediaItemId);

    /// <summary>
    /// Issues a stream token for the given media item with a caller-chosen lifetime and
    /// returns the absolute loopback stream URI for it — for long-running consumers such as
    /// the live stream segmenter whose ffmpeg process reads the endpoint for the whole
    /// remaining air time of a broadcast slot. Unlike <see cref="TryCreateProbeUri"/> this
    /// is not gated by the probe option; the caller decides whether HTTP input is used at
    /// all. Returns <see langword="null"/> when no usable HTTP address of this server
    /// instance can be determined — the caller then falls back to another input method.
    /// </summary>
    /// <param name="mediaItemId">The media item the token authorizes.</param>
    /// <param name="lifetime">How long the token must stay valid (clamped to a minimum).</param>
    /// <returns>The stream URI, or <see langword="null"/> when no HTTP address is available.</returns>
    string? TryCreateStreamUri(long mediaItemId, TimeSpan lifetime);

    /// <summary>
    /// Resolves a stream token back to the media item it was issued for.
    /// </summary>
    /// <param name="token">The stream token as issued by <see cref="TryCreateProbeUri"/>.</param>
    /// <returns>The media item id, or <see langword="null"/> when the token is unknown or expired.</returns>
    long? ResolveMediaItemId(string token);
}
