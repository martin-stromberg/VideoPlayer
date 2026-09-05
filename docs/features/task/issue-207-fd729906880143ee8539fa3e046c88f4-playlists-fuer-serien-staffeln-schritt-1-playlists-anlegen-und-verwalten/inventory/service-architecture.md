# Service-Architektur und Dependency Injection

## IFavoritesService (Referenz-Interface)

**Datei:** `VideoWebPlayer/Services/IFavoritesService.cs`

Service-Interface für die Verwaltung von Favoriten eines Benutzers.

### Methoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetFavoritesAsync` | `userId: string`, `cancellationToken: CancellationToken` | `Task<DtoFavoriteEntry[]>` | Ruft alle Favoriten eines Benutzers ab |
| `AddFavoriteAsync` | `userId: string`, `entry: FavoriteEntry`, `cancellationToken: CancellationToken` | `Task` | Fügt einen Favoriten für einen Benutzer hinzu |
| `RemoveFavoriteAsync` | `userId: string`, `entry: FavoriteEntry`, `cancellationToken: CancellationToken` | `Task` | Entfernt einen Favoriten für einen Benutzer |
| `ToggleFavoriteAsync` | `userId: string`, `entry: DtoMediaEntry`, `cancellationToken: CancellationToken` | `Task<bool>` | Toggelt den Favorite-Status, rückgabe gibt neuen Status an |

### Muster

- Alle Operationen sind **async** und nehmen `CancellationToken` an
- `userId` wird immer als Parameter übergeben, nicht aus `CurrentUser` abgerufen (ermöglicht Testability)
- Rückgabewerte sind DTOs oder primitive Typen, nie Domain-Entities

## FavoritesService (Referenz-Implementation)

**Datei:** `VideoWebPlayer/Services/FavoritesService.cs`

Implementiert `IFavoritesService` und enthält die CRUD-Logik sowie Event-Publishing.

### Abhängigkeiten

| Abhängigkeit | Typ | Zweck |
|--------------|-----|-------|
| `_db` | `ApplicationDbContext` | Datenbankzugriff |
| `_notificationService` | `MediaUpdateNotificationService` | SignalR-Benachrichtigungen für Favoriten-Änderungen |
| `_watchedStatusService` | `WatchedStatusService` | Optional: Anreicherung von Favoriten mit Watched-Status |

### Konstruktor

```csharp
public FavoritesService(
    ApplicationDbContext db,
    MediaUpdateNotificationService notificationService,
    WatchedStatusService? watchedStatusService = null)
{
    _db = db;
    _notificationService = notificationService;
    _watchedStatusService = watchedStatusService ?? new WatchedStatusService(db);
}
```

### Implementierte Methoden

**`GetFavoritesAsync(userId, cancellationToken)`**
- Filtert `FavoriteEntries` nach `UserId`
- Erzeugt DTOs via `Create<DtoFavoriteEntry>(rec)`
- Lädt entsprechende Media-Entitäten (Movie, TVShow, etc.)
- Bereichert DTOs mit Watched-Status via `_watchedStatusService.EnrichAsync()`

**`AddFavoriteAsync(userId, entry, cancellationToken)`**
- Setzt `entry.UserId = userId`
- Setzt `entry.CreatedAt = DateTime.UtcNow`
- Speichert in DB via `_db.SaveChangesAsync()`
- Publiziert Benachrichtigung via `_notificationService.NotifyFavoritesChangedAsync()`

**`RemoveFavoriteAsync(userId, entry, cancellationToken)`**
- Findet Favorite durch komplexe Bedingungen (MovieId, TVShowId, etc.)
- Entfernt aus DB
- Publiziert Benachrichtigung

**`ToggleFavoriteAsync(userId, entry, cancellationToken)`**
- Prüft Existence
- Fügt hinzu oder entfernt je nach aktuellem Status
- Gibt neuen Status als `bool` zurück

### Hilfsmethoden

**`GetFavoriteEntryAsync(userId, entry, cancellationToken)` (private)**
- Komplexe Logik zur Ermittlung von Favoriten über verschiedene Media-Typen
- Handhabt auch polymorphe Abfragen

**`LoadFavoriteEntryDtoAsync(rec, cancellationToken)` (private)**
- Lädt die entsprechende Media-Entität basierend auf den IDs in `FavoriteEntry`
- Erstellt DTOs mit Hierachie-Informationen (z.B. Episode → Season → Show)

**`Create<T>(ms)` (private, static)**
- Reflection-basiertes Mapping
- Kopiert Properties von Source zu Target-DTO
- Respektiert `IgnoreAssignPropertyAttribute` auf Ziel-Properties

## Dependency Injection Setup

**Datei:** `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

### Registrierung (Zeile 229)

```csharp
services.AddScoped<IFavoritesService, FavoritesService>();
```

**Scope:** `Scoped` — Eine neue Instanz pro HTTP-Request / Blazor-Circuit

### Kontext im gesamten Setup

**Singleton-Services (Leben während der App-Lebensdauer):**
- `EventManager` (Zeile 224)
- `MediaUpdateNotificationService` (Zeile 270)
- `InternalConnectionService` (Zeile 75)

**Scoped-Services (Neu pro Request/Circuit):**
- `IFavoritesService, FavoritesService` (Zeile 229)
- `IUnlockedMediaService, UnlockedMediaService` (Zeile 230)
- `IGenreService, GenreService` (Zeile 231)
- `WatchedStatusService` (Zeile 236)
- Viele weitere...

**Transient-Services (Immer neu):**
- `IAuthService, AuthService` (Zeile 264)

### DbContext Registrierung (Zeile 157)

```csharp
services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
```

Der `ApplicationDbContext` wird ebenfalls mit `Scoped` Lifetime registriert (Standard für DbContext).

## Service-Pattern-Zusammenfassung

**Konventionen im System:**
1. **Interface First:** Public API definiert über Interface
2. **Async/Await:** Alle I/O-Operationen sind async
3. **CancellationToken:** Alle async Methoden akzeptieren `CancellationToken`
4. **Dependency Injection:** Dependencies im Konstruktor, nicht über Properties
5. **DTO-Rückgabe:** Service gibt DTOs zurück, nicht Domain-Entities
6. **userId-Parameter:** Benutzer-ID wird als Parameter übergeben (nicht via CurrentUser)
7. **Event Publishing:** Services publizieren Events nach Änderungen (z.B. via `NotifyFavoritesChangedAsync()`)

Diese Patterns sollten von `IPlaylistService` und `PlaylistService` übernommen werden.
