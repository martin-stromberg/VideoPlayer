using System.Threading.Channels;

namespace VideoWebPlayer.Services;

/// <summary>
/// Wake-up line between the places that create media (scan, metadata editing) and
/// <see cref="PlaylistBackfillWorker"/>: instead of the worker polling for work on a timer, it sleeps until
/// this signal fires.
/// </summary>
public interface IPlaylistBackfillSignal
{
    /// <summary>
    /// Reports that <see cref="Data.ApplicationDbContext"/> just committed backfill markers
    /// (<see cref="Data.PlaylistBackfillMarker"/>). While a scan is running (see <see cref="BeginScan"/>) this
    /// does not wake the worker - the end of the scan does; outside a scan it wakes the worker at once.
    /// </summary>
    void NotifyMarkersWritten();

    /// <summary>
    /// Announces a scan/classification run. While at least one scan scope is open, marker commits do not wake
    /// the worker (a large scan would otherwise wake it hundreds of times); disposing the last open scope wakes
    /// it once - "nothing marked" then costs a single cheap query.
    /// </summary>
    /// <returns>The scope to dispose when the scan (including its classification) is over.</returns>
    IDisposable BeginScan();

    /// <summary>
    /// Waits until the worker should look for marked collection media. Several signals raised while nobody
    /// waits are coalesced into one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completing when a signal is pending.</returns>
    ValueTask WaitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default, thread-safe <see cref="IPlaylistBackfillSignal"/> (registered as a singleton): a one-slot channel
/// that coalesces signals, plus a counter of currently running scans.
/// </summary>
public sealed class PlaylistBackfillSignal : IPlaylistBackfillSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    private int _activeScans;

    /// <inheritdoc />
    public void NotifyMarkersWritten()
    {
        if (Volatile.Read(ref _activeScans) > 0)
            return; // The end of the running scan wakes the worker.

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
        if (Interlocked.Decrement(ref _activeScans) == 0)
            _channel.Writer.TryWrite(true);
    }

    private sealed class ScanScope : IDisposable
    {
        private PlaylistBackfillSignal? _owner;

        public ScanScope(PlaylistBackfillSignal owner) => _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.EndScan();
    }
}
