namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Configuration of the data backup created before an automatic program update is installed.
/// Bound from the <c>AutoUpdate:Backup</c> configuration section.
/// </summary>
public sealed class UpdateBackupOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "AutoUpdate:Backup";

    /// <summary>
    /// Gets or sets a value indicating whether a backup is created before an update is installed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the directory the backups are stored in. Relative paths are resolved against the
    /// application's content root.
    /// </summary>
    public string Path { get; set; } = "Backups";

    /// <summary>
    /// Gets or sets the number of program update backups retained by the backup infrastructure.
    /// </summary>
    public int RetainedBackupCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether the installation is canceled when the backup fails or no
    /// <see cref="IUpdateBackupService"/> is registered.
    /// </summary>
    public bool CancelInstallationOnFailure { get; set; } = true;
}
