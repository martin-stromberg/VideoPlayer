using Microsoft.EntityFrameworkCore;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Persists backup operation history entries.
/// </summary>
public sealed class BackupOperationHistoryService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Creates a new history service.
    /// </summary>
    public BackupOperationHistoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Adds a completed operation history row.
    /// </summary>
    public async Task AddAsync(
        string operation,
        BackupOperationResult result,
        string? userId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _db.BackupOperationHistories.Add(new BackupOperationHistory
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            Operation = operation,
            FileName = result.Descriptor?.FileName,
            Generation = result.Descriptor?.Generation.ToString(),
            Succeeded = result.Succeeded,
            UserId = userId,
            Message = result.Succeeded ? result.Message : string.Join(" ", result.Errors.DefaultIfEmpty(result.Message))
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the latest operation history rows.
    /// </summary>
    public Task<List<BackupOperationHistory>> GetLatestAsync(int count = 25, CancellationToken cancellationToken = default)
        => _db.BackupOperationHistories
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
}
