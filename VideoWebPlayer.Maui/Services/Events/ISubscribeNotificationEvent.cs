namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Interface for subscribing to notification events within the MAUI application.
/// </summary>
public interface ISubscribeNotificationEvent
{
    /// <summary>
    /// Subscribes to a specific type of notification event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to be invoked when the event occurs.</param>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent;

    /// <summary>
    /// Unsubscribes from a specific type of notification event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent;
}
