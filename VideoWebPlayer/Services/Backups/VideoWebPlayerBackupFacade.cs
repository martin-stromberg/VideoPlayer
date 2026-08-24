using System.IO.Compression;
using Microsoft.Extensions.Hosting;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Coordinates backup operations, settings and history for the admin UI.
/// </summary>
public sealed class VideoWebPlayerBackupFacade
{
    private readonly IBackupService _backupService;
    private readonly IBackupDataSource _dataSource;
    private readonly VideoWebPlayerBackupDataFactory _factory;
    private readonly IBackupOptionsProvider _optionsProvider;
    private readonly IHostEnvironment _environment;
    private readonly BackupSettingsService _settingsService;
    private readonly BackupOperationHistoryService _historyService;
    private readonly ILogger<VideoWebPlayerBackupFacade> _logger;

    /// <summary>
    /// Creates a new backup facade.
    /// </summary>
    public VideoWebPlayerBackupFacade(
        IBackupService backupService,
        IBackupDataSource dataSource,
        VideoWebPlayerBackupDataFactory factory,
        IBackupOptionsProvider optionsProvider,
        IHostEnvironment environment,
        BackupSettingsService settingsService,
        BackupOperationHistoryService historyService,
        ILogger<VideoWebPlayerBackupFacade> logger)
    {
        _backupService = backupService;
        _dataSource = dataSource;
        _factory = factory;
        _optionsProvider = optionsProvider;
        _environment = environment;
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

        var items = await _dataSource.GetBackupDataAsync(cancellationToken);
        var result = await _backupService.StoreAsync(
            $"manual-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
            BackupGeneration.Manual,
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

        await _historyService.AddAsync("Backup", operationResult, userId, started, cancellationToken);
        return operationResult;
    }

    /// <summary>
    /// Imports an uploaded backup and records history.
    /// </summary>
    public async Task<BackupOperationResult> ImportUploadAsync(Stream stream, string fileName, string? userId, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        try
        {
            var safeFileName = Path.GetFileName(fileName);
            if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal))
                return BackupOperationResult.Failure("Ungültiger Dateiname.", fileName);

            if (!safeFileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                safeFileName += ".bak";

            var options = await _optionsProvider.GetOptionsAsync(cancellationToken);
            var storagePath = options.StoragePath;
            if (string.IsNullOrWhiteSpace(storagePath))
                storagePath = Path.Combine("Data", "Backups");

            var fullStoragePath = Path.IsPathRooted(storagePath)
                ? Path.GetFullPath(storagePath)
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, storagePath));

            Directory.CreateDirectory(fullStoragePath);

            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            if (ms.Length == 0)
                return BackupOperationResult.Failure("Die hochgeladene Datei ist leer.", fileName);

            try
            {
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry is null)
                    return BackupOperationResult.Failure("Kein gültiges Backup: manifest.json fehlt.", fileName);
            }
            catch (InvalidDataException ex)
            {
                return BackupOperationResult.Failure("Die hochgeladene Datei ist kein gültiges Backup.", ex.Message);
            }

            var targetPath = Path.Combine(fullStoragePath, safeFileName);
            await using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ms.Position = 0;
                await ms.CopyToAsync(fileStream, cancellationToken);
            }

            var descriptors = await _backupService.ListBackupsAsync(cancellationToken);
            var descriptor = descriptors.FirstOrDefault(d =>
                string.Equals(d.Path, targetPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.FileName, safeFileName, StringComparison.OrdinalIgnoreCase));

            var operationResult = BackupOperationResult.Success("Backup wurde importiert.", descriptor);
            await _historyService.AddAsync("Upload", operationResult, userId, started, cancellationToken);
            return operationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import of uploaded backup {FileName} failed.", fileName);
            return BackupOperationResult.Failure($"Import fehlgeschlagen: {ex.Message}", ex.Message);
        }
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

        if (!confirmRestore)
            return BackupOperationResult.Failure("Wiederherstellung nicht bestätigt.");

        try
        {
            _factory.UserId = userId;
            _factory.Progress = progress;
            var data = await _backupService.RestoreAsync(fileName, _factory, cancellationToken);
            var result = BackupOperationResult.Success("Backup wurde wiederhergestellt.");
            await _historyService.AddAsync("Restore", result, userId, started, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore of {FileName} failed.", fileName);
            var result = BackupOperationResult.Failure($"Wiederherstellung fehlgeschlagen: {ex.Message}", ex.Message);
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
