namespace VideoWebPlayer.Data;

/// <summary>
/// Stores administrator-managed settings for the automatic program update subsystem.
/// </summary>
public sealed class UpdateSettings
{
    /// <summary>
    /// Gets or sets the singleton settings row identifier.
    /// </summary>
    public int Id { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether automatic update checks are enabled.
    /// </summary>
    public bool AutomaticChecksEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval in minutes between automatic update checks.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 360;

    /// <summary>
    /// Gets or sets a value indicating whether prerelease versions are accepted.
    /// </summary>
    public bool AllowPrereleaseUpdates { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether updates are installed automatically after discovery.
    /// </summary>
    public bool AutomaticInstallationEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether update packages may be downloaded automatically.
    /// </summary>
    public bool AutomaticDownloadEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the operating system service name used by the updater.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a backup is created before installation.
    /// </summary>
    public bool CreateBackupBeforeInstallation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether installation is canceled when the backup fails.
    /// </summary>
    public bool CancelInstallationOnBackupFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the configured target directory for update backup compatibility.
    /// </summary>
    public string UpdateBackupPath { get; set; } = "Backups";

    /// <summary>
    /// Gets or sets the number of program update backups retained by the backup infrastructure.
    /// </summary>
    public int RetainedUpdateBackupCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
