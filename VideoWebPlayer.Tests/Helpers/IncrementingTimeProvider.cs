using System.Threading;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class IncrementingTimeProvider : TimeProvider
{
    private readonly TimeSpan _step;
    private DateTimeOffset _current;
    private long _timestamp;

    public IncrementingTimeProvider(DateTimeOffset start, TimeSpan step)
    {
        _current = start;
        _step = step;
    }

    // Expose helpers for tests to inspect the last returned and current time without
    // advancing the provider.
    public DateTimeOffset PeekCurrentUtc()
    {
        return _current;
    }

    public DateTimeOffset PeekLastReturnedUtc()
    {
        return _current - _step;
    }

    public override DateTimeOffset GetUtcNow()
    {
        var current = _current;
        _current = _current.Add(_step);
        return current;
    }

    public void Advance(TimeSpan delta)
    {
        _current = _current.Add(delta);
    }

    public override long GetTimestamp()
    {
        return Interlocked.Increment(ref _timestamp);
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
    }
}
