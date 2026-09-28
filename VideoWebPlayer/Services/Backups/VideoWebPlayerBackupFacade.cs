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
    /// <param name="backupService">Service that stores, lists, restores and deletes backup files.</param>
    /// <param name="dataSource">Source of the data written into a backup.</param>
    /// <param name="factory">Factory that provides the data handling for restores.</param>
    /// <param name="optionsProvider">Provider of the backup options, such as the storage path.</param>
    /// <param name="environment">Host environment used to resolve relative storage paths.</param>
    /// <param name="settingsService">Service that persists the backup settings.</param>
    /// <param name="historyService">Service that records the backup operation history.</param>
    /// <param name="logger">The logger.</param>
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
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The descriptors of the available backups.</returns>
    public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken = default)
        => _backupService.ListBackupsAsync(cancellationToken);

    /// <summary>
    /// Creates a manual backup and records history.
    /// </summary>
    /// <param name="userId">The id of the requesting user recorded in the history; may be <c>null</c>.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The operation result, including the descriptor of the created backup when it succeeded.</returns>
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
    /// Imports an uploaded backup temp file and records history.
    /// </summary>
    /// <param name="tempFilePath">The path of the uploaded temporary file.</param>
    /// <param name="fileName">The client-provided backup file name; it must not contain path segments.</param>
    /// <param name="userId">The id of the requesting user recorded in the history; may be <c>null</c>.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The operation result; a failure when the file name is invalid, the file is empty or not a valid backup, or the import fails.</returns>
    public async Task<BackupOperationResult> ImportUploadFileAsync(string tempFilePath, string fileName, string? userId, CancellationToken cancellationToken = default)
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

            var fileInfo = new FileInfo(tempFilePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
                return BackupOperationResult.Failure("Die hochgeladene Datei ist leer.", fileName);

            try
            {
                await using var temp = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                using var archive = new ZipArchive(temp, ZipArchiveMode.Read, leaveOpen: true);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry is null)
                    return BackupOperationResult.Failure("Kein gültiges Backup: manifest.json fehlt.", fileName);
            }
            catch (InvalidDataException ex)
            {
                return BackupOperationResult.Failure("Die hochgeladene Datei ist kein gültiges Backup.", ex.Message);
            }

            var targetPath = Path.Combine(fullStoragePath, safeFileName);
            await MoveTempFileAsync(tempFilePath, targetPath, cancellationToken);

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

    private async Task MoveTempFileAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsSameFileSystem(sourcePath, targetPath))
        {
            File.Move(sourcePath, targetPath, overwrite: true);
            return;
        }

        // Copy into a ".part" file next to the target so that the final File.Move
        // is a guaranteed rename and a partial copy never appears as a ".bak" file.
        var stagingPath = targetPath + ".part";
        try
        {
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var target = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Move(stagingPath, targetPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(stagingPath))
                    File.Delete(stagingPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove partially copied backup upload {StagingPath}.", stagingPath);
            }

            throw;
        }

        File.Delete(sourcePath);
    }

    // Path.GetPathRoot alone is not enough on Linux: /tmp (tmpfs) and the storage
    // path can share the "/" root while living on different filesystems, where
    // File.Move would silently fall back to a synchronous, non-cancellable copy.
    private static bool IsSameFileSystem(string sourcePath, string targetPath)
    {
        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
        var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetPath));
        if (!string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var sourceMount = GetMountPoint(sourcePath);
            var targetMount = GetMountPoint(targetPath);
            return sourceMount is not null
                && targetMount is not null
                && string.Equals(sourceMount, targetMount, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string? GetMountPoint(string path)
    {
        var fullPath = Path.GetFullPath(path);
        string? best = null;
        foreach (var drive in DriveInfo.GetDrives())
        {
            var root = drive.RootDirectory.FullName;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            var endsAtBoundary = fullPath.Length == root.Length
                || root.EndsWith(Path.DirectorySeparatorChar)
                || fullPath[root.Length] is '/' or '\\';
            if (endsAtBoundary && (best is null || root.Length > best.Length))
                best = root;
        }

        return best;
    }

    /// <summary>
    /// Restores a backup and records history.
    /// </summary>
    /// <param name="fileName">The name of the backup file to restore.</param>
    /// <param name="userId">The id of the requesting user recorded in the history; may be <c>null</c>.</param>
    /// <param name="confirmRestore">Whether the restore was confirmed; when <c>false</c> the restore is refused.</param>
    /// <param name="progress">Optional receiver of the restore progress.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The operation result of the restore.</returns>
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
    /// <param name="fileName">The name of the backup file to delete.</param>
    /// <param name="userId">The id of the requesting user recorded in the history; may be <c>null</c>.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The operation result of the deletion.</returns>
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
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The persisted backup settings.</returns>
    public Task<BackupSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => _settingsService.GetOrCreateAsync(cancellationToken);

    /// <summary>
    /// Updates persisted backup settings.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task UpdateSettingsAsync(BackupSettings settings, CancellationToken cancellationToken = default)
        => _settingsService.UpdateAsync(settings, cancellationToken);

    /// <summary>
    /// Gets latest operation history rows.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The latest 25 history entries.</returns>
    public Task<List<BackupOperationHistory>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => _historyService.GetLatestAsync(25, cancellationToken);
}
