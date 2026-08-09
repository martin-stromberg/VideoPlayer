using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Runs scheduled backups and writes them to the admin history.
/// </summary>
public sealed class VideoWebPlayerAutomaticBackupRunner : IAutomaticBackupRunner
{
    private readonly IBackupService _backupService;
    private readonly BackupOperationHistoryService _historyService;

    /// <summary>
    /// Creates a new automatic backup runner.
    /// </summary>
    public VideoWebPlayerAutomaticBackupRunner(
        IBackupService backupService,
        BackupOperationHistoryService historyService)
    {
        _backupService = backupService;
        _historyService = historyService;
    }

    /// <inheritdoc />
    public async Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var result = await _backupService.CreateBackupAsync(new BackupCreateRequest(generation, "VideoWebPlayer"), cancellationToken);
        await _historyService.AddAsync("AutomaticBackup", result, null, started, cancellationToken);
        if (result.Succeeded)
            await _backupService.ApplyRetentionAsync(cancellationToken);

        return result;
    }
}
