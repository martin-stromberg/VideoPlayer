namespace msTools.Backup;

/// <summary>
/// Provides host data for backup export and restore.
/// </summary>
public interface IBackupDataProvider
{
    /// <summary>
    /// Gets the stable provider identifier stored in backup manifests.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Exports host data to the target stream.
    /// </summary>
    Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Validates host payload data.
    /// </summary>
    Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken);

    /// <summary>
    /// Restores host data from the source stream.
    /// </summary>
    Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Contains context for export operations.
/// </summary>
public sealed record BackupExportContext(BackupGeneration Generation, DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// Gets file payload entries that should be written next to data.json in the ZIP.
    /// </summary>
    public IList<BackupFileAttachment> FileAttachments { get; } = new List<BackupFileAttachment>();
}

/// <summary>
/// Coordinates host-side restore exclusivity.
/// </summary>
public interface IBackupRestoreGuard
{
    /// <summary>
    /// Enters restore mode and returns a handle that releases it.
    /// </summary>
    Task<IAsyncDisposable> EnterRestoreAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Provides current backup options, including persisted host settings.
/// </summary>
public interface IBackupOptionsProvider
{
    /// <summary>
    /// Gets current backup options.
    /// </summary>
    Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Stores and validates backup ZIP files.
/// </summary>
public interface IBackupStore
{
    /// <summary>
    /// Lists known backup files.
    /// </summary>
    Task<IReadOnlyList<BackupDescriptor>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a newly created backup.
    /// </summary>
    Task<BackupDescriptor> SaveBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a backup stream.
    /// </summary>
    Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken);

    /// <summary>
    /// Imports a validated upload into the backup store.
    /// </summary>
    Task<BackupDescriptor> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored backup for reading.
    /// </summary>
    Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a stored backup.
    /// </summary>
    Task DeleteAsync(string fileName, CancellationToken cancellationToken);
}

/// <summary>
/// Provides high-level backup operations.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Lists backups.
    /// </summary>
    Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a backup.
    /// </summary>
    Task<BackupOperationResult> CreateBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates an uploaded backup stream.
    /// </summary>
    Task<BackupValidationResult> ValidateUploadAsync(Stream source, CancellationToken cancellationToken);

    /// <summary>
    /// Imports an uploaded backup stream.
    /// </summary>
    Task<BackupOperationResult> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a backup for downloading.
    /// </summary>
    Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// Restores a backup.
    /// </summary>
    Task<BackupOperationResult> RestoreBackupAsync(BackupRestoreRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Applies automatic backup retention.
    /// </summary>
    Task ApplyRetentionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Executes automatic backups for the scheduler.
/// </summary>
public interface IAutomaticBackupRunner
{
    /// <summary>
    /// Creates an automatic backup and performs host-specific follow-up work.
    /// </summary>
    Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken);
}

/// <summary>
/// Applies backup retention rules.
/// </summary>
public interface IBackupRetentionService
{
    /// <summary>
    /// Deletes expired automatic generation backups.
    /// </summary>
    Task ApplyAsync(IReadOnlyList<BackupDescriptor> descriptors, BackupRetentionOptions options, CancellationToken cancellationToken);
}
