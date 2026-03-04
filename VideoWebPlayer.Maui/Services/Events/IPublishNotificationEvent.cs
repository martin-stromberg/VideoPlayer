namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Interface for publishing notification events within the MAUI application.
/// </summary>
public interface IPublishNotificationEvent
{
    /// <summary>
    /// Publishes a notification event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="notificationEvent">The event instance to publish.</param>
    void Publish<TEvent>(TEvent notificationEvent) where TEvent : NotificationEvent;
}
