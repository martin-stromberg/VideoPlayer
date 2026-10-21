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
}
