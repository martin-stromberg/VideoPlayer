namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Coordinates background write operations with exclusive restore operations.
/// </summary>
public interface IBackgroundProcessingGate
{
    /// <summary>
    /// Gets a value indicating whether restore mode currently blocks new operations.
    /// </summary>
    bool IsPausedForRestore { get; }

    /// <summary>
    /// Gets the number of active operations.
    /// </summary>
    int ActiveOperationCount { get; }

    /// <summary>
    /// Enters a background write operation.
    /// </summary>
    Task<IAsyncDisposable> EnterOperationAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses new operations and waits until active operations are completed.
    /// </summary>
    Task<IAsyncDisposable> PauseForRestoreAsync(CancellationToken cancellationToken);
}
