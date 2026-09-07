# Logikklassen

## `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetPlaylistsAsync | public | Gibt alle Playlists für einen Benutzer zurück |
| GetPlaylistAsync | public | Gibt eine einzelne Playlist für einen Benutzer zurück |
| CreatePlaylistAsync | public | Erstellt eine neue Playlist mit optionalem Sortiermodus |
| UpdatePlaylistAsync | public | Aktualisiert eine bestehende Playlist inkl. Sortiermodus |
| DeletePlaylistAsync | public | Löscht eine Playlist |
| AddMediaToPlaylistAsync | public | Fügt einen Medieneintrag zu einer Playlist hinzu (mit Cascade-Logik) |
| RemoveMediaFromPlaylistAsync | public | Entfernt einen Medieneintrag aus einer Playlist |
| GetPlaylistEntriesAsync | public | Gibt alle Einträge einer Playlist zurück |
| GetPlaylistEntriesPagedAsync | public | Gibt eine sortierte, paginierte Seite von Einträgen zurück |

**Status für Schritt 4:** Folgende Methoden **FEHLEN**:
- `ReorderPlaylistEntryAsync(playlistId, userId, entryId, newSortOrder)` - ändert die `SortOrder` eines Eintrags im Manual-Mode
- `BatchReorderPlaylistEntriesAsync(playlistId, userId, reorderOperations)` - führt mehrere Reorder-Operationen atomar durch
- `ChangeSortModeAsync(playlistId, userId, newSortMode, confirmLossOfManualOrder?)` - wechselt zwischen Sortiermodi

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetPlaylistsAsync | public | Implementierung von `IPlaylistService.GetPlaylistsAsync` |
| GetPlaylistAsync | public | Implementierung von `IPlaylistService.GetPlaylistAsync` |
| CreatePlaylistAsync | public | Implementierung von `IPlaylistService.CreatePlaylistAsync` |
| UpdatePlaylistAsync | public | Implementierung von `IPlaylistService.UpdatePlaylistAsync` |
| DeletePlaylistAsync | public | Implementierung von `IPlaylistService.DeletePlaylistAsync` |
| AddMediaToPlaylistAsync | public | Implementierung von `IPlaylistService.AddMediaToPlaylistAsync` |
| RemoveMediaFromPlaylistAsync | public | Implementierung von `IPlaylistService.RemoveMediaFromPlaylistAsync` |
| GetPlaylistEntriesAsync | public | Implementierung von `IPlaylistService.GetPlaylistEntriesAsync` |
| GetPlaylistEntriesPagedAsync | public | Gibt paginierte Einträge zurück; sortiert je nach `Playlist.SortMode` |
| SortPlaylistEntriesByReleaseDateAsync | private | Sortiert Einträge nach Erscheinungsdatum mit Fallback-Kette |
| BuildEntryDtosAsync | private | Konvertiert `PlaylistEntry` in `DtoPlaylistEntry` mit Titeln, Bildern und Zugriffsprüfungen |
| LoadValidPlaylistEntriesAsync | private | Lädt Playlist-Einträge und entfernt verwaiste Einträge |

**Aktuelle Sortier-Logik in `GetPlaylistEntriesPagedAsync` (Zeile 830-857):**
- Für `SortMode == ByReleaseDate`: Aufrufe `SortPlaylistEntriesByReleaseDateAsync`, sortiert nach ReleaseDate → ParentId → SequenceNumber → AddedAt
- Für andere Modi (aktuell nur `Manual`): Sortierung nach `AddedAt`

**Status für Schritt 4:**
- `GetPlaylistEntriesPagedAsync` muss erweitert werden: Für `SortMode == Manual` nach `SortOrder` aufsteigend sortieren statt nach `AddedAt`
- Die Methoden `ReorderPlaylistEntryAsync`, `BatchReorderPlaylistEntriesAsync`, `ChangeSortModeAsync` **FEHLEN**
- `AddMediaToPlaylistAsync` muss erweitert werden: Bei `SortMode == Manual` neue Einträge mit `SortOrder = Max(existingOrders) + 1` versehen

## `PlaylistsController`
Datei: `VideoWebPlayer/Controllers/PlaylistsController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetPlaylists | public | GET /api/playlists - Alle Playlists für aktuellen Benutzer |
| GetPlaylist | public | GET /api/playlists/{id} - Eine einzelne Playlist |
| CreatePlaylist | public | POST /api/playlists - Erstellt eine neue Playlist |
| UpdatePlaylist | public | PUT /api/playlists/{id} - Aktualisiert eine Playlist |
| DeletePlaylist | public | DELETE /api/playlists/{id} - Löscht eine Playlist |
| AddMediaToPlaylist | public | POST /api/playlists/{id}/entries - Fügt Medieninhalt hinzu |
| RemoveMediaFromPlaylist | public | DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId} - Entfernt Medieninhalt |
| GetPlaylistEntries | public | GET /api/playlists/{id}/entries - Gibt alle Einträge zurück |
| GetPlaylistEntriesPaged | public | GET /api/playlists/{id}/entries/paged - Gibt paginierte Einträge zurück |

**Status für Schritt 4:** Folgende Endpoints **FEHLEN**:
- `PUT /api/playlists/{id}/entries/{entryId}/order` - Einzelnes Reordern
- `POST /api/playlists/{id}/entries/batch-reorder` - Batch-Reordern
- `PATCH /api/playlists/{id}/sort-mode` - Sortiermodus wechseln (mit Bestätigung)
