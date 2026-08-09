using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Coordinates backup operations, settings and history for the admin UI.
/// </summary>
public sealed class VideoWebPlayerBackupFacade
{
    private readonly IBackupService _backupService;
    private readonly BackupSettingsService _settingsService;
    private readonly BackupOperationHistoryService _historyService;

    /// <summary>
    /// Creates a new backup facade.
    /// </summary>
    public VideoWebPlayerBackupFacade(
        IBackupService backupService,
        BackupSettingsService settingsService,
        BackupOperationHistoryService historyService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _historyService = historyService;
    }

    /// <summary>
    /// Lists available backups.
    /// </summary>
    public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken = default)
        => _backupService.ListBackupsAsync(cancellationToken);

    /// <summary>
    /// Creates a manual backup and records history.
    /// </summary>
    public async Task<BackupOperationResult> CreateManualBackupAsync(string? userId, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = await _backupService.CreateBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "VideoWebPlayer"), cancellationToken);
        await _historyService.AddAsync("Backup", result, userId, started, cancellationToken);
        if (result.Succeeded)
            await _backupService.ApplyRetentionAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Imports an uploaded backup and records history.
    /// </summary>
    public async Task<BackupOperationResult> ImportUploadAsync(Stream stream, string fileName, string? userId, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = await _backupService.ImportUploadedBackupAsync(stream, fileName, cancellationToken);
        await _historyService.AddAsync("Upload", result, userId, started, cancellationToken);
        return result;
    }

    /// <summary>
    /// Restores a backup and records history.
    /// </summary>
    public async Task<BackupOperationResult> RestoreAsync(string fileName, string? userId, bool confirmRestore, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = await _backupService.RestoreBackupAsync(new BackupRestoreRequest(fileName, userId, confirmRestore), cancellationToken);
        await _historyService.AddAsync("Restore", result, userId, started, cancellationToken);
        return result;
    }

    /// <summary>
    /// Gets persisted backup settings.
    /// </summary>
    public Task<BackupSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => _settingsService.GetOrCreateAsync(cancellationToken);

    /// <summary>
    /// Updates persisted backup settings.
    /// </summary>
    public Task UpdateSettingsAsync(BackupSettings settings, CancellationToken cancellationToken = default)
        => _settingsService.UpdateAsync(settings, cancellationToken);

    /// <summary>
    /// Gets latest operation history rows.
    /// </summary>
    public Task<List<BackupOperationHistory>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => _historyService.GetLatestAsync(25, cancellationToken);
}
