# Logikklassen und Services

## `ContinueWatchingService`

**Datei:** `VideoWebPlayer\Services\ContinueWatchingService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetListAsync(user, ct)` | öffentlich | Ruft die Weiterschauen-Liste des Benutzers ab; gibt bis zu 50 Einträge absteigend nach `ListOrder` / `UpdatedAt` / `Id` sortiert zurück |
| `ReportProgressAsync(user, movieId, episodeId, position, duration, ct)` | öffentlich | Puffert Wiedergabefortschritt; ignoriert Positionen unter 5 Sekunden |
| `ProcessBufferedEntryAsync(userId, movieId, episodeId, position, duration, ct)` | öffentlich | Verarbeitet gepufferten Eintrag, prüft Schwelle zum Markieren als „gesehen", erstellt/aktualisiert oder entfernt Einträge |
| `HideAsync(userId, movieId, episodeId, ct)` | öffentlich | Entfernt einen Weiterschauen-Eintrag aus der Liste |
| `SkipAsync(userId, movieId, episodeId, ct)` | öffentlich | Überspringt einen Eintrag, ersetzt ihn durch den nächsten oder entfernt ihn, wenn kein Folgemedium existiert |
| `UpsertAsync(userId, movieId, episodeId, position, duration, ct)` | privat | Erstellt oder aktualisiert einen Eintrag; entfernt ältere Einträge der gleichen Serie/Sammlung |
| `RemoveExistingTVShowEntry(userId, episodeId, ct)` | privat | Entfernt alle anderen Episoden-Einträge der gleichen Serie für den Benutzer |
| `RemoveExtsingMovieCollectionEntry(userId, movieId, ct)` | privat | Entfernt alle anderen Film-Einträge der gleichen Sammlung für den Benutzer |
| `GetNextMovieAsync(movieId, ct)` | privat | Ruft den nächsten Film in der gleichen Sammlung ab |
| `GetNextEpisodeAsync(episodeId, ct)` | privat | Ruft die nächste Episode ab (oder erste Episode der nächsten Staffel, falls keine weiteren Episoden in der aktuellen Staffel) |
| `GetUserIdAsync(principal, ct)` | privat | Extrahiert Benutzer-ID aus Claims Principal |
| `Create<T>(ms)` | geschützt | Generische DTO-Erstellung durch Reflexion |

**Abonnierte Events:** Keine explizit dokumentiert in dieser Klasse

**Publizierte Events:**
- `MediaUpdateNotificationService.NotifyContinueWatchingUpdatedAsync(userId, ct)` — Sendet SignalR-Benachrichtigungen bei Änderungen der Weiterschauen-Liste an den Benutzer

---

## `ContinueWatchingBuffer`

**Kurzbeschreibung:** In-Memory-Puffer für Wiedergabefortschritts-Events (nicht vollständig in dieser Bestandsaufnahme untersucht; wird von `ReportProgressAsync` verwendet)

---

## `WatchedStatusService`

**Kurzbeschreibung:** Service zur Verwaltung der globalen „Gesehen"-Markierungen. Wird von `ContinueWatchingService.ProcessBufferedEntryAsync` aufgerufen, um ein Video als „gesehen" zu markieren, wenn der Schwellenwert erreicht ist.

---

## `ContinueWatchingController`

**Datei:** `VideoWebPlayer\Controllers\ContinueWatchingController.cs`

**API-Endpoints:**

| Endpoint | Methode | Zweck |
|----------|--------|-------|
| `/api/continue-watching` | GET | Ruft die Weiterschauen-Liste ab |
| `/api/continue-watching/progress` | POST | Meldet Wiedergabefortschritt (ProgressRequest) |
| `/api/continue-watching/hide` | POST | Verbirgt einen Eintrag (ContinueWatchingActionRequest) |
| `/api/continue-watching/skip` | POST | Überspringt einen Eintrag (ContinueWatchingActionRequest) |

**ProgressRequest (aktuell):**
```csharp
record ProgressRequest(
    string MediaType,           // "movie" oder "episode"
    long MediaId,
    long PositionSeconds,
    long DurationSeconds
);
```

**Fehlende Parameter (erforderlich für Anforderung):**
- `PlaylistId` (long?, optional) — wird aktuell nicht unterstützt

**ContinueWatchingActionRequest (aktuell):**
```csharp
record ContinueWatchingActionRequest(
    string MediaType,
    long MediaId
);
```

**Fehlende Parameter (erforderlich für Anforderung):**
- `PlaylistId` (long?, optional) — wird aktuell nicht unterstützt

**Authentifizierung:** `[BearerTokenCheck]` auf Controller-Ebene erforderlich

---

## `VideoPlayer.razor`

**Datei:** `VideoWebPlayer\Components\Shared\Media\VideoPlayer.razor`

**Parameter (Playlist-Kontext von Schritt 5):**
- `PlaylistContext` (PlaylistPlaybackContext?) — Optional, nur gesetzt beim Start aus einer Playlist
- `CurrentPlaylistPosition` (int?) — Aktuelle Position in der Playlist

**Ereignisse:**
- `CurrentPlaylistEntryIdChanged` — Wird aufgerufen, wenn der aktuelle Wiedergabeeintrag sich ändert (Next/Previous/Advance)
- `PositionChanged` — Wird aufgerufen, wenn die Position aktualisiert wird

**Lokaler Zustand:**
- `currentPlaylistId` (long?) — Aktuelle Playlist-ID
- `currentPlaylistEntryId` (long?) — Aktuelle Eintrag-ID in der Playlist
- `playlistTotalCount` (int?) — Gesamtanzahl der Einträge
- `currentPlaylistName` (string) — Name der Playlist für Anzeige
- `playlistEndReached` (bool) — Markiert, ob das Ende erreicht ist

**Methoden:**
- `ApplyPlaylistContext(context, position)` — Setzt den lokalen Playlist-Kontext
- `ClearPlaylistContext()` — Löscht den Playlist-Kontext
- `OnNextPlaylistEntryAsync()` — Navigation zum nächsten Eintrag
- `OnPreviousPlaylistEntryAsync()` — Navigation zum vorherigen Eintrag
- `OnMediaEndAsync()` — Auto-Advance beim Medienende
- `OnRestartPlaylistAsync()` — Neustart der Playlist
- `UpdatePosition(seconds, duration)` — JavaScript-Callback für Position-Updates

**Wichtig für Schritt 6:**
- Ruft `continueWatching.attach()` auf, ohne derzeit `PlaylistId` zu übergeben
- Muss erweitert werden, um PlaylistId beim Progress-Report zu unterstützen

---

## JavaScript `continueWatching.js`

**Datei:** `VideoWebPlayer\wwwroot\js\continueWatching.js`

**Funktion `attach(videoEl, mediaType, mediaId, baseUrl, bearerToken)`:**
- Registriert Event-Listener auf dem `<video>`-Element (timeupdate, pause, ended)
- Sendet Progress-Updates an `/api/continue-watching/progress`
- Payload: `{ mediaType, mediaId, positionSeconds, durationSeconds }`

**Fehlende Unterstützung (erforderlich für Anforderung):**
- `playlistId` wird aktuell nicht in die Payload aufgenommen
- Die Funktion-Signatur muss erweitert werden, um `playlistId` zu akzeptieren

**Hinweis:** Funktion wird von `VideoPlayer.razor` in `OnAfterRenderAsync` aufgerufen (Zeile 156)
