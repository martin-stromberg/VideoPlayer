# Event-Management und Event-Publisher

## EventManager

**Datei:** `VideoWebPlayer/Services/EventManager.cs`

Zentrale Klasse für Event-Publishing im System. Verwaltet Event-Listener und Publish-Mechaniken.

### Registrierung

```csharp
services.AddSingleton<EventManager>();  // In ServiceCollectionExtensions.cs, Zeile 224
```

**Lifetime:** `Singleton` — Ein EventManager für die ganze App-Lebensdauer

### Verwendung im DbContext

```csharp
public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly EventManager _eventManager;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, EventManager eventManager)
        : base(options)
    {
        _eventManager = eventManager;
    }
}
```

Der EventManager wird in den DbContext injiziert für Event-Publishing nach Datenbankoperationen.

### Publish-Pattern

```csharp
await SaveChangesAsync();
_eventManager.Publish(new MediaSourceCreatedEvent(source));
```

**Pattern:**
1. Datenbankoperation durchführen (`SaveChangesAsync()`)
2. Wenn erfolgreich: Event publizieren via `_eventManager.Publish()`
3. Event enthält relevante Entitäts-Informationen

## Bestehende Events

**Dateiverzeichnis:** `VideoWebPlayer/Events/`

### MediaSourceCreatedEvent

**Datei:** `MediaSourceCreatedEvent.cs`

Wird publiziert, wenn eine neue MediaSource erstellt wird.

**Verwendung in DbContext:**
```csharp
public async Task AddMediaSourceAsync(MediaSource source)
{
    source.CreatedAt = DateTime.UtcNow;
    MediaSources.Add(source);
    await SaveChangesAsync();
    _eventManager.Publish(new MediaSourceCreatedEvent(source));
}
```

### MediaSourceUpdatedEvent

**Datei:** `MediaSourceUpdatedEvent.cs`

Wird publiziert, wenn eine MediaSource aktualisiert wird.

### MediaSourceDeletedEvent

**Datei:** `MediaSourceDeletedEvent.cs`

Wird publiziert, wenn eine MediaSource gelöscht wird.

### BackgroundProcessingStatusEvent

**Datei:** `BackgroundProcessingStatusEvent.cs`

Wird publiziert für Background-Processing-Status-Updates (z.B. Scan-Fortschritt).

## Event-Interface / Base-Klasse

**Annahme:** Events erben von einer gemeinsamen Base oder implementieren ein Interface (Details müssen in den Event-Dateien überprüft werden).

**Typisches Pattern:**
```csharp
public class MediaSourceCreatedEvent
{
    public MediaSourceCreatedEvent(MediaSource source)
    {
        Source = source;
    }

    public MediaSource Source { get; }
}
```

## Signal-R Integration

**Verknüpfung zu Notifications:**

`MediaUpdateNotificationService` wird verwendet, um Event-basierte Änderungen an Clients zu propagieren:

```csharp
await _notificationService.NotifyFavoritesChangedAsync(userId, cancellationToken);
```

**Mechanik:**
- Event wird publiziert (oder Notification wird direkt gesendet)
- SignalR Hub sendet Update an verbundene Clients
- UI wird aktualisiert (Blazor Components, JavaScript)

## Event-Publishing-Architektur

**Bestehender Ansatz:**
- Events sind unidirektional (nur Publish, keine Subscribe-Implementierung im Code)
- Events werden hauptsächlich für Notifications und Logging genutzt
- Keine transaktionalen Event-Handler (Events sind nicht teil der Datenbankoperation)

**Implikation für Playlists:**
- Playlist-Events werden nach `SaveChangesAsync()` publiziert
- Events können für SignalR-Benachrichtigungen oder Audit-Logging genutzt werden
- Keine Business-Logik sollte von Event-Publishings abhängen

## Empfohlene Playlist-Events

Basierend auf der Anforderung und der bestehenden Architektur:

### PlaylistCreatedEvent
```csharp
public class PlaylistCreatedEvent
{
    public PlaylistCreatedEvent(Playlist playlist)
    {
        Playlist = playlist;
    }
    public Playlist Playlist { get; }
}
```

**Publizieren in PlaylistService:**
```csharp
await _db.Playlists.AddAsync(playlist, cancellationToken);
await _db.SaveChangesAsync(cancellationToken);
_eventManager.Publish(new PlaylistCreatedEvent(playlist));
```

### PlaylistUpdatedEvent
```csharp
public class PlaylistUpdatedEvent
{
    public PlaylistUpdatedEvent(Playlist playlist)
    {
        Playlist = playlist;
    }
    public Playlist Playlist { get; }
}
```

### PlaylistDeletedEvent
```csharp
public class PlaylistDeletedEvent
{
    public PlaylistDeletedEvent(long playlistId, string userId)
    {
        PlaylistId = playlistId;
        UserId = userId;
    }
    public long PlaylistId { get; }
    public string UserId { get; }
}
```

(Bei Löschung ist die Entity möglicherweise schon detached, daher nur Metadata)

## Notification Service Pattern

**Bestehendes Beispiel:** `MediaUpdateNotificationService`

```csharp
// In FavoritesService
await _notificationService.NotifyFavoritesChangedAsync(userId, cancellationToken);
```

**Mögliches Pattern für Playlists:**
```csharp
// Hypothetisch, falls eine PlaylistNotificationService implementiert wird
await _notificationService.NotifyPlaylistsChangedAsync(userId, cancellationToken);
```

Dies würde via SignalR alle Clients des Benutzers benachrichtigen.

## Zusammenhang zu Audit-Logging

Events könnten auch für Audit-Logging genutzt werden (z.B. "User X created playlist Y at time Z"). Die Anforderung erwähnt optionales Audit-Logging, daher könnte eine Audit-Event-Listener implementiert werden, die Events abfängt und loggt.

**Kein Code für Audit-Listeners existiert bisher**, aber das Event-System ist dafür vorbereitet (Publish-Subscribe-Pattern ermöglicht einfaches Hinzufügen von Listenern).
