using System.Threading.Channels;

namespace VideoWebPlayer.Services;

/// <summary>
/// Wake-up line between the places that change the schedule's inputs (rule CRUD, media creation
/// or deletion) and <see cref="ProgramScheduleWorker"/>: instead of only polling on the generation
/// interval, the worker also wakes when this signal fires.
/// </summary>
public interface IProgramScheduleSignal
{
    /// <summary>
    /// Reports that the schedule inputs changed (a program rule was created, updated, activated,
    /// deactivated or deleted; media relevant to the plan was added or removed). While a scan is
    /// running (see <see cref="BeginScan"/>) this does not wake the worker - the end of the scan does;
    /// outside a scan it wakes the worker at once.
    /// </summary>
    void NotifyScheduleChanged();

    /// <summary>
    /// Announces a scan/classification run. While at least one scan scope is open, notifications do
    /// not wake the worker (a large scan would otherwise wake it hundreds of times); disposing the
    /// last open scope wakes it once — but only when the scan actually suppressed at least one
    /// notification, so a scan without relevant changes does not wake anybody.
    /// </summary>
    /// <returns>The scope to dispose when the scan (including its classification) is over.</returns>
    IDisposable BeginScan();

    /// <summary>
    /// Waits until the worker should re-evaluate the schedule. Several signals raised while nobody
    /// waits are coalesced into one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completing when a signal is pending.</returns>
    ValueTask WaitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default, thread-safe <see cref="IProgramScheduleSignal"/> (registered as a singleton): a
/// one-slot channel that coalesces signals, a counter of currently running scans and a counter
/// of the notifications they suppressed — the last scan's end only wakes when it suppressed at
/// least one. Structurally mirrors <see cref="PlaylistBackfillSignal"/>, but diverges in the
/// wake condition: there the scan's end wakes unconditionally, here only after a suppressed
/// notification.
/// </summary>
public sealed class ProgramScheduleSignal : IProgramScheduleSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    private int _activeScans;
    private int _suppressedNotifications;

    /// <inheritdoc />
    public void NotifyScheduleChanged()
    {
        if (Volatile.Read(ref _activeScans) > 0)
        {
            Interlocked.Increment(ref _suppressedNotifications);
            // The last scan ending between the check and the increment would have drained the
            // counter without seeing this notification — deliver it directly then.
            if (Volatile.Read(ref _activeScans) == 0
                && Interlocked.Exchange(ref _suppressedNotifications, 0) > 0)
                _channel.Writer.TryWrite(true);
            return;
        }

        _channel.Writer.TryWrite(true);
    }

    /// <inheritdoc />
    public IDisposable BeginScan()
    {
        Interlocked.Increment(ref _activeScans);
        return new ScanScope(this);
    }

    /// <inheritdoc />
    public async ValueTask WaitAsync(CancellationToken cancellationToken)
        => await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

    private void EndScan()
    {
        if (Interlocked.Decrement(ref _activeScans) == 0
            && Interlocked.Exchange(ref _suppressedNotifications, 0) > 0)
            _channel.Writer.TryWrite(true);
    }

    private sealed class ScanScope : IDisposable
    {
        private ProgramScheduleSignal? _owner;

        public ScanScope(ProgramScheduleSignal owner) => _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.EndScan();
    }
}
