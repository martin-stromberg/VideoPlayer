namespace msTools.Backup;

/// <summary>
/// Applies retention to managed backup generations.
/// </summary>
public sealed class BackupRetentionService : IBackupRetentionService
{
    private readonly IBackupStore _store;

    /// <summary>
    /// Creates a new retention service.
    /// </summary>
    public BackupRetentionService(IBackupStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task ApplyAsync(IReadOnlyList<BackupDescriptor> descriptors, BackupRetentionOptions options, CancellationToken cancellationToken)
    {
        await DeleteExpiredAsync(descriptors, BackupGeneration.Son, Math.Max(0, options.SonCount), cancellationToken);
        await DeleteExpiredAsync(descriptors, BackupGeneration.Father, Math.Max(0, options.FatherCount), cancellationToken);
        await DeleteExpiredAsync(descriptors, BackupGeneration.Grandfather, Math.Max(0, options.GrandfatherCount), cancellationToken);
        await DeleteExpiredAsync(descriptors, BackupGeneration.ProgramUpdate, Math.Max(0, options.ProgramUpdateCount), cancellationToken);
    }

    private async Task DeleteExpiredAsync(
        IReadOnlyList<BackupDescriptor> descriptors,
        BackupGeneration generation,
        int keep,
        CancellationToken cancellationToken)
    {
        var expired = descriptors
            .Where(x => x.IsValid && x.Generation == generation)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(keep)
            .ToList();

        foreach (var descriptor in expired)
            await _store.DeleteAsync(descriptor.FileName, cancellationToken);
    }
}
