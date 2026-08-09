using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Adapts the VideoWebPlayer background gate to the reusable backup library.
/// </summary>
public sealed class VideoWebPlayerBackupRestoreGuard : IBackupRestoreGuard
{
    private readonly IBackgroundProcessingGate _gate;

    /// <summary>
    /// Creates a new restore guard.
    /// </summary>
    public VideoWebPlayerBackupRestoreGuard(IBackgroundProcessingGate gate)
    {
        _gate = gate;
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> EnterRestoreAsync(CancellationToken cancellationToken)
        => _gate.PauseForRestoreAsync(cancellationToken);
}
