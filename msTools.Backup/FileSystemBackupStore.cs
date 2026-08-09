using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace msTools.Backup;

/// <summary>
/// Stores backups as ZIP files in the local file system.
/// </summary>
public sealed class FileSystemBackupStore : IBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IBackupOptionsProvider _optionsProvider;
    private readonly IBackupDataProvider _dataProvider;
    private readonly IHostEnvironment? _environment;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FileSystemBackupStore> _logger;

    /// <summary>
    /// Creates a new file-system store.
    /// </summary>
    public FileSystemBackupStore(
        IBackupOptionsProvider optionsProvider,
        IBackupDataProvider dataProvider,
        TimeProvider timeProvider,
        ILogger<FileSystemBackupStore> logger,
        IHostEnvironment? environment = null)
    {
        _optionsProvider = optionsProvider;
        _dataProvider = dataProvider;
        _timeProvider = timeProvider;
        _logger = logger;
        _environment = environment;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var directory = await GetStorageDirectoryAsync(cancellationToken);
        if (!Directory.Exists(directory))
            return Array.Empty<BackupDescriptor>();

        var result = new List<BackupDescriptor>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.zip"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await DescribeFileAsync(file, cancellationToken));
        }

        return result.OrderByDescending(x => x.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<BackupDescriptor> SaveBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken)
    {
        var directory = await GetStorageDirectoryAsync(cancellationToken);
        Directory.CreateDirectory(directory);

        var createdAt = _timeProvider.GetUtcNow();
        var fileName = CreateFileName(createdAt, request.Generation, _dataProvider.ProviderId, uploaded: false);
        var finalPath = Path.Combine(directory, fileName);
        var tempPath = Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var exportContext = new BackupExportContext(request.Generation, createdAt);
                var payloadEntries = new List<string> { "index.json" };
                var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "manifest.json",
                    "index.json"
                };

                var dataEntry = archive.CreateEntry("index.json", CompressionLevel.Optimal);
                await using (var dataStream = dataEntry.Open())
                    await _dataProvider.ExportAsync(dataStream, exportContext, cancellationToken);

                foreach (var attachment in exportContext.FileAttachments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entryName = NormalizeEntryName(attachment.EntryName);
                    if (!IsSafePayloadEntryName(entryName))
                        throw new InvalidOperationException($"Ungültiger Datei-Payload-Eintrag: {attachment.EntryName}");
                    if (!seenEntries.Add(entryName))
                        throw new InvalidOperationException($"Doppelter ZIP-Eintrag: {entryName}");

                    var fileEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using (var attachmentStream = fileEntry.Open())
                        await attachment.WriteAsync(attachmentStream, cancellationToken);

                    payloadEntries.Add(entryName);
                }

                var manifest = new BackupManifest
                {
                    FormatVersion = 1,
                    ProviderId = _dataProvider.ProviderId,
                    AppName = request.AppName,
                    CreatedAtUtc = createdAt,
                    Generation = request.Generation,
                    PayloadEntries = payloadEntries
                };

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, finalPath, overwrite: false);
            return await DescribeFileAsync(finalPath, cancellationToken);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        await using var copy = new MemoryStream();
        await source.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;

        try
        {
            using var archive = new ZipArchive(copy, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                if (!IsSafeEntryName(entry.FullName))
                    errors.Add($"Unsicherer ZIP-Eintrag: {entry.FullName}");
            }

            var manifest = await ReadManifestAsync(archive, cancellationToken);
            if (manifest is null)
            {
                errors.Add("manifest.json fehlt oder ist ungültig.");
            }
            else
            {
                var archiveShapeValidation = await ValidateArchiveShapeAsync(archive, manifest, cancellationToken);
                if (!archiveShapeValidation.IsValid)
                    errors.AddRange(archiveShapeValidation.Errors);
            }

            var dataEntry = archive.GetEntry("index.json");
            if (dataEntry is null)
            {
                errors.Add("index.json fehlt.");
            }
            else if (errors.Count == 0)
            {
                await using var dataStream = dataEntry.Open();
                var providerValidation = await _dataProvider.ValidateAsync(
                    dataStream,
                    new BackupValidationContext((entryName, token) => OpenPayloadEntryAsync(archive, entryName, token)),
                    cancellationToken);
                if (!providerValidation.IsValid)
                    errors.AddRange(providerValidation.Errors);
            }
        }
        catch (InvalidDataException ex)
        {
            errors.Add($"Datei ist kein gültiges ZIP-Archiv: {ex.Message}");
        }
        catch (JsonException ex)
        {
            errors.Add($"Backup-Metadaten sind nicht lesbar: {ex.Message}");
        }

        return errors.Count == 0 ? BackupValidationResult.Valid : new BackupValidationResult(false, errors);
    }

    /// <inheritdoc />
    public async Task<BackupDescriptor> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken)
    {
        var options = await _optionsProvider.GetOptionsAsync(cancellationToken);
        if (source.CanSeek)
            source.Position = 0;

        var directory = await GetStorageDirectoryAsync(cancellationToken);
        Directory.CreateDirectory(directory);

        var createdAt = _timeProvider.GetUtcNow();
        var finalName = CreateFileName(createdAt, BackupGeneration.Uploaded, _dataProvider.ProviderId, uploaded: true);
        var finalPath = Path.Combine(directory, finalName);
        var tempPath = Path.Combine(directory, $"{finalName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await CopyToFileEnforcingLimitAsync(source, tempPath, options.MaxUploadSizeBytes, cancellationToken);

            await using (var validationStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var validation = await ValidateAsync(validationStream, cancellationToken);
                if (!validation.IsValid)
                    throw new InvalidOperationException(string.Join(" ", validation.Errors));
            }

            File.Move(tempPath, finalPath, overwrite: false);
            return await DescribeFileAsync(finalPath, cancellationToken, BackupGeneration.Uploaded);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static async Task CopyToFileEnforcingLimitAsync(
        Stream source,
        string targetPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var total = 0L;
        var buffer = new byte[81920];
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (maxBytes > 0 && total > maxBytes)
                throw new InvalidOperationException("Die hochgeladene Datei ist zu groß.");

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = await GetKnownPathAsync(fileName, cancellationToken);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = await GetKnownPathAsync(fileName, cancellationToken);
        File.Delete(path);
    }

    private async Task<string> GetKnownPathAsync(string fileName, CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
            throw new InvalidOperationException("Ungültiger Dateiname.");

        var directory = await GetStorageDirectoryAsync(cancellationToken);
        var path = Path.Combine(directory, safeName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Backup wurde nicht gefunden.", safeName);

        return path;
    }

    private async Task<string> GetStorageDirectoryAsync(CancellationToken cancellationToken)
    {
        var options = await _optionsProvider.GetOptionsAsync(cancellationToken);
        var path = options.StoragePath;
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine("Data", "Backups");

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        var root = _environment?.ContentRootPath ?? AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }

    private async Task<BackupDescriptor> DescribeFileAsync(
        string path,
        CancellationToken cancellationToken,
        BackupGeneration? forcedGeneration = null)
    {
        var file = new FileInfo(path);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var manifest = await ReadManifestAsync(archive, cancellationToken);
            if (manifest is null)
                return InvalidDescriptor(file, "manifest.json fehlt oder ist ungültig.");

            var validation = await ValidateArchiveShapeAsync(archive, manifest, cancellationToken);
            return new BackupDescriptor(
                file.Name,
                file.FullName,
                file.Length,
                manifest.CreatedAtUtc,
                forcedGeneration ?? InferGeneration(file.Name, manifest.Generation),
                manifest.ProviderId,
                manifest.FormatVersion,
                validation.IsValid,
                validation.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Backup-Datei {Path} konnte nicht beschrieben werden.", path);
            return InvalidDescriptor(file, ex.Message);
        }
    }

    private async Task<BackupValidationResult> ValidateArchiveShapeAsync(ZipArchive archive, BackupManifest manifest, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var entry in archive.Entries)
        {
            if (!IsSafeEntryName(entry.FullName))
                errors.Add($"Unsicherer ZIP-Eintrag: {entry.FullName}");
        }

        if (manifest.FormatVersion != 1)
            errors.Add($"Nicht unterstützte Formatversion: {manifest.FormatVersion}.");
        if (!string.Equals(manifest.ProviderId, _dataProvider.ProviderId, StringComparison.Ordinal))
            errors.Add("Die Providerkennung passt nicht zu dieser Anwendung.");
        if (archive.GetEntry("index.json") is null)
            errors.Add("index.json fehlt.");

        var payloadEntries = manifest.PayloadEntries ?? new List<string>();
        if (payloadEntries.Count == 0)
        {
            errors.Add("Manifest enthält keine Payload-Einträge.");
        }
        else
        {
            var seenPayloadEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var payloadEntry in payloadEntries)
            {
                if (!IsSafeEntryName(payloadEntry))
                {
                    errors.Add($"Unsicherer Manifest-Payload-Eintrag: {payloadEntry}");
                    continue;
                }

                if (!seenPayloadEntries.Add(payloadEntry))
                    errors.Add($"Doppelter Manifest-Payload-Eintrag: {payloadEntry}");

                if (!IsKnownPayloadEntryName(payloadEntry))
                    errors.Add($"Manifest-Payload-Eintrag {payloadEntry} liegt nicht unter einem bekannten Payload-Pfad.");

                if (archive.GetEntry(payloadEntry) is null)
                    errors.Add($"Manifest-Payload-Eintrag {payloadEntry} fehlt im ZIP.");
            }

            if (!seenPayloadEntries.Contains("index.json"))
                errors.Add("Manifest referenziert index.json nicht.");

            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, "manifest.json", StringComparison.Ordinal))
                    continue;
                if (!seenPayloadEntries.Contains(entry.FullName))
                    errors.Add($"ZIP-Eintrag {entry.FullName} ist nicht im Manifest referenziert.");
            }
        }

        await Task.CompletedTask.WaitAsync(cancellationToken);
        return errors.Count == 0 ? BackupValidationResult.Valid : new BackupValidationResult(false, errors);
    }

    private static async Task<BackupManifest?> ReadManifestAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("manifest.json");
        if (entry is null)
            return null;

        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken);
    }

    private static BackupDescriptor InvalidDescriptor(FileInfo file, string error)
        => new(
            file.Name,
            file.FullName,
            file.Exists ? file.Length : 0,
            file.Exists ? file.CreationTimeUtc : DateTimeOffset.MinValue,
            BackupGeneration.Manual,
            string.Empty,
            0,
            false,
            new[] { error });

    private static string CreateFileName(DateTimeOffset createdAt, BackupGeneration generation, string providerId, bool uploaded)
    {
        var safeProvider = string.Concat(providerId.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' ? ch : '-'));
        var prefix = uploaded ? "uploaded-" : string.Empty;
        return $"{prefix}{createdAt:yyyyMMdd-HHmmss}-{generation.ToString().ToLowerInvariant()}-{safeProvider}.zip";
    }

    private static BackupGeneration InferGeneration(string fileName, BackupGeneration manifestGeneration)
        => fileName.StartsWith("uploaded-", StringComparison.OrdinalIgnoreCase)
            ? BackupGeneration.Uploaded
            : manifestGeneration;

    private static bool IsSafeEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (Path.IsPathRooted(name) || name.Contains(':') || name.Contains('\\'))
            return false;

        var parts = name.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0
            && !name.EndsWith("/", StringComparison.Ordinal)
            && parts.All(part => part != "." && part != "..");
    }

    private static string NormalizeEntryName(string name)
        => name.Replace('\\', '/');

    private static bool IsSafePayloadEntryName(string name)
        => IsSafeEntryName(name)
            && IsKnownPayloadEntryName(name)
            && !name.EndsWith("/", StringComparison.Ordinal);

    private static bool IsKnownPayloadEntryName(string name)
        => string.Equals(name, "index.json", StringComparison.Ordinal)
            || (name.StartsWith("files/", StringComparison.Ordinal) && name.Length > "files/".Length)
            || (name.StartsWith("entities/", StringComparison.Ordinal) && name.Length > "entities/".Length);

    private static Task<Stream> OpenPayloadEntryAsync(ZipArchive archive, string entryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeEntryName(entryName);
        if (!IsSafePayloadEntryName(normalized))
            throw new InvalidDataException($"Ungültiger Payload-Eintrag: {entryName}");

        var payloadEntry = archive.GetEntry(normalized)
            ?? throw new FileNotFoundException("Payload-Eintrag wurde im Backup nicht gefunden.", normalized);

        return Task.FromResult(payloadEntry.Open());
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
