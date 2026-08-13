namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Creates a full data export used as a backup before a program update is installed. Implementations are
/// resolved as an optional service, so the application runs without a backup provider until one is registered
/// (planned: <c>msTools.Backup</c>).
/// </summary>
public interface IUpdateBackupService
{
    /// <summary>
    /// Creates a full data export for the requested update event.
    /// </summary>
    /// <param name="request">Describes where the backup is stored and which update triggered it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the backup, including the created file.</returns>
    Task<UpdateBackupResult> CreateBackupAsync(UpdateBackupRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a requested backup.
/// </summary>
/// <param name="TargetDirectory">The absolute directory for providers that write update backups themselves. Providers that use it are responsible for creating it. Providers that delegate to an existing backup infrastructure may ignore it.</param>
/// <param name="Reason">A short, human readable description of what triggered the backup.</param>
public sealed record UpdateBackupRequest(string TargetDirectory, string Reason);

/// <summary>
/// Describes the outcome of a backup.
/// </summary>
/// <param name="Succeeded">Whether the backup was created successfully.</param>
/// <param name="BackupFilePath">The created backup file, or <see langword="null"/> if none was created.</param>
/// <param name="Message">An optional message describing the outcome.</param>
public sealed record UpdateBackupResult(bool Succeeded, string? BackupFilePath, string? Message)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="backupFilePath">The created backup file.</param>
    /// <param name="message">An optional message.</param>
    /// <returns>A successful result.</returns>
    public static UpdateBackupResult Success(string backupFilePath, string? message = null)
        => new(true, backupFilePath, message);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The failure reason.</param>
    /// <returns>A failed result.</returns>
    public static UpdateBackupResult Failure(string message) => new(false, null, message);
}
