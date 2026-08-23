using System.IO.Compression;
using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Represents the VideoWebPlayer application backup as a single <see cref="IBackupData"/> object.
/// Internally the backup is stored as a ZIP containing index.json and the table/image payloads.
/// </summary>
public sealed class VideoWebPlayerBackupData : IBackupData
{
    private const string IndexEntryName = "index.json";

    private readonly VideoWebPlayerBackupDataProvider _provider;
    private readonly VideoWebPlayerBackupDataFactory? _factory;
    private readonly BackupExportContext? _exportContext;

    /// <summary>
    /// Gets the unique storage name inside the backup folder.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the content type identifier used by the restore factory.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Creates a backup data object for export.
    /// </summary>
    public VideoWebPlayerBackupData(
        BackupExportContext exportContext,
        string name,
        string contentType,
        VideoWebPlayerBackupDataProvider provider)
    {
        _exportContext = exportContext;
        Name = name;
        ContentType = contentType;
        _provider = provider;
    }

    /// <summary>
    /// Creates a backup data object for restore.
    /// </summary>
    public VideoWebPlayerBackupData(
        string name,
        string contentType,
        VideoWebPlayerBackupDataProvider provider,
        VideoWebPlayerBackupDataFactory factory)
    {
        Name = name;
        ContentType = contentType;
        _provider = provider;
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task WriteToAsync(Stream target, CancellationToken ct = default)
    {
        if (_exportContext is null)
            throw new InvalidOperationException("Export context is not set.");

        using var indexStream = new MemoryStream();
        await _provider.ExportAsync(indexStream, _exportContext, ct);
        indexStream.Position = 0;

        using var zip = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: true);

        var indexEntry = zip.CreateEntry(IndexEntryName, CompressionLevel.Optimal);
        await using (var entryStream = indexEntry.Open())
        {
            await indexStream.CopyToAsync(entryStream, ct);
        }

        foreach (var attachment in _exportContext.FileAttachments)
        {
            var entry = zip.CreateEntry(attachment.EntryName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await attachment.WriteAsync(entryStream, ct);
        }
    }

    /// <inheritdoc />
    public async Task ReadFromAsync(Stream source, CancellationToken ct = default)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        var indexEntry = zip.GetEntry(IndexEntryName)
            ?? throw new InvalidDataException("Backup is missing index.json.");
        await using var indexStream = indexEntry.Open();

        Task<Stream> OpenPayloadEntryAsync(string entryName, CancellationToken token)
        {
            var entry = zip.GetEntry(entryName)
                ?? throw new FileNotFoundException($"Backup payload entry not found: {entryName}");
            return Task.FromResult<Stream>(entry.Open());
        }

        var context = new BackupRestoreContext(
            _factory?.UserId,
            OpenPayloadEntryAsync,
            _factory?.Progress);

        await _provider.RestoreAsync(indexStream, context, ct);
    }
}
