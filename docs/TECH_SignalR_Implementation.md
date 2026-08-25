# SignalR Echtzeit-Updates - Technical Implementation

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Entwickler  
> **Version**: 2.0  
> **Letzte Aktualisierung**: 2024

## Übersicht

SignalR wurde implementiert für automatische Echtzeit-Updates der Media-Listen zwischen Backend und MAUI-Client.

## Repository-Kontext

Backend-Pfade wie `VideoWebPlayer/...` liegen im Web-Repository. MAUI-Pfade wie `VideoWebPlayer.Maui/...` beziehen sich auf das separate MAUI-Repository, in dieser Arbeitskopie auf den Klon unter `Sub-Repository/`.

## Architektur

```
Server (ASP.NET Core)              MAUI Client
┌──────────────────────┐          ┌──────────────────────┐
│ MediaUpdateHub       │◄────────►│ SignalRService       │
│ (SignalR Hub)        │          │                      │
└──────────────────────┘          └──────────────────────┘
         │                                  │
         ├─ NewVideosScanned               ├─► Event Handler
         ├─ ContinueWatchingUpdated        ├─► Event Handler
         └─ FavoritesChanged               └─► Event Handler
                                                    │
                                                    ↓
                                          ┌──────────────────────┐
                                          │ NotificationEvent    │
                                          │ Service              │
                                          └──────────────────────┘
                                                    │
                                                    ↓
                                          ┌──────────────────────┐
                                          │ UI Components        │
                                          │ (HomePage, etc.)     │
                                          └──────────────────────┘
```

## Implementierte Features

### 1. Server-seitiger SignalR Hub

**Datei:** `VideoWebPlayer/Hubs/MediaUpdateHub.cs`

```csharp
public class MediaUpdateHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
```

**Events:**
- `NewVideosScanned(sourceId, count)` → Broadcast an alle Clients
- `ContinueWatchingUpdated()` → An spezifischen User  
- `FavoritesChanged()` → An spezifischen User

### 2. Backend Notification Service

**Datei:** `VideoWebPlayer/Services/MediaUpdateNotificationService.cs`

```csharp
public class MediaUpdateNotificationService
{
    public async Task NotifyNewVideosScannedAsync(long sourceId, int count, CancellationToken ct)
    {
        if (count <= 0) return;
        
        await _hubContext.Clients.All
            .SendAsync("NewVideosScanned", sourceId, count, cancellationToken: ct);
    }
    
    public async Task NotifyContinueWatchingUpdatedAsync(string userId, CancellationToken ct)
    {
        await _hubContext.Clients.User(userId)
            .SendAsync("ContinueWatchingUpdated", cancellationToken: ct);
    }
}
```

**Vorteile:**
- ✅ Zentrale Event-Logik
- ✅ Einheitliches Logging
- ✅ Exception-Handling
- ✅ Entscheidung über Event-Relevanz (z.B. count > 0)

### 3. MAUI SignalR Client Service

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Services/SignalRService.cs`

```csharp
public class SignalRService : IAsyncDisposable
{
    public event EventHandler? ContinueWatchingUpdated;
    public event EventHandler? FavoritesChanged;
    public event EventHandler<NewVideosScannedEventArgs>? NewVideosScanned;

    public async Task ConnectAsync(string serverAddress, string token)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On("ContinueWatchingUpdated", () => {
            ContinueWatchingUpdated?.Invoke(this, EventArgs.Empty);
        });
        
        // ... weitere Event-Handler
        
        await _connection.StartAsync();
    }
}
```

**Features:**
- ✅ Automatische Verbindung beim App-Start
- ✅ Bearer-Token Authentication
- ✅ Automatische Reconnection bei Verbindungsabbruch
- ✅ Event-basiertes Push-System
- ✅ Thread-safe Operations

### 4. MAUI Notification Event Service

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Services/Events/NotificationEventService.cs`

Der `NotificationEventService` fungiert als Brücke zwischen SignalR und der MAUI-App:

```csharp
public class NotificationEventService : IPublishNotificationEvent, ISubscribeNotificationEvent
{
    public NotificationEventService(SignalRService? signalRService)
    {
        if (signalRService != null)
        {
            signalRService.ContinueWatchingUpdated += OnContinueWatchingUpdated;
            signalRService.NewVideosScanned += OnNewVideosScanned;
            // ...
        }
    }

    private void OnContinueWatchingUpdated(object? sender, EventArgs e)
    {
        Publish(new ContinueWatchingUpdatedEvent());
    }
}
```

**Vorteile:**
- ✅ Einheitliches Event-System (SignalR + interne Events)
- ✅ Type-safe Event-Handling
- ✅ Einfache UI-Integration

### 5. UI-Integration (HomePage)

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/HomePage.xaml.cs`

```csharp
public HomePage()
{
    InitializeComponent();
    
    _eventSubscriber = App.ServiceProvider?.GetService<ISubscribeNotificationEvent>();
    if (_eventSubscriber != null)
    {
        _eventSubscriber.Subscribe<ContinueWatchingUpdatedEvent>(OnContinueWatchingUpdated);
        _eventSubscriber.Subscribe<NewVideosScannedEvent>(OnNewVideosScanned);
    }
}

private async void OnContinueWatchingUpdated(ContinueWatchingUpdatedEvent e)
{
    await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        await _viewModel.RefreshDataAsync();
    });
}
```

## Event-Flow

### Continue-Watching Update:

```
User spielt Video
        ↓
ReportProgress API-Call
        ↓
ContinueWatchingService.ProcessBufferedEntryAsync()
        ↓
UpsertAsync() → SaveChanges
        ↓
MediaUpdateNotificationService.NotifyContinueWatchingUpdatedAsync(userId)
        ↓
SignalR Hub → Clients.User(userId).SendAsync("ContinueWatchingUpdated")
        ↓
SignalRService empfängt Event
        ↓
NotificationEventService.Publish(ContinueWatchingUpdatedEvent)
        ↓
HomePage.OnContinueWatchingUpdated()
        ↓
ViewModel.RefreshDataAsync()
        ↓
UI aktualisiert sich automatisch (via PropertyChanged)
```

### Neue Videos gescannt:

```
MediaSourceScanService läuft (stündlich)
        ↓
Neue Videos gefunden (count > 0)
        ↓
MediaUpdateNotificationService.NotifyNewVideosScannedAsync(sourceId, count)
        ↓
SignalR Hub → Clients.All.SendAsync("NewVideosScanned", sourceId, count)
        ↓
Alle verbundenen Clients empfangen Event
        ↓
NotificationEventService.Publish(NewVideosScannedEvent)
        ↓
HomePage.OnNewVideosScanned() + NotificationTicker.OnNewVideosScanned()
        ↓
Liste + Ticker werden aktualisiert
```

## Service-Registrierung

### Backend (`ServiceCollectionExtensions.cs`):

```csharp
services.AddSingleton<MediaUpdateNotificationService>();
services.AddSignalR();
```

### Backend (`WebApplicationExtensions.cs`):

```csharp
app.MapHub<MediaUpdateHub>("/hubs/mediaupdate");
```

### MAUI (`MauiProgram.cs`):

```csharp
builder.Services.AddSingleton<SignalRService>();
builder.Services.AddSingleton<NotificationEventService>(sp =>
{
    var signalRService = sp.GetService<SignalRService>();
    return new NotificationEventService(signalRService);
});
```

### MAUI (`App.xaml.cs`):

```csharp
public async void InitializeAfterServices(IServiceProvider services)
{
    // ... nach Login
    await signalRService.ConnectAsync(serverAddress, token);
}
```

## Vorteile

### 1. Echtzeit-Updates:
- ✅ Keine Wartezeit bis zum nächsten Seitenaufruf
- ✅ Sofortige Sichtbarkeit von Änderungen
- ✅ Multi-Device Sync (Desktop + Mobile gleichzeitig aktualisiert)

### 2. Effizient:
- ✅ Kein Polling nötig (spart Batterie + Bandbreite)
- ✅ Updates nur bei tatsächlichen Änderungen
- ✅ WebSocket-basiert (persistent connection)

### 3. Benutzerfreundlich:
- ✅ Listen sind immer aktuell
- ✅ Nahtlose User Experience
- ✅ Ticker zeigt Benachrichtigungen an

### 4. Robust:
- ✅ Automatische Reconnection bei Verbindungsverlust
- ✅ Fehler-Logging
- ✅ Graceful Degradation (ohne SignalR funktioniert App trotzdem)

### 5. Wartbar:
- ✅ Zentrale Event-Logik im Backend
- ✅ Einheitliches Event-System im Frontend
- ✅ Type-safe mit starker Typisierung

## Testing

### 1. SignalR Connection testen:

```
[SignalR] Connecting to: http://server:5000/hubs/mediaupdate
[SignalR] Connected successfully. ConnectionId: [ID]
```

### 2. Event-Empfang testen:

```
[SignalR] ContinueWatchingUpdated received
[NotificationEventService] SignalR: ContinueWatchingUpdated received
[NotificationEventService] Publishing event: ContinueWatchingUpdatedEvent
[HomePage] Event: Continue-Watching updated - refreshing list
```

### 3. Reconnection testen:

```
[SignalR] Connection closed. Error: [...]
[SignalR] Reconnecting...
[SignalR] Reconnected. ConnectionId: [NEW-ID]
```

## Dependencies

### NuGet Packages:
- **Server**: `Microsoft.AspNetCore.SignalR` (included in ASP.NET Core)
- **MAUI**: `Microsoft.AspNetCore.SignalR.Client` Version 10.0.0

### Konfiguration:
- **Hub URL**: `/hubs/mediaupdate`
- **Authentication**: Bearer Token
- **Transport**: WebSockets (Fallback auf Server-Sent Events)

## Troubleshooting

### Problem: "Unauthorized" beim Verbinden

**Ursache**: Token nicht korrekt übergeben

**Lösung**:
```csharp
options.AccessTokenProvider = () => Task.FromResult<string?>(token);
```

### Problem: Events kommen nicht an

**Diagnose:**
1. Prüfe ConnectionId im Debug Output
2. Prüfe ob User ID korrekt ist  
3. Server muss `Clients.User(userId)` verwenden (nicht `.All`)

**Lösung**: User ID Mapping prüfen in SignalR Claims

### Problem: Verbindung bricht ab

**Lösung**: Automatische Reconnection ist bereits implementiert

```csharp
.WithAutomaticReconnect()
```

## Erweiterungsmöglichkeiten

### Download-Progress Events:

```csharp
// Backend:
await _hubContext.Clients.User(userId)
    .SendAsync("DownloadProgress", videoId, percentComplete);

// Client:
_connection.On<long, int>("DownloadProgress", (videoId, percent) =>
{
    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(videoId, percent));
});
```

### Gruppen-Support (Watch-Together):

```csharp
// User einer Watch-Party hinzufügen:
await _hubContext.Groups.AddToGroupAsync(connectionId, $"party_{partyId}");

// Event an alle Party-Mitglieder:
await _hubContext.Clients.Group($"party_{partyId}")
    .SendAsync("PartySync", playbackPosition);
```

## Status & Roadmap

✅ **Implementiert:**
- Server-seitiger Hub
- Backend Notification Service  
- MAUI Client Service
- Event-System Integration
- HomePage Integration
- NotificationTicker Integration

⏳ **Geplant:**
- Download-Progress Events
- Watch-Party/Group-Synchronisation
- Presence-Status (Online/Offline)
- Typing-Indicators für Chat

## Related Documentation

- [Event-System](./TECH_Event_System.md) - MAUI Notification Infrastructure
- [Notification Ticker](./TECH_Notification_Ticker.md) - Footer-Ticker-Komponente
- [Download Management](./TECH_Download_Management.md) - Offline-Downloads

---

**Siehe auch:**
- [Microsoft SignalR Documentation](https://learn.microsoft.com/aspnet/core/signalr)
- [MAUI SignalR Client](https://learn.microsoft.com/aspnet/core/signalr/dotnet-client)
