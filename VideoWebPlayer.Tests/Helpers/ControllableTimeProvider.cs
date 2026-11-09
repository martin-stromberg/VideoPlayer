using System.Threading;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// A <see cref="TimeProvider"/> whose timers fire only when the test calls <see cref="TickTimers"/>,
/// so <see cref="PeriodicTimer"/>-based component loops can be driven deterministically without
/// waiting for real time.
/// </summary>
internal sealed class ControllableTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state);
        lock (_timers)
            _timers.Add(timer);
        return timer;
    }

    /// <summary>Fires every live timer once.</summary>
    public void TickTimers()
    {
        ManualTimer[] timers;
        lock (_timers)
            timers = _timers.ToArray();
        foreach (var timer in timers)
            timer.Fire();
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
