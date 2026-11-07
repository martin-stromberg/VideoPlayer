using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Test fake of <see cref="IProgramScheduleSignal"/>: counts
/// <see cref="NotifyLiveScheduleChanged"/> calls (the worker's live-stream wake line);
/// <see cref="WaitAsync"/>/<see cref="WaitLiveAsync"/> stay pending so the worker relies
/// solely on its interval sleep until cancellation.
/// </summary>
internal sealed class FakeScheduleSignal : IProgramScheduleSignal
{
    private int _liveNotifications;

    /// <summary>How often the live-schedule-changed line was fired.</summary>
    public int LiveNotifications => _liveNotifications;

    /// <inheritdoc />
    public void NotifyScheduleChanged()
    {
    }

    /// <inheritdoc />
    public void NotifyScheduleChangedNow()
    {
    }

    /// <inheritdoc />
    public IDisposable BeginScan() => new ScanScopeStub();

    /// <inheritdoc />
    public void NotifyLiveScheduleChanged() => Interlocked.Increment(ref _liveNotifications);

    /// <inheritdoc />
    public ValueTask WaitAsync(CancellationToken cancellationToken)
        => new(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    /// <inheritdoc />
    public ValueTask WaitLiveAsync(CancellationToken cancellationToken)
        => new(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    private sealed class ScanScopeStub : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
