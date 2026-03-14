/// <summary>
/// Represents application setup metadata.
/// </summary>
public class Setup
{
    /// <summary>
    /// Gets or sets the setup identifier.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the current data version.
    /// </summary>
    public int DataVersion { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether genres have changed.
    /// </summary>
    public bool GenresChanged { get; set; }

    /// <summary>
    /// Gets or sets the interval (in minutes) at which the scan process should run.
    /// Default: 60 minutes.
    /// </summary>
    public int ScanProcessIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the interval (in days) at which a media collection should be re-scanned.
    /// Default: 7 days.
    /// </summary>
    public int MediaCollectionScanIntervalDays { get; set; } = 7;
}