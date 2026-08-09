namespace msTools.Backup;

/// <summary>
/// Configures backup storage, uploads, scheduling and retention.
/// </summary>
public sealed class BackupOptions
{
    /// <summary>
    /// Gets or sets the directory where backup ZIP files are stored.
    /// </summary>
    public string StoragePath { get; set; } = Path.Combine("Data", "Backups");

    /// <summary>
    /// Gets or sets the maximum accepted upload size in bytes.
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 512L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets a value indicating whether automatic backups are enabled.
    /// </summary>
    public bool AutomaticBackupsEnabled { get; set; }

    /// <summary>
    /// Gets the backup schedule options.
    /// </summary>
    public BackupScheduleOptions Schedule { get; set; } = new();

    /// <summary>
    /// Gets the backup retention options.
    /// </summary>
    public BackupRetentionOptions Retention { get; set; } = new();
}

/// <summary>
/// Configures automatic backup scheduling.
/// </summary>
public sealed class BackupScheduleOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the scheduler should run.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the interval at which due backups are checked.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the son backup frequency.
    /// </summary>
    public TimeSpan SonFrequency { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets the father backup frequency.
    /// </summary>
    public TimeSpan FatherFrequency { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the grandfather backup frequency.
    /// </summary>
    public TimeSpan GrandfatherFrequency { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Configures how many backups are retained per managed generation.
/// </summary>
public sealed class BackupRetentionOptions
{
    /// <summary>
    /// Gets or sets the retained son backup count.
    /// </summary>
    public int SonCount { get; set; } = 7;

    /// <summary>
    /// Gets or sets the retained father backup count.
    /// </summary>
    public int FatherCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets the retained grandfather backup count.
    /// </summary>
    public int GrandfatherCount { get; set; } = 12;

    /// <summary>
    /// Gets or sets the retained program update backup count.
    /// </summary>
    public int ProgramUpdateCount { get; set; } = 5;
}
