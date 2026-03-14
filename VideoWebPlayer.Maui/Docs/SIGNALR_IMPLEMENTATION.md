# SignalR Echtzeit-Updates - Implementation

## Übersicht
SignalR wurde implementiert für automatische Echtzeit-Updates der Media-Listen in der MAUI-App.

## Architektur

```
Server                          MAUI Client
┌──────────────────┐           ┌──────────────────┐
│ MediaUpdateHub   │◄─────────►│ SignalRService   │
└──────────────────┘           └──────────────────┘
        │                               │
        ├─ NewVideosScanned            ├─► OnNewVideosScanned
        ├─ ContinueWatchingUpdated     ├─► OnContinueWatchingUpdated
        └─ FavoritesChanged            └─► OnFavoritesChanged
```

## Implementierte Features

### 1. Server-seitiger SignalR Hub
**Datei:** `VideoWebPlayer/Hubs/MediaUpdateHub.cs`

**Events:**
- `NewVideosScanned(sourceId, count)` → Broadcast an alle Clients
- `ContinueWatchingUpdated()` → An spezifischen User
- `FavoritesChanged()` → An spezifischen User

### 2. MAUI SignalR Client Service
**Datei:** `VideoWebPlayer.Maui/Services/SignalRService.cs`

**Features:**
- ✅ Automatische Verbindung beim App-Start
- ✅ Bearer-Token Authentication
- ✅ Automatische Reconnection bei Verbindungsabbruch
- ✅ Event-basiertes Push-System
- ✅ Thread-safe Operations

**Public API:**
```csharp
public event EventHandler? ContinueWatchingUpdated;
public event EventHandler? FavoritesChanged;
public event EventHandler<NewVideosScannedEventArgs>? NewVideosScanned;

public async Task ConnectAsync(string serverAddress, string token);
public async Task DisconnectAsync();
public bool IsConnected { get; }
```

### 3. HomePage Integration
**Datei:** `VideoWebPlayer.Maui/HomePage.xaml.cs`

**Event-Handler:**
```csharp
private async void OnContinueWatchingUpdated(object? sender, EventArgs e)
{
    // Lade Continue-Watching Liste neu
    _viewModel.ContinueWatching.Items.Clear();
    await _viewModel.LoadDataAsync();
}

private async void OnFavoritesChanged(object? sender, EventArgs e)
{
    // Lade Favoriten neu
    _viewModel.Favorites.Items.Clear();
    await _viewModel.LoadDataAsync();
}

private async void OnNewVideosScanned(object? sender, NewVideosScannedEventArgs e)
{
    // Lade Recent Entries neu
    _viewModel.RecentEntries.Items.Clear();
    await _viewModel.LoadDataAsync();
}
```

## Registrierung

### Server (`ServiceCollectionExtensions.cs`):
```csharp
services.AddSignalR();
```

### Server (`WebApplicationExtensions.cs`):
```csharp
app.MapHub<MediaUpdateHub>("/hubs/mediaupdate");
```

### MAUI (`MauiProgram.cs`):
```csharp
builder.Services.AddSingleton<Services.SignalRService>();
```

### MAUI (`App.xaml.cs`):
```csharp
await signalRService.ConnectAsync(serverAddress, token);
```

## Event-Flow

### Continue-Watching Update:
```
User spielt Video
↓
ReportProgress API-Call
↓
ContinueWatchingService.ReportProgressAsync()
↓
UpsertAsync() → SaveChanges
↓
_hubContext.Clients.User(userId).SendAsync("ContinueWatchingUpdated")
↓
SignalRService empfängt Event
↓
HomePage.OnContinueWatchingUpdated()
↓
Liste wird neu geladen
↓
UI aktualisiert sich automatisch
```

### Neue Videos gescannt:
```
MediaSourceScanService läuft
↓
Neue Videos gefunden
↓
_hubContext.Clients.All.SendAsync("NewVideosScanned", sourceId, count)
↓
Alle verbundenen Clients empfangen Event
↓
HomePage.OnNewVideosScanned()
↓
Recent Entries werden neu geladen
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
- ✅ Optional: Toast-Benachrichtigungen möglich

### 4. Robust:
- ✅ Automatische Reconnection bei Verbindungsverlust
- ✅ Fehler-Logging
- ✅ Graceful Degradation (ohne SignalR funktioniert App trotzdem)

## Server-seitige Events triggern

### In ContinueWatchingService:
```csharp
await _hubContext.Clients.User(userId).SendAsync("ContinueWatchingUpdated");
```

### In MediaSourceScanService:
```csharp
await _hubContext.Clients.All.SendAsync("NewVideosScanned", sourceId, newVideoCount);
```

### In FavoritesService:
```csharp
await _hubContext.Clients.User(userId).SendAsync("FavoritesChanged");
```

## Testing

### 1. SignalR Connection testen:
```csharp
// Im Debug Output suchen:
[SignalR] Connecting to: http://server:5000/hubs/mediaupdate
[SignalR] Connected successfully. ConnectionId: [ID]
```

### 2. Event-Empfang testen:
```csharp
// Video abspielen → Continue-Watching-Update:
[SignalR] ContinueWatchingUpdated received
[HomePage] SignalR: Continue-Watching updated - refreshing list
```

### 3. Reconnection testen:
```csharp
// Server stoppen und wieder starten:
[SignalR] Connection closed. Error: [...]
[SignalR] Reconnecting...
[SignalR] Reconnected. ConnectionId: [NEW-ID]
```

## Erweiterungsmöglichkeiten

### Optional: Download-Completed Event:
```csharp
// Server:
public async Task NotifyDownloadCompleted(string userId, long videoId, string videoType)
{
    await Clients.User(userId).SendAsync("DownloadCompleted", videoId, videoType);
}

// Client:
_connection.On<long, string>("DownloadCompleted", (videoId, videoType) =>
{
    DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(videoId, videoType));
});
```

### Optional: Toast-Benachrichtigungen:
```csharp
private async void OnNewVideosScanned(object? sender, NewVideosScannedEventArgs e)
{
    await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        // Zeige Toast
        await DisplayAlert("Neue Videos", $"{e.Count} neue Videos verfügbar!", "OK");
        
        // Liste aktualisieren
        await _viewModel.RefreshRecentEntriesAsync();
    });
}
```

## Dependencies

### NuGet Packages:
- **Server**: Bereits enthalten in ASP.NET Core
- **MAUI**: `Microsoft.AspNetCore.SignalR.Client` Version 10.0.0

### Konfiguration:
- **Hub URL**: `/hubs/mediaupdate`
- **Authentication**: Bearer Token
- **Transport**: WebSockets (Fallback auf Server-Sent Events)

## Troubleshooting

### Problem: "Unauthorized" beim Verbinden
**Lösung**: Token korrekt in AccessTokenProvider übergeben
```csharp
options.AccessTokenProvider = () => Task.FromResult<string?>(token);
```

### Problem: Events kommen nicht an
**Lösung**: 
1. Prüfe ConnectionId im Debug Output
2. Prüfe ob User ID korrekt ist
3. Server muss `Clients.User(userId)` verwenden (nicht `.All`)

### Problem: Verbindung bricht ab
**Lösung**: Automatische Reconnection ist bereits implementiert
```csharp
.WithAutomaticReconnect()
```

## Status

✅ **Server-seitiger Hub**: Vollständig implementiert
✅ **MAUI Client Service**: Vollständig implementiert
✅ **HomePage Integration**: Event-Handler registriert
✅ **Continue-Watching Service**: SignalR-Updates implementiert
⏳ **MediaSourceScanService**: Noch zu implementieren
⏳ **FavoritesService**: Noch zu implementieren

## Next Steps

1. **SignalR-Updates in MediaSourceScanService hinzufügen**
2. **SignalR-Updates in FavoritesService hinzufügen**
3. **Optional**: Toast-Benachrichtigungen für neue Videos
4. **Optional**: Badge-Counts für neue Content
5. **Optional**: Download-Completed Events
