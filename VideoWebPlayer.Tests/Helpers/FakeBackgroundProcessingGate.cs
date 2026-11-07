using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Test fake of <see cref="IBackgroundProcessingGate"/>: every lease appends
/// <c>gate:enter</c>/<c>gate:exit</c> to a shared order log so tests can prove the schedule
/// pass runs inside the lease.
/// </summary>
internal sealed class FakeBackgroundProcessingGate : IBackgroundProcessingGate
{
    private readonly IList<string> _orderLog;

    public FakeBackgroundProcessingGate(IList<string> orderLog) => _orderLog = orderLog;

    /// <summary>Operation names the leases were entered with.</summary>
    public List<string> OperationNames { get; } = [];

    /// <inheritdoc />
    public bool IsPausedForRestore => false;

    /// <inheritdoc />
    public int ActiveOperationCount { get; private set; }

    /// <inheritdoc />
    public Task<IAsyncDisposable> EnterOperationAsync(string name, CancellationToken cancellationToken)
    {
        lock (_orderLog)
        {
            _orderLog.Add("gate:enter");
            OperationNames.Add(name);
            ActiveOperationCount++;
        }
        return Task.FromResult<IAsyncDisposable>(new Lease(this));
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> PauseForRestoreAsync(CancellationToken cancellationToken)
        => Task.FromResult<IAsyncDisposable>(new Lease(this));

    private sealed class Lease(FakeBackgroundProcessingGate owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            lock (owner._orderLog)
            {
                owner._orderLog.Add("gate:exit");
                owner.ActiveOperationCount--;
            }
            return ValueTask.CompletedTask;
        }
    }
}
