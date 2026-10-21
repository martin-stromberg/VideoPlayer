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
    /// Gets or sets the timeout of a single ffprobe invocation in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets the normalized polling interval (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveIntervalSeconds => IntervalSeconds > 0 ? IntervalSeconds : 60;

    /// <summary>
    /// Gets the normalized batch size (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveBatchSize => BatchSize > 0 ? BatchSize : 10;

    /// <summary>
    /// Gets the normalized probe timeout (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveTimeoutSeconds => TimeoutSeconds > 0 ? TimeoutSeconds : 120;
}
