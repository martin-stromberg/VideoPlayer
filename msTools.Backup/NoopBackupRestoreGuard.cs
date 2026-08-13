namespace msTools.Backup;

/// <summary>
/// Default restore guard for hosts without background writers.
/// </summary>
public sealed class NoopBackupRestoreGuard : IBackupRestoreGuard
{
    /// <inheritdoc />
    public Task<IAsyncDisposable> EnterRestoreAsync(CancellationToken cancellationToken)
        => Task.FromResult<IAsyncDisposable>(new NoopLease());

    private sealed class NoopLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
