namespace VideoWebPlayer.Configuration;

/// <summary>
/// Options for the ffprobe-based media duration probe (section <c>MediaProbe</c>).
/// </summary>
public sealed class MediaProbeOptions
{
    /// <summary>
    /// Gets or sets the absolute path to the ffprobe executable, or <c>null</c> to resolve it
    /// automatically (convention directories next to the application, then <c>PATH</c>).
    /// </summary>
    public string? FfprobePath { get; set; }

    /// <summary>
    /// Gets or sets the absolute path to the ffmpeg executable, or <c>null</c> to resolve it
    /// automatically (convention directories next to the application, then <c>PATH</c>).
    /// </summary>
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// Gets or sets the polling interval of the probe worker in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of media items probed per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the timeout of a single ffprobe invocation on a local file path in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the timeout of a single ffprobe invocation that reads the media from a piped
    /// stream (<c>pipe:0</c>) in seconds. Remote streams are considerably slower than local file
    /// access, so the default is intentionally higher than <see cref="TimeoutSeconds"/>.
    /// </summary>
    public int StreamTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Gets or sets the minimum delay in seconds before a media item whose last probe attempt
    /// failed transiently is probed again.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of probe attempts per media item; once reached, the item
    /// is treated as permanently failed and no longer probed.
    /// </summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of ffprobe processes run in parallel within one batch.
    /// </summary>
    public int ParallelProbes { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether remote media items are probed through the application's own
    /// loopback HTTP stream endpoint before falling back to <c>sftp://</c>/<c>pipe:0</c>.
    /// Over HTTP with range support ffprobe seeks like it does over a seekable protocol and
    /// only reads the container headers — through the same media-source read path as
    /// playback (e.g. SSH.NET for SFTP), independent of the source type.
    /// </summary>
    public bool HttpProbeEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the explicit loopback base URL used for HTTP self-probes
    /// (e.g. <c>http://127.0.0.1:5039</c>), or <c>null</c> to detect it automatically from
    /// the server's bound addresses. Set this when the automatic detection cannot produce a
    /// reachable address (e.g. behind a reverse proxy or with unusual bindings).
    /// </summary>
    public string? HttpProbeBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the lifetime in seconds of an internal stream token issued for an HTTP
    /// probe. The token authorizes streaming of exactly one media item and only needs to
    /// outlive a single ffprobe invocation.
    /// </summary>
    public int HttpProbeTokenLifetimeSeconds { get; set; } = 600;

    /// <summary>
    /// Gets the normalized polling interval (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveIntervalSeconds => IntervalSeconds > 0 ? IntervalSeconds : 60;

    /// <summary>
    /// Gets the normalized batch size (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveBatchSize => BatchSize > 0 ? BatchSize : 10;

    /// <summary>
    /// Gets the normalized probe timeout for local file paths (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveTimeoutSeconds => TimeoutSeconds > 0 ? TimeoutSeconds : 120;

    /// <summary>
    /// Gets the normalized probe timeout for piped streams (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveStreamTimeoutSeconds => StreamTimeoutSeconds > 0 ? StreamTimeoutSeconds : 600;

    /// <summary>
    /// Gets the normalized retry delay (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveRetryDelaySeconds => RetryDelaySeconds > 0 ? RetryDelaySeconds : 300;

    /// <summary>
    /// Gets the normalized maximum attempt count (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveMaxAttempts => MaxAttempts > 0 ? MaxAttempts : 10;

    /// <summary>
    /// Gets the normalized probe parallelism (values &lt;= 0 fall back to sequential probing).
    /// </summary>
    public int EffectiveParallelProbes => ParallelProbes > 0 ? ParallelProbes : 1;

    /// <summary>
    /// Gets the normalized internal stream token lifetime (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveHttpProbeTokenLifetimeSeconds => HttpProbeTokenLifetimeSeconds > 0 ? HttpProbeTokenLifetimeSeconds : 600;
}
