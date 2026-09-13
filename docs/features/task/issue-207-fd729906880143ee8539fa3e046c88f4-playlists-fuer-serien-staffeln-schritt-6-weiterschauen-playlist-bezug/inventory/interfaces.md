# Interfaces und Contracts

## `IPlaylistService`

**Status:** Wird in den Anforderung erwähnt (Schritt 5) und sollte bereits existieren.

**Vermutete Hauptmethoden (basierend auf Anforderung):**
- `GetPlaylistAsync(playlistId, ct)` — Ruft eine Playlist mit ihren Einträgen ab
- `ValidatePlaylistOwnershipAsync(userId, playlistId, ct)` — Prüft, ob Benutzer Besitzer der Playlist ist
- `GetPlaylistNameAsync(playlistId, ct)` — Ruft den Namen einer Playlist ab

**Hinweis:** Genaue Signatur nicht in dieser Bestandsaufnahme untersucht; wird als Abhängigkeit in Schritt 6 benötigt für Playlist-Validierung beim Speichern von Weiterschauen-Einträgen

---

## `ILogger<T>`

**Verwendung in den analysierten Klassen:**
- `ContinueWatchingService` — Logging von Warnings bei Playlist-Navigationsfehlern (in VideoPlayer.razor, Zeile 289)

---

## Weitere Dependencies (in ContinueWatchingService injiziert)

- `ApplicationDbContext` — Datenbankzugriff
- `UserManager<ApplicationUser>` — Benutzer-Management
- `ContinueWatchingBuffer` — In-Memory Buffer für Progress-Events
- `MediaUpdateNotificationService` — SignalR-Benachrichtigungen
- `ProgramSettingsService` — Globale Anwendungseinstellungen
- `WatchedStatusService` — Verwaltung der „Gesehen"-Markierungen
- `TimeProvider` — Dependency für System-Zeit (testbar)

---

## API-Requests und Responses

### `ProgressRequest`

**Aktuell (in Controller definiert):**
```csharp
record ProgressRequest(
    string MediaType,          // "movie" oder "episode"
    long MediaId,
    long PositionSeconds,
    long DurationSeconds
);
```

**Erforderliche Erweiterung (Schritt 6):**
```csharp
record ProgressRequest(
    string MediaType,
    long MediaId,
    long PositionSeconds,
    long DurationSeconds,
    long? PlaylistId           // Neu: optional
);
```

---

### `ContinueWatchingActionRequest`

**Aktuell:**
```csharp
record ContinueWatchingActionRequest(
    string MediaType,
    long MediaId
);
```

**Erforderliche Erweiterung (Schritt 6):**
```csharp
record ContinueWatchingActionRequest(
    string MediaType,
    long MediaId,
    long? PlaylistId           // Neu: optional
);
```

---

### `ContinueWatchingMutationResult`

**Aktuell:**
```csharp
record ContinueWatchingMutationResult(
    string Action,              // "hidden", "replaced", "removed"
    string Message
);
```

**Hinweis:** Wird von Hide/Skip-Endpoints zurückgegeben; keine Erweiterung für Schritt 6 erforderlich

---

## Client-APIs

### `IPlaylistApiClient`

**Verwendung in:** `VideoPlayer.razor` (Schritt 5, Navigation)

**Bekannte Methoden (aus VideoPlayer.razor sichtbar):**
- `GetNextPlaylistEntryAsync(playlistId, currentEntryId)` — Ruft nächsten Eintrag ab
- `GetPreviousPlaylistEntryAsync(playlistId, currentEntryId)` — Ruft vorherigen Eintrag ab
- `AdvancePlaylistAsync(playlistId, currentEntryId)` — Auto-Advance
- `StartPlaylistAsync(playlistId, entryId)` — Startet Playlist

**Hinweis:** Genaue Signatur und Rückgabewerte nicht vollständig dokumentiert in dieser Bestandsaufnahme

---

## DTOs (Transfer Objects)

Siehe auch [models.md](models.md) für vollständige DTO-Dokumentation.

### `DtoMediaEntry` (Base)

**Basis für Film/Episode-Darstellung**

### `DtoMovie`

| Eigenschaft | Typ |
|-------------|-----|
| Id | long |
| Name | string |
| FanartPictureId | long? |
| PosterPictureId | long? |
| Collection | DtoMovieCollection? |
| WatchedAt | DateTime? |

### `DtoTVShowEpisode`

| Eigenschaft | Typ |
|-------------|-----|
| Id | long |
| Name | string |
| Number | int |
| Season | DtoTVShowSeason? |
| PosterPictureId | long? |
| FanartPictureId | long? |
| WatchedAt | DateTime? |

### `DtoTVShowSeason`

| Eigenschaft | Typ |
|-------------|-----|
| Id | long |
| Name | string |
| Show | DtoTVShow? |

### `DtoTVShow`

| Eigenschaft | Typ |
|-------------|-----|
| Id | long |
| Name | string |
