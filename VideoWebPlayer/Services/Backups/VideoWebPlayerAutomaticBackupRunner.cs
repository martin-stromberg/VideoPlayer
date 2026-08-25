using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Runs scheduled backups and writes them to the admin history.
/// </summary>
public sealed class VideoWebPlayerAutomaticBackupRunner : IAutomaticBackupRunner
{
    private readonly IBackupService _backupService;
    private readonly IBackupDataSource _dataSource;
    private readonly BackupOperationHistoryService _historyService;

    /// <summary>
    /// Creates a new automatic backup runner.
    /// </summary>
    public VideoWebPlayerAutomaticBackupRunner(
        IBackupService backupService,
        IBackupDataSource dataSource,
        BackupOperationHistoryService historyService)
    {
        _backupService = backupService;
        _dataSource = dataSource;
        _historyService = historyService;
    }

    /// <inheritdoc />
    public async Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;

        var items = await _dataSource.GetBackupDataAsync(cancellationToken);
        var result = await _backupService.StoreAsync(
            $"{generation.ToString().ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
            generation,
            items,
            cancellationToken);

        BackupOperationResult operationResult;
        if (result.Succeeded)
        {
            var descriptors = await _backupService.ListBackupsAsync(cancellationToken);
            var descriptor = descriptors.FirstOrDefault(d => string.Equals(d.Path, result.BackupPath, StringComparison.OrdinalIgnoreCase));
            operationResult = BackupOperationResult.Success(result.Message, descriptor);
            await _backupService.ApplyRetentionAsync(cancellationToken);
        }
        else
        {
            operationResult = BackupOperationResult.Failure(result.Message);
        }

        await _historyService.AddAsync("AutomaticBackup", operationResult, null, started, cancellationToken);
        return operationResult;
    }
}
