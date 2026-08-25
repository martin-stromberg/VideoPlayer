# MAUI Event-System - Technical Documentation

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Entwickler  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2024

## Übersicht

Das MAUI Event-System bietet eine zentrale Event-Infrastruktur für die Kommunikation innerhalb der MAUI-Anwendung. Es kombiniert SignalR-Backend-Events mit internen Application-Events in einem einheitlichen Pub/Sub-System.

## Repository-Kontext

Diese Datei beschreibt die ausgelagerte MAUI-Implementierung. Pfade mit `VideoWebPlayer.Maui/...` beziehen sich auf das separate MAUI-Repository, in dieser Arbeitskopie auf den Klon unter `Sub-Repository/`. Das Web-Repository stellt nur die Backend- und API-Vertraege bereit.

## Architektur

```
┌─────────────────────────────────────────────────────────┐
│                  NotificationEventService               │
│                                                         │
│  ┌─────────────────────┐    ┌──────────────────────┐  │
│  │  IPublishNotification│    │ ISubscribeNotification│  │
│  │  Event              │    │ Event                 │  │
│  └─────────────────────┘    └──────────────────────┘  │
│                                                         │
│         ↑                            ↓                  │
│         │                            │                  │
│    Publishers                   Subscribers             │
│    (Services)                   (UI Components)         │
└─────────────────────────────────────────────────────────┘
           ↑                            
           │ SignalR Events             
           │                            
   ┌───────┴────────┐                  
   │ SignalRService │                  
   └────────────────┘                  
```

## Komponenten

### 1. Base Event Class

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Services/Events/NotificationEvent.cs`

```csharp
public abstract class NotificationEvent
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
```

Alle Events erben von dieser Basisklasse und erhalten automatisch einen Timestamp.

### 2. Interfaces

#### IPublishNotificationEvent

```csharp
public interface IPublishNotificationEvent
{
    void Publish<TEvent>(TEvent notificationEvent) where TEvent : NotificationEvent;
}
```

#### ISubscribeNotificationEvent

```csharp
public interface ISubscribeNotificationEvent
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : NotificationEvent;
}
```

### 3. NotificationEventService

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Services/Events/NotificationEventService.cs`

Zentrale Implementierung mit:
- Pub/Sub-Mechanismus via `ConcurrentDictionary`
- SignalR-Event-Integration
- Thread-safe Operations
- Exception-Handling

```csharp
public class NotificationEventService : IPublishNotificationEvent, ISubscribeNotificationEvent
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly SignalRService? _signalRService;

    public void Publish<TEvent>(TEvent notificationEvent)
    {
        if (_subscribers.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                ((Action<TEvent>)handler).Invoke(notificationEvent);
            }
        }
    }
}
```

## Event-Typen

### Backend-Events (via SignalR)

#### ContinueWatchingUpdatedEvent
```csharp
public class ContinueWatchingUpdatedEvent : NotificationEvent { }
```
Wird ausgelöst, wenn die Continue-Watching-Liste aktualisiert wurde.

#### FavoritesChangedEvent
```csharp
public class FavoritesChangedEvent : NotificationEvent { }
```
Wird ausgelöst, wenn Favoriten geändert wurden.

#### NewVideosScannedEvent
```csharp
public class NewVideosScannedEvent : NotificationEvent
{
    public long SourceId { get; }
    public int Count { get; }
}
```
Wird ausgelöst, wenn neue Videos gescannt wurden.

### Interne Application-Events

#### DownloadCompletedEvent
```csharp
public class DownloadCompletedEvent : NotificationEvent
{
    public DownloadedVideo Download { get; }
}
```
Wird vom `DownloadManager` publiziert, wenn ein Download abgeschlossen wurde.

#### DownloadDeletedEvent
```csharp
public class DownloadDeletedEvent : NotificationEvent
{
    public long VideoId { get; }
    public string VideoType { get; }
    public string Title { get; }
}
```
Wird vom `DownloadManager` publiziert, wenn ein Download gelöscht wurde.

## Verwendung

### 1. Service registrieren (MauiProgram.cs)

```csharp
builder.Services.AddSingleton<NotificationEventService>(sp =>
{
    var signalRService = sp.GetService<SignalRService>();
    return new NotificationEventService(signalRService);
});
builder.Services.AddSingleton<IPublishNotificationEvent>(sp => 
    sp.GetRequiredService<NotificationEventService>());
builder.Services.AddSingleton<ISubscribeNotificationEvent>(sp => 
    sp.GetRequiredService<NotificationEventService>());
```

### 2. Events abonnieren (UI Component)

```csharp
public partial class HomePage : ContentPage
{
    private readonly ISubscribeNotificationEvent? _eventSubscriber;

    public HomePage()
    {
        InitializeComponent();
        
        _eventSubscriber = App.ServiceProvider?.GetService<ISubscribeNotificationEvent>();
        if (_eventSubscriber != null)
        {
            _eventSubscriber.Subscribe<DownloadCompletedEvent>(OnDownloadCompleted);
            _eventSubscriber.Subscribe<NewVideosScannedEvent>(OnNewVideosScanned);
        }
    }

    private async void OnDownloadCompleted(DownloadCompletedEvent e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Download fertig", $"{e.Download.Title} wurde heruntergeladen!", "OK");
            await _viewModel.RefreshDataAsync();
        });
    }
}
```

### 3. Events publizieren (Service)

```csharp
public class DownloadManager
{
    private IPublishNotificationEvent? _eventPublisher;

    public void SetEventPublisher(IPublishNotificationEvent eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task SaveCompletedDownloadAsync(DownloadTask task, long fileSizeBytes)
    {
        var download = new DownloadedVideo { /* ... */ };
        await _database.InsertAsync(download);
        
        // Event publizieren
        _eventPublisher?.Publish(new DownloadCompletedEvent(download));
    }
}
```

## Integration mit DownloadManager

Der `DownloadManager` muss während der App-Initialisierung konfiguriert werden:

```csharp
// In App.xaml.cs - InitializeAfterServices:
public void InitializeAfterServices(IServiceProvider services)
{
    var eventPublisher = services.GetService<IPublishNotificationEvent>();
    if (eventPublisher != null)
    {
        DownloadManager.Instance.SetEventPublisher(eventPublisher);
    }
}
```

## Event-Flow Beispiel

### Download Completed:

```
DownloadQueue.ProcessDownloadAsync()
        ↓
DownloadManager.SaveCompletedDownloadAsync()
        ↓
_eventPublisher.Publish(DownloadCompletedEvent)
        ↓
NotificationEventService verteilt an alle Subscriber:
        ├─→ HomePage.OnDownloadCompleted()
        │   └─→ ViewModel.RefreshDataAsync()
        └─→ NotificationTicker.OnDownloadCompleted()
            └─→ QueueMessage("✅ Download abgeschlossen: ...")
```

## Best Practices

### 1. UI-Thread-Sicherheit

**Immer** `MainThread.InvokeOnMainThreadAsync` verwenden bei UI-Updates:

```csharp
private async void OnEvent(MyEvent e)
{
    await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        // UI-Updates hier
        Label.Text = e.Message;
    });
}
```

### 2. Exception-Handling

Event-Handler sollten Exceptions abfangen:

```csharp
private async void OnEvent(MyEvent e)
{
    try
    {
        await ProcessEventAsync(e);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error handling event: {ex.Message}");
    }
}
```

### 3. Unsubscribe bei Dispose

Wenn Components disposed werden:

```csharp
protected override void OnDisappearing()
{
    base.OnDisappearing();
    _eventSubscriber?.Unsubscribe<MyEvent>(OnMyEvent);
}
```

### 4. Event-Naming

- Events beschreiben **WAS passiert ist** (Vergangenheit)
- **Richtig**: `DownloadCompletedEvent`, `UserLoggedInEvent`
- **Falsch**: `DownloadCompleteEvent`, `LoginUserEvent`

## Eigene Events erstellen

### 1. Event-Klasse definieren:

```csharp
public class PlaybackStartedEvent : NotificationEvent
{
    public long VideoId { get; }
    public string VideoType { get; }
    
    public PlaybackStartedEvent(long videoId, string videoType)
    {
        VideoId = videoId;
        VideoType = videoType;
    }
}
```

### 2. Event publizieren:

```csharp
_eventPublisher?.Publish(new PlaybackStartedEvent(videoId, videoType));
```

### 3. Event abonnieren:

```csharp
_eventSubscriber?.Subscribe<PlaybackStartedEvent>(e => 
{
    Debug.WriteLine($"Playback started: {e.VideoType} {e.VideoId}");
});
```

## Testing

### Unit-Tests für Events:

```csharp
[Fact]
public void PublishEvent_NotifiesAllSubscribers()
{
    // Arrange
    var service = new NotificationEventService();
    var received = false;
    service.Subscribe<TestEvent>(e => received = true);
    
    // Act
    service.Publish(new TestEvent());
    
    // Assert
    Assert.True(received);
}
```

## Performance-Überlegungen

### Thread-Safety
- `ConcurrentDictionary` für Subscriber-Verwaltung
- Handlers werden in Kopie iteriert (ToList())
- Verhindert Modification während Iteration

### Memory
- WeakReferences könnten verwendet werden für automatisches Cleanup
- Aktuell: Manuelle Unsubscribe-Aufrufe erforderlich

### Async Events
- Events sind synchron (Action<T>)
- Async-Operationen müssen vom Handler gestartet werden
- Verhindert Deadlocks in Event-Chain

## Debugging

### Debug-Ausgaben aktiviert:

```
[NotificationEventService] Subscribed to DownloadCompletedEvent. Total subscribers: 2
[NotificationEventService] Publishing event: DownloadCompletedEvent
[NotificationEventService] Notified 2 subscribers for DownloadCompletedEvent
```

### Troubleshooting:

**Problem**: Events kommen nicht an

**Diagnose**:
1. Prüfe ob Service registriert ist
2. Prüfe ob Subscribe aufgerufen wurde
3. Prüfe Debug-Output für "Subscribed to..."

**Problem**: UI friert ein

**Ursache**: Blocking-Operations im Event-Handler

**Lösung**: Async-Operationen in Task starten:
```csharp
private void OnEvent(MyEvent e)
{
    _ = Task.Run(async () => await ProcessAsync(e));
}
```

## Related Documentation

- [SignalR-Implementation](./TECH_SignalR_Implementation.md) - Backend-Event-Integration
- [Notification Ticker](./TECH_Notification_Ticker.md) - UI-Component mit Event-Subscriptions
- [Download Management](./TECH_Download_Management.md) - Event-Publishing aus DownloadManager

---

**Siehe auch:**
- [Observer Pattern](https://refactoring.guru/design-patterns/observer)
- [Pub/Sub Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber)
