namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// A <see cref="TimeProvider"/> whose timers fire only when the test calls <see cref="Advance"/>
/// past their due time — drives <c>Task.Delay(span, provider, ct)</c> deterministically without
/// waiting real time (e.g. the schedule worker's generation interval).
/// </summary>
internal sealed class ScheduleTestTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<PendingTimer> _timers = [];
    private DateTimeOffset _now;

    public ScheduleTestTimeProvider(DateTimeOffset start) => _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward and fires every timer whose due time was reached —
    /// earliest first, so chained delays observe a monotonic clock.</summary>
    /// <param name="delta">How far to move the clock forward.</param>
    public void Advance(TimeSpan delta)
    {
        var target = _now + delta;
        while (true)
        {
            PendingTimer? next;
            lock (_gate)
            {
                next = _timers.Where(t => t.DueAt <= target).OrderBy(t => t.DueAt).FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    return;
                }
                _timers.Remove(next);
                _now = next.DueAt;
            }
            next.Fire();
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new PendingTimer(this, callback, state,
            dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : GetUtcNow() + dueTime);
        lock (_gate)
            _timers.Add(timer);
        return timer;
    }

    private void Remove(PendingTimer timer)
    {
        lock (_gate)
            _timers.Remove(timer);
    }

    private sealed class PendingTimer(ScheduleTestTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } = dueAt;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner.GetUtcNow() + dueTime;
            return true;
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            owner.Remove(this);
            return ValueTask.CompletedTask;
        }
    }
}
