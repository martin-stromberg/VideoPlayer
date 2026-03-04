using System.Collections.Concurrent;

namespace VideoWebPlayer.Maui.Services.Events;

/// <summary>
/// Service for managing notification events within the MAUI application.
/// Provides publish/subscribe functionality and integrates with SignalR events.
/// </summary>
public class NotificationEventService : IPublishNotificationEvent, ISubscribeNotificationEvent
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly SemaphoreSlim _subscriberLock = new(1, 1);
    private readonly SignalRService? _signalRService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationEventService"/> class.
    /// </summary>
    /// <param name="signalRService">Optional SignalR service for backend events.</param>
    public NotificationEventService(SignalRService? signalRService = null)
    {
        _signalRService = signalRService;

        // Register SignalR event handlers if service is available
        if (_signalRService != null)
        {
            _signalRService.ContinueWatchingUpdated += OnContinueWatchingUpdated;
            _signalRService.FavoritesChanged += OnFavoritesChanged;
            _signalRService.NewVideosScanned += OnNewVideosScanned;

            System.Diagnostics.Debug.WriteLine("[NotificationEventService] Registered SignalR event handlers");
        }
    }

    /// <summary>
    /// Publishes a notification event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="notificationEvent">The event instance to publish.</param>
    public void Publish<TEvent>(TEvent notificationEvent) where TEvent : NotificationEvent
    {
        if (notificationEvent == null)
            throw new ArgumentNullException(nameof(notificationEvent));

        var eventType = typeof(TEvent);
        
        System.Diagnostics.Debug.WriteLine($"[NotificationEventService] Publishing event: {eventType.Name}");

        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            // Create a copy of handlers to avoid modification during iteration
            var handlersCopy = handlers.ToList();
            
            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<TEvent>)handler).Invoke(notificationEvent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationEventService] Error invoking handler for {eventType.Name}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[NotificationEventService] Notified {handlersCopy.Count} subscribers for {eventType.Name}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationEventService] No subscribers for {eventType.Name}");
        }
    }

    /// <summary>
    /// Subscribes to a specific type of notification event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to be invoked when the event occurs.</param>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);

        _subscriberLock.Wait();
        try
        {
            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<Delegate>();
            }

            _subscribers[eventType].Add(handler);
            
            System.Diagnostics.Debug.WriteLine($"[NotificationEventService] Subscribed to {eventType.Name}. Total subscribers: {_subscribers[eventType].Count}");
        }
        finally
        {
            _subscriberLock.Release();
        }
    }

    /// <summary>
    /// Unsubscribes from a specific type of notification event.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);

        _subscriberLock.Wait();
        try
        {
            if (_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                
                System.Diagnostics.Debug.WriteLine($"[NotificationEventService] Unsubscribed from {eventType.Name}. Remaining subscribers: {handlers.Count}");

                if (handlers.Count == 0)
                {
                    _subscribers.TryRemove(eventType, out _);
                }
            }
        }
        finally
        {
            _subscriberLock.Release();
        }
    }

    #region SignalR Event Handlers

    private void OnContinueWatchingUpdated(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[NotificationEventService] SignalR: ContinueWatchingUpdated received");
        Publish(new ContinueWatchingUpdatedEvent());
    }

    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[NotificationEventService] SignalR: FavoritesChanged received");
        Publish(new FavoritesChangedEvent());
    }

    private void OnNewVideosScanned(object? sender, NewVideosScannedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationEventService] SignalR: NewVideosScanned received (Source: {e.SourceId}, Count: {e.Count})");
        Publish(new NewVideosScannedEvent(e.SourceId, e.Count));
    }

    #endregion
}
