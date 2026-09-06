# Logikklassen / Services

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, cancellationToken)` | `public` | Liefert alle Playlists eines Benutzers |
| `GetPlaylistAsync(playlistId, userId, cancellationToken)` | `public` | Liefert eine einzelne Playlist mit Berechtigungsprüfung |
| `CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken)` | `public` | Erstellt eine neue Playlist |
| `UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken)` | `public` | Aktualisiert eine existierende Playlist |
| `DeletePlaylistAsync(playlistId, userId, cancellationToken)` | `public` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)` | `public` | Fügt Medieninhalt (mit Kaskade) zu einer Playlist hinzu |
| `RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)` | `public` | Entfernt Medieninhalt aus einer Playlist |
| `GetPlaylistEntriesAsync(playlistId, userId, cancellationToken)` | `public` | Liefert alle Einträge einer Playlist (lädt Titel, entfernt verwaiste Einträge) |

### Private Methoden (Hilfsfunktionen)

| Methode | Beschreibung |
|---------|-------------|
| `GetOwnedPlaylistAsync(playlistId, userId, cancellationToken)` | Prüft Berechtigung und liefert Playlist oder wirft `PlaylistAccessDeniedException` |
| `ValidateName(name)` | Validiert und trimmt Playlist-Namen |
| `ValidateDescription(description)` | Validiert und trimmt Beschreibungen |
| `EnsureNameNotDuplicateAsync(userId, name, excludePlaylistId, cancellationToken)` | Prüft auf doppelte Namen innerhalb eines Benutzers |
| `ParseSortMode(sortMode, fallback)` | Parsst und validiert einen Sortiermodus-String |
| `ParseMediaType(mediaType)` | Parsst und validiert einen Medientyp-String |
| `TryParseKnownMediaType(mediaType, out parsed)` | Versucht, einen Medientyp zu parsen und prüft auf Unterstützung |
| `BuildEntriesToAddAsync(playlistId, parsedMediaType, normalizedMediaType, mediaId, cancellationToken)` | Erstellt Liste von zu hinzufügenden `PlaylistEntry`-Einträgen mit Kaskade-Logik |
| `BuildAddResultAsync(entriesToAdd, skippedDuplicateCount, topLevelEntry, mediaTitle, cancellationToken)` | Baut das DTO-Ergebnis für eine Add-Operation auf |
| `GetCascadeMediaIdsAsync(mediaType, mediaId, cancellationToken)` | Liefert alle Kaskaden-Kinder (z. B. Episoden einer Serie) |
| `GetMediaTitleAsync(mediaType, mediaId, cancellationToken)` | Liefert den Titel eines Medieneintrags |
| `GetMediaTitlesAsync(mediaType, mediaIds, cancellationToken)` | Liefert Titel für mehrere Medieneinträge eines Typs |
| `ToDto(playlist)` | Konvertiert `Playlist`-Entity zu `DtoPlaylist` |
| `ToDto(playlistEntry, mediaTitle, parentMediaTitle)` | Konvertiert `PlaylistEntry`-Entity zu `DtoPlaylistEntry` mit Titeln |

### Innere Klasse `MediaTypeHandler`

Struktur:
```csharp
private sealed class MediaTypeHandler
{
    public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, string>>> LoadTitlesAsync { get; init; }
    public Func<ApplicationDbContext, long, CancellationToken, Task<List<(MediaType MediaType, long MediaId)>>>? LoadCascadeChildrenAsync { get; init; }
}
```

**Zweck:** Abstraktion pro Medientyp für Titel-Laden und optionale Kaskaden-Logik.

**Handler pro Medientyp:**

| Medientyp | `LoadTitlesAsync` | `LoadCascadeChildrenAsync` |
|-----------|------|------|
| `Movie` | Lädt aus `Movies` Tabelle | Nicht vorhanden (null) |
| `TVShowEpisode` | Lädt aus `TVShowEpisodes` Tabelle | Nicht vorhanden |
| `TVShowSeason` | Lädt aus `TVShowSeasons` Tabelle | Lädt alle Episoden der Staffel |
| `TVShow` | Lädt aus `TVShows` Tabelle | Lädt alle Staffeln und dann alle Episoden |
| `MovieCollection` | Lädt aus `MovieCollections` Tabelle | Lädt alle Filme der Sammlung |

### Abhängigkeiten und Integrationen

- **Abhängigkeiten:** `ApplicationDbContext`, `EventManager`, `PlaylistSettings` (IOptions)
- **Events:** Publiziert `PlaylistCreatedEvent`, `PlaylistUpdatedEvent`, `PlaylistDeletedEvent`
- **Exceptions:** Wirft `PlaylistAccessDeniedException`, `KeyNotFoundException`, `InvalidOperationException`

### Wichtige Hinweise zu Schritt 3

**NICHT vorhanden:** 
- Methode `GetPlaylistEntriesPagedAsync()` – **muss neu implementiert werden** für Schritt 3
- Methoden `LoadReleaseDateAsync()` oder `GetHierarchySequenceAsync()` in `MediaTypeHandler` – **müssen neu hinzugefügt werden**
- Sortierlogik nach Erscheinungsdatum – **muss neu implementiert werden**
- Paginierungslogik – **muss neu implementiert werden**

**VORHANDEN:**
- Basis-Infrastruktur: `MediaTypeHandler` ermöglicht einfache Erweiterung pro Medientyp
- Kaskaden-Logik zum Laden von Kinder-Medien
- Berechtigungsprüfungen und Orphan-Bereinigung
- Titel-Laden und Konvertierung zu DTOs

