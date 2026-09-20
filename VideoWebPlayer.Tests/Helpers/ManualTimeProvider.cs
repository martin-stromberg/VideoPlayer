namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// A <see cref="TimeProvider"/> whose "now" only moves when a test calls <see cref="Advance"/>; timers still
/// use the real clock.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualTimeProvider"/> class.
    /// </summary>
    /// <param name="start">The initial time.</param>
    public ManualTimeProvider(DateTimeOffset start) => _now = start;

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="delta">How far to move.</param>
    public void Advance(TimeSpan delta) => _now += delta;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => TimeProvider.System.CreateTimer(callback, state, dueTime, period);
}
