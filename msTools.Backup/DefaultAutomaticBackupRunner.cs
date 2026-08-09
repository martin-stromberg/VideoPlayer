namespace msTools.Backup;

/// <summary>
/// Default automatic backup runner used by host-neutral registrations.
/// </summary>
public sealed class DefaultAutomaticBackupRunner : IAutomaticBackupRunner
{
    private readonly IBackupService _backupService;

    /// <summary>
    /// Creates a new default automatic backup runner.
    /// </summary>
    public DefaultAutomaticBackupRunner(IBackupService backupService)
    {
        _backupService = backupService;
    }

    /// <inheritdoc />
    public async Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken)
    {
        var result = await _backupService.CreateBackupAsync(new BackupCreateRequest(generation, "Scheduled"), cancellationToken);
        if (result.Succeeded)
            await _backupService.ApplyRetentionAsync(cancellationToken);

        return result;
    }
}
