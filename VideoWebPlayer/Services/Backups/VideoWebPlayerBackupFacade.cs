using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Coordinates backup operations, settings and history for the admin UI.
/// </summary>
public sealed class VideoWebPlayerBackupFacade
{
    private readonly IBackupService _backupService;
    private readonly VideoWebPlayerBackupDataProvider _provider;
    private readonly VideoWebPlayerBackupDataFactory _factory;
    private readonly BackupSettingsService _settingsService;
    private readonly BackupOperationHistoryService _historyService;
    private readonly ILogger<VideoWebPlayerBackupFacade> _logger;

    /// <summary>
    /// Creates a new backup facade.
    /// </summary>
    public VideoWebPlayerBackupFacade(
        IBackupService backupService,
        VideoWebPlayerBackupDataProvider provider,
        VideoWebPlayerBackupDataFactory factory,
        BackupSettingsService settingsService,
        BackupOperationHistoryService historyService,
        ILogger<VideoWebPlayerBackupFacade> logger)
    {
        _backupService = backupService;
        _provider = provider;
        _factory = factory;
        _settingsService = settingsService;
        _historyService = historyService;
        _logger = logger;
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
        _logger.LogInformation("Starting manual backup for user {UserId}.", userId);
        var exportContext = new BackupExportContext(BackupGeneration.Manual, started);
        var data = new VideoWebPlayerBackupData(exportContext, "videowebplayer/database", "VideoWebPlayer:Database", _provider);
        var backupName = $"manual-{started:yyyyMMdd-HHmmss}";
        var result = await _backupService.StoreAsync(backupName, [data], cancellationToken);
        var path = result.BackupPath ?? backupName;
        var descriptor = new BackupDescriptor(path, path, 0, started, BackupGeneration.Manual, _provider.ProviderId, 1, true, []);
        var operationResult = result.Succeeded
            ? BackupOperationResult.Success(result.Message, descriptor)
            : BackupOperationResult.Failure(result.Message, [result.Message]);
        await _historyService.AddAsync("Backup", operationResult, userId, started, cancellationToken);
        if (result.Succeeded)
            await _backupService.ApplyRetentionAsync(cancellationToken);
        return operationResult;
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
    public async Task<BackupOperationResult> RestoreAsync(
        string fileName,
        string? userId,
        bool confirmRestore,
        IProgress<BackupRestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        _factory.UserId = userId;
        _factory.Progress = progress;

        try
        {
            await _backupService.RestoreAsync(fileName, _factory, cancellationToken);
            var descriptor = new BackupDescriptor(fileName, fileName, 0, started, BackupGeneration.Manual, _provider.ProviderId, 1, true, []);
            var result = BackupOperationResult.Success("Wiederherstellung abgeschlossen.", descriptor);
            await _historyService.AddAsync("Restore", result, userId, started, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore of {FileName} failed.", fileName);
            var result = BackupOperationResult.Failure($"Wiederherstellung fehlgeschlagen: {ex.Message}", [ex.Message]);
            await _historyService.AddAsync("Restore", result, userId, started, cancellationToken);
            return result;
        }
    }

    /// <summary>
    /// Deletes a stored backup and records history.
    /// </summary>
    public async Task<BackupOperationResult> DeleteAsync(string fileName, string? userId, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var result = await _backupService.DeleteBackupAsync(fileName, cancellationToken);
        await _historyService.AddAsync("Löschen", result, userId, started, cancellationToken);
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
