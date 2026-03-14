namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Base class for all notification events in the MAUI application.
/// </summary>
public abstract class NotificationEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
