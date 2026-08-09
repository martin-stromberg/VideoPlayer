using msTools.Backup;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Creates update backups through the same backup infrastructure used by manual web backups.
/// </summary>
public sealed class VideoWebPlayerUpdateBackupService : IUpdateBackupService
{
    private readonly IBackupService _backupService;
    private readonly BackupOperationHistoryService _historyService;
    private readonly ILogger<VideoWebPlayerUpdateBackupService> _logger;

    /// <summary>
    /// Creates a new update backup adapter.
    /// </summary>
    public VideoWebPlayerUpdateBackupService(
        IBackupService backupService,
        BackupOperationHistoryService historyService,
        ILogger<VideoWebPlayerUpdateBackupService> logger)
    {
        _backupService = backupService;
        _historyService = historyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateBackupResult> CreateBackupAsync(UpdateBackupRequest request, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        BackupOperationResult result;
        try
        {
            result = await _backupService.CreateBackupAsync(
                new BackupCreateRequest(BackupGeneration.ProgramUpdate, "VideoWebPlayer"),
                cancellationToken);

            await _historyService.AddAsync("ProgramUpdateBackup", result, userId: null, started, cancellationToken);
            if (!result.Succeeded)
                return UpdateBackupResult.Failure(ToMessage(result));

            if (result.Descriptor is null)
                return UpdateBackupResult.Failure("Backup wurde erstellt, aber ohne Dateiinformation zurueckgegeben.");

            await _backupService.ApplyRetentionAsync(cancellationToken);
            return UpdateBackupResult.Success(result.Descriptor.Path, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup before program update failed.");
            result = BackupOperationResult.Failure("Backup vor Programmupdate fehlgeschlagen.", ex.Message);
            await _historyService.AddAsync("ProgramUpdateBackup", result, userId: null, started, cancellationToken);
            return UpdateBackupResult.Failure(ex.Message);
        }
    }

    private static string ToMessage(BackupOperationResult result)
        => string.Join(" ", result.Errors.DefaultIfEmpty(result.Message));
}
