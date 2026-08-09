using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace msTools.Backup;

/// <summary>
/// Default high-level backup service.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IBackupStore _store;
    private readonly IBackupDataProvider _dataProvider;
    private readonly IBackupRestoreGuard _restoreGuard;
    private readonly IBackupOptionsProvider _optionsProvider;
    private readonly IBackupRetentionService _retentionService;
    private readonly ILogger<BackupService> _logger;

    /// <summary>
    /// Creates a new backup service.
    /// </summary>
    public BackupService(
        IBackupStore store,
        IBackupDataProvider dataProvider,
        IBackupRestoreGuard restoreGuard,
        IBackupOptionsProvider optionsProvider,
        IBackupRetentionService retentionService,
        ILogger<BackupService> logger)
    {
        _store = store;
        _dataProvider = dataProvider;
        _restoreGuard = restoreGuard;
        _optionsProvider = optionsProvider;
        _retentionService = retentionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
        => _store.ListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<BackupOperationResult> CreateBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = await _store.SaveBackupAsync(request, cancellationToken);
            return BackupOperationResult.Success("Backup wurde erstellt.", descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed.");
            return BackupOperationResult.Failure("Backup konnte nicht erstellt werden.", ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<BackupValidationResult> ValidateUploadAsync(Stream source, CancellationToken cancellationToken)
        => _store.ValidateAsync(source, cancellationToken);

    /// <inheritdoc />
    public async Task<BackupOperationResult> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = await _store.ImportUploadedBackupAsync(source, originalFileName, cancellationToken);
            return BackupOperationResult.Success("Backup wurde hochgeladen.", descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup upload import failed.");
            return BackupOperationResult.Failure("Backup konnte nicht hochgeladen werden.", ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
        => _store.OpenReadAsync(fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = (await _store.ListAsync(cancellationToken))
                .FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            await _store.DeleteAsync(fileName, cancellationToken);
            return BackupOperationResult.Success("Backup wurde gelöscht.", descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup deletion failed for {FileName}.", fileName);
            return BackupOperationResult.Failure("Backup konnte nicht gelöscht werden.", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<BackupOperationResult> RestoreBackupAsync(BackupRestoreRequest request, CancellationToken cancellationToken)
    {
        if (!request.ConfirmRestore)
            return BackupOperationResult.Failure("Restore wurde nicht bestätigt.");

        try
        {
            await using (var validationStream = await _store.OpenReadAsync(request.FileName, cancellationToken))
            {
                var validation = await _store.ValidateAsync(validationStream, cancellationToken);
                if (!validation.IsValid)
                    return BackupOperationResult.Failure("Backup konnte nicht wiederhergestellt werden.", validation.Errors.ToArray());
            }

            await using var restoreLease = await _restoreGuard.EnterRestoreAsync(cancellationToken);
            await using var stream = await _store.OpenReadAsync(request.FileName, cancellationToken);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.GetEntry("index.json") ?? throw new InvalidDataException("index.json fehlt.");

            await using var dataStream = entry.Open();
            await _dataProvider.RestoreAsync(
                dataStream,
                new BackupRestoreContext(
                    request.UserId,
                    (entryName, token) => OpenPayloadEntryAsync(archive, entryName, token),
                    request.Progress),
                cancellationToken);

            var descriptor = (await _store.ListAsync(cancellationToken))
                .FirstOrDefault(x => string.Equals(x.FileName, request.FileName, StringComparison.OrdinalIgnoreCase));

            return BackupOperationResult.Success("Backup wurde wiederhergestellt.", descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup restore failed for {FileName}.", request.FileName);
            return BackupOperationResult.Failure("Backup konnte nicht wiederhergestellt werden.", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        var options = await _optionsProvider.GetOptionsAsync(cancellationToken);
        var descriptors = await _store.ListAsync(cancellationToken);
        await _retentionService.ApplyAsync(descriptors, options.Retention, cancellationToken);
    }

    private static bool IsSafePayloadEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(':') || name.Contains('\\'))
            return false;

        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0
            && !name.EndsWith("/", StringComparison.Ordinal)
            && parts.All(part => part != "." && part != "..")
            && (string.Equals(name, "index.json", StringComparison.Ordinal)
                || name.StartsWith("entities/", StringComparison.Ordinal)
                || name.StartsWith("files/", StringComparison.Ordinal));
    }

    private static Task<Stream> OpenPayloadEntryAsync(ZipArchive archive, string entryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = entryName.Replace('\\', '/');
        if (!IsSafePayloadEntryName(normalized))
            throw new InvalidDataException($"Ungültiger Payload-Eintrag: {entryName}");

        var payloadEntry = archive.GetEntry(normalized)
            ?? throw new FileNotFoundException("Payload-Eintrag wurde im Backup nicht gefunden.", normalized);

        return Task.FromResult(payloadEntry.Open());
    }
}
