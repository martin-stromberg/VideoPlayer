# Logik-Komponenten - Playlist-Wiedergabe

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

### Öffentliche Methoden (aus `IPlaylistService`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, ct)` | `public` | Ruft alle Playlists für einen Benutzer ab |
| `GetPlaylistAsync(playlistId, userId, ct)` | `public` | Ruft eine einzelne Playlist ab |
| `CreatePlaylistAsync(...)` | `public` | Erstellt eine neue Playlist |
| `UpdatePlaylistAsync(...)` | `public` | Aktualisiert Name/Beschreibung einer Playlist |
| `DeletePlaylistAsync(playlistId, userId, ct)` | `public` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync(...)` | `public` | Fügt Medien zur Playlist hinzu (mit Kaskadenauflösung) |
| `RemoveMediaFromPlaylistAsync(...)` | `public` | Entfernt Medien aus der Playlist |
| `GetPlaylistEntriesAsync(playlistId, userId, ct)` | `public` | Ruft alle Einträge einer Playlist ab (sortiert) |
| `GetPlaylistEntriesPagedAsync(...)` | `public` | Ruft Einträge paginiert ab (sortiert) |
| `ReorderPlaylistEntryAsync(...)` | `public` | Ändert die manuelle Sortierreihenfolge eines Eintrags |
| `BatchReorderPlaylistEntriesAsync(...)` | `public` | Ändert mehrere Einträge atomar in der Sortierreihenfolge |
| `ChangeSortModeAsync(...)` | `public` | Wechselt den Sortiermodus einer Playlist |
| `GetMaxSortOrderAsync(playlistId, userId, ct)` | `public` | Ruft die maximale `SortOrder` einer Playlist ab |
| `MoveEntryToBeginningAsync(...)` | `public` | Verschiebt einen Eintrag an den Anfang |
| `MoveEntryBetweenAsync(...)` | `public` | Verschiebt einen Eintrag zwischen zwei Positionen |

### Private Hilfsmethoden (relevant für Wiedergabe)

| Methode | Zweck |
|---------|-------|
| `SortPlaylistEntriesForModeAsync(mode, entries, ct)` | **Zentral für Wiedergabe**: Sortiert Einträge nach dem aktuellen Sortiermodus |
| `SortPlaylistEntriesByReleaseDateAsync(entries, ct)` | Sortiert Einträge nach Erscheinungsdatum mit Fallback-Kette (Release Date → Hierarchie → Aufnahmezeitpunkt) |
| `BuildPlaylistEntriesSortKey(...)` | Erstellt Sortierungsschlüssel für Release-Date-Sortierung |
| `LoadValidPlaylistEntriesAsync(playlistId, ct)` | Lädt Einträge und entfernt verwaiste Einträge (deren Medien gelöscht wurden) |
| `BuildEntryDtosAsync(...)` | Konvertiert `PlaylistEntry` zu `DtoPlaylistEntry` mit aufgelösten Werten und Freischaltungsprüfung |

### Abhängigkeiten

| Abhängigkeit | Typ | Zweck |
|-------------|-----|-------|
| `ApplicationDbContext` | `private readonly` | Datenbankkontext |
| `PlaylistSettings` | `private readonly` | Konfiguration (Max. Playlists pro Benutzer, Max. Einträge pro Playlist, Standard-Seitengröße) |
| `PlaylistEntryAccessResolver` | `private readonly` | Prüft Freischaltungsstatus von Einträgen |
| `PlaylistEntryReorderService` | `private readonly` | Verwaltet manuelle Sortierreihenfolge |

**Noch nicht implementiert für diese Anforderung:**
- `GetNextPlaylistEntryAsync(playlistId, currentEntryId)` - findet nächsten abzuspielenden Eintrag
- `GetPreviousPlaylistEntryAsync(playlistId, currentEntryId)` - findet vorherigen abzuspielenden Eintrag
- `StartPlaylistAsync(playlistId, entryId?)` - initiiert Wiedergabe einer Playlist
- `AdvancePlaylistAsync(playlistId, currentEntryId)` - wird aufgerufen, wenn Titel zu Ende geht

---

## `PlaylistEntryAccessResolver`
Datei: `VideoWebPlayer/Services/PlaylistEntryAccessResolver.cs`

### Öffentliche Methoden

| Methode | Zweck |
|---------|-------|
| `LoadAccessCheckDataAsync(userId, ct)` | Lädt in einem Durchgang alle Unlock-IDs und Media Source-IDs für einen Benutzer |
| `LoadUnlockHierarchyMappingsAsync(idsByType, ct)` | Lädt die ID-Mappings für Hierarchieauflösung (Film → Sammlung, Episode → Show) |
| `LoadMediaSourceMappingsAsync(idsByType, ct)` | Lädt die `MediaSourceId` für alle Einträge |
| `ResolveUnlockMediaId(entry, hierarchyMappings)` | Bestimmt die ID zur Unlock-Prüfung (direkt oder via Hierarchie) |
| `ResolveMediaSourceId(entry, mediaSourceMappings)` | Ruft die `MediaSourceId` eines Eintrags auf |
| `ResolveAccessibilityAsync(entries, userId, idsByType, ct)` | **Zentral für Wiedergabe**: Bulk-Auflösung der Zugriffsrechte für mehrere Einträge |
| `CheckEntryAccessible(entry, unlockedId, sourceId, accessData)` | Prüft einzelnen Eintrag: Regel ist `hasSourceAccess OR isUnlocked` |

**Wichtig:** Alle Lookups sind bulk-optimiert (eine Datenbankabfrage pro Medientyp, nicht N+1), damit Freischaltungsprüfung für 100e von Playlist-Einträgen schnell ist.

---

## `IUnlockedMediaService` / `UnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

### Öffentliche Methoden (Auszug für Wiedergabe)

| Methode | Zweck |
|---------|-------|
| `GetUnlockedMovieCollectionIdsForUserAsync(userId, ct)` | Lädt alle freigeschalteten Filmsammlungen für einen Benutzer |
| `GetUnlockedTVShowIdsForUserAsync(userId, ct)` | Lädt alle freigeschalteten Serien für einen Benutzer |
| `GetMediaSourceIdsForUserAsync(userId, ct)` | Lädt Media-Source-IDs, zu denen der Benutzer regulären Zugriff hat |
| `IsAccessible(hasSourceAccess, isUnlocked)` | **Kanonische Zugriffsprüfung**: Gibt `true` zurück, wenn Benutzer regulären Zugriff ODER Unlock hat |

---

## `MediaHierarchyRegistry`
Datei: `VideoWebPlayer/Services/MediaHierarchyRegistry.cs`

### Zweck
Zentrales Mediotyp-Registry mit bulk-optimierten Operationen für jeden unterstützten Medientyp:

| Medientyp | Ist direkt abspielbar | Ist Direct Unlock Target | Unlock ID Space | Kaskadenauflösung |
|-----------|----------------------|--------------------------|-----------------|------------------|
| `Movie` | ❌ (braucht Sammlung) | ❌ | MovieCollection | Keine |
| `TVShowEpisode` | ✅ | ❌ | TVShow | Keine |
| `TVShowSeason` | ❌ | ❌ | TVShow | → Episoden |
| `TVShow` | ✅ (theoretisch, aber wird bei Wiedergabe übersprungen) | ✅ | TVShow | → Staffeln + Episoden |
| `MovieCollection` | ✅ (theoretisch, aber wird bei Wiedergabe übersprungen) | ✅ | MovieCollection | → Filme |

### Relevante statische Daten

- `Handlers[MediaType]` - Dictionary mit `MediaTypeHandler` für jeden Medientyp
  - Jeder Handler hat: `LoadTitlesAsync`, `LoadExistingIdsAsync`, `LoadCascadeChildrenAsync`, `LoadReleaseDateAsync`, `GetHierarchySequenceAsync`, `LoadPictureIdsAsync`
  - Eigenschaften: `IsDirectUnlockTarget`, `UnlockIdSpace`

### Wichtig für Wiedergabe

- **Abspielbare Einträge:** Nur `TVShowEpisode` und `Movie` sind direkt abspielbar
- **Sammel-Einträge in Playlists:** Laut Anforderung werden TVShow, TVShowSeason und MovieCollection beim Hinzufügen (Schritt 2) bereits zu Einzeltiteln aufgelöst, daher sollten sie nicht als direkte Playlist-Einträge vorkommen
- **Fallback-Sortierung:** Release Date → Hierarchie/Episodennummer → Aufnahmezeitpunkt

---

## `VideoPlayer.razor`
Datei: `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`

### Existierende Funktionalität

| Parameter / Eigenschaft | Zweck |
|------------------------|--------|
| `Show` | Sichtbarkeit des Players |
| `StreamUrl` | URL zum Video-Stream |
| `MediaType` | Medientyp (z.B. "movie", "episode") |
| `MediaId` | ID des Mediums |
| `StartPositionSeconds` | Startposition (z.B. für "Fortsetzen") |
| `AutoPlay` | Autoplay bei Laden (true) |
| `CurrentPositionSeconds` | Aktuelle Wiedergabeposition (öffentliche Eigenschaft) |
| `PositionChanged` | Event beim Wechsel der Wiedergabeposition |
| `OnClose` | Callback zum Schließen des Players |

### JavaScript-Integration
- `continueWatching.attach/detach` - Speichert/lädt Wiedergabeprogress
- `videoPlayer.registerTimeUpdate/unregisterTimeUpdate` - Meldet Positionswechsel an .NET
- `videoPlayer.setStartPosition` - Setzt Startposition
- `videoPlayer.getCurrentPosition` - Fragt aktuelle Position ab

**Noch nicht implementiert für diese Anforderung:**
- Playlist-Badge-Anzeige (z.B. "[Meine Favoriten: 3/12]")
- Nächster/Vorheriger Button für Playlist-Navigation
- Behandlung von Media-End-Events für automatisches Weiterschalten

---

## PlaylistsController
Datei: `VideoWebPlayer/Controllers/PlaylistsController.cs`

### Bestehende Endpunkte (relevant für Wiedergabe)

| Endpunkt | Methode | Zweck |
|----------|---------|-------|
| `GET /api/playlists/{id}` | `GetPlaylist(id)` | Ruft Playlist-Metadaten ab |
| `GET /api/playlists/{id}/entries` | `GetPlaylistEntries(id)` | Ruft alle Einträge ab |
| `GET /api/playlists/{id}/entries/paged` | `GetPlaylistEntriesPaged(id, page, size)` | Ruft paginierte Einträge ab (mit Sortierung) |

**Noch nicht implementiert für diese Anforderung:**
- `POST /api/playlists/{id}/play?entryId={entryId}` - Startet Wiedergabe einer Playlist ab einem Eintrag
- `POST /api/playlists/{id}/play/next` - Navigiert zum nächsten Titel
- `POST /api/playlists/{id}/play/previous` - Navigiert zum vorherigen Titel

