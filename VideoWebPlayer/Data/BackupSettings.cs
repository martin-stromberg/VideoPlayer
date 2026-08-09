namespace VideoWebPlayer.Data;

/// <summary>
/// Stores administrator-managed backup settings.
/// </summary>
public sealed class BackupSettings
{
    /// <summary>
    /// Gets or sets the settings row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the backup storage path.
    /// </summary>
    public string StoragePath { get; set; } = Path.Combine("Data", "Backups");

    /// <summary>
    /// Gets or sets a value indicating whether automatic backups are enabled.
    /// </summary>
    public bool AutomaticBackupsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the retained son backup count.
    /// </summary>
    public int SonRetentionCount { get; set; } = 7;

    /// <summary>
    /// Gets or sets the retained father backup count.
    /// </summary>
    public int FatherRetentionCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets the retained grandfather backup count.
    /// </summary>
    public int GrandfatherRetentionCount { get; set; } = 12;

    /// <summary>
    /// Gets or sets the maximum upload size in bytes.
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 512L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
