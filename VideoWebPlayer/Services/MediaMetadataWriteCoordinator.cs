namespace VideoWebPlayer.Services;

/// <summary>
/// In-process semaphore for metadata writes performed by scans and the manual editor.
/// </summary>
public sealed class MediaMetadataWriteCoordinator : IMediaMetadataWriteCoordinator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <inheritdoc />
    public async Task<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Lease(_semaphore);
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        public Lease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _semaphore.Release();

            return ValueTask.CompletedTask;
        }
    }
}
