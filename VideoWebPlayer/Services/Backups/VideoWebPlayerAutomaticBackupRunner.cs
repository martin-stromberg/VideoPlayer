using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Runs scheduled backups and writes them to the admin history.
/// </summary>
public sealed class VideoWebPlayerAutomaticBackupRunner : IAutomaticBackupRunner
{
    private readonly IBackupService _backupService;
    private readonly IBackupDataProvider _provider;
    private readonly BackupOperationHistoryService _historyService;

    /// <summary>
    /// Creates a new automatic backup runner.
    /// </summary>
    public VideoWebPlayerAutomaticBackupRunner(
        IBackupService backupService,
        IBackupDataProvider provider,
        BackupOperationHistoryService historyService)
    {
        _backupService = backupService;
        _provider = provider;
        _historyService = historyService;
    }

    /// <inheritdoc />
    public async Task<BackupOperationResult> RunAutomaticBackupAsync(BackupGeneration generation, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var exportContext = new BackupExportContext(generation, started);
        var data = new VideoWebPlayerBackupData(exportContext, "videowebplayer/database", "VideoWebPlayer:Database", _provider);
        var backupName = $"{generation}-{started:yyyyMMdd-HHmmss}".ToLowerInvariant();
        var result = await _backupService.StoreAsync(backupName, [data], cancellationToken);
        var path = result.BackupPath ?? backupName;
        var descriptor = new BackupDescriptor(path, path, 0, started, generation, _provider.ProviderId, 1, true, []);
        var operationResult = result.Succeeded
            ? BackupOperationResult.Success(result.Message, descriptor)
            : BackupOperationResult.Failure(result.Message, [result.Message]);
        await _historyService.AddAsync("AutomaticBackup", operationResult, null, started, cancellationToken);
        if (result.Succeeded)
            await _backupService.ApplyRetentionAsync(cancellationToken);

        return operationResult;
    }
}
