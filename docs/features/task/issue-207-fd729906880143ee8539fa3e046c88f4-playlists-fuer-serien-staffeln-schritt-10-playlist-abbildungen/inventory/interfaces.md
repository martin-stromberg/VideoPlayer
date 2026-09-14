# Interfaces – Bestandsaufnahme

## `IPlaylistService`
Datei: (Wird nach Suche in PlaylistService definiert)

**Zweck:** Service-Interface für Playlist-Operationen

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | `userId`, `genreId?`, `cancellationToken` | `Task<DtoPlaylist[]>` | Lädt Playlists des Nutzers |
| `GetPlaylistAsync` | `playlistId`, `userId`, `cancellationToken` | `Task<DtoPlaylist?>` | Lädt einzelne Playlist |
| `CreatePlaylistAsync` | `userId`, `name`, `description?`, `sortMode?`, `cancellationToken` | `Task<DtoPlaylist>` | Erstellt Playlist |
| `UpdatePlaylistAsync` | `playlistId`, `userId`, `name`, `description?`, `sortMode?`, `cancellationToken` | `Task<DtoPlaylist>` | Aktualisiert Playlist |
| `DeletePlaylistAsync` | `playlistId`, `userId`, `cancellationToken` | `Task` | Löscht Playlist |
| `AddMediaToPlaylistAsync` | `playlistId`, `userId`, `mediaType`, `mediaId`, `cancellationToken` | `Task<DtoPlaylistAddResult>` | Fügt Media hinzu |
| `RemoveMediaFromPlaylistAsync` | `playlistId`, `userId`, `mediaType`, `mediaId`, `confirmContinueWatchingRemoval`, `cancellationToken` | `Task` | Entfernt Media |
| `GetPlaylistEntriesAsync` | `playlistId`, `userId`, `cancellationToken` | `Task<PlaylistEntryInfo[]>` | Lädt Einträge |
| `GetPlaylistEntriesPagedAsync` | `playlistId`, `userId`, `pageNumber`, `pageSize`, `cancellationToken` | `Task<PlaylistEntryPage>` | Lädt paginierte Einträge |
| `ChangeSortModeAsync` | `playlistId`, `userId`, `newSortMode`, `confirmLossOfManualOrder`, `cancellationToken` | `Task<DtoPlaylist>` | Ändert Sortiermodus |
| `ReorderPlaylistEntryAsync` | `playlistId`, `userId`, `entryId`, `newSortOrder`, `cancellationToken` | `Task` | Ändert Sortierreihenfolge |
| `BatchReorderPlaylistEntriesAsync` | `playlistId`, `userId`, `operations`, `cancellationToken` | `Task<PlaylistEntryInfo[]>` | Batch-Umordnung |
| `SetPlaylistGenresAsync` | `playlistId`, `userId`, `genreIds`, `cancellationToken` | `Task<DtoPlaylist>` | Setzt Genres manuell |
| `ResetPlaylistGenresAsync` | `playlistId`, `userId`, `cancellationToken` | `Task<DtoPlaylist>` | Setzt Genres zurück |
| `StartPlaylistAsync` | `playlistId`, `userId`, `entryId?`, `cancellationToken` | `Task<DtoPlaylistPlaybackStart>` | Startet Wiedergabe |
| `GetNextPlaylistEntryAsync` | `playlistId`, `userId`, `currentEntryId`, `cancellationToken` | `Task<PlaylistEntryInfo?>` | Nächster Eintrag |
| `GetPreviousPlaylistEntryAsync` | `playlistId`, `userId`, `currentEntryId`, `cancellationToken` | `Task<PlaylistEntryInfo?>` | Vorheriger Eintrag |
| `AdvancePlaylistAsync` | `playlistId`, `userId`, `currentEntryId`, `cancellationToken` | `Task<PlaylistEntryInfo?>` | Auto-Advance |

**Bemerkungen zu Schritt 10:**
- Interface hat noch KEINE Methoden für Cover-Verwaltung
- Sollten hinzugefügt werden oder in separates Interface ausgelagert werden

## `IUnlockedMediaService`
Datei: (Wird von PlaylistService verwendet)

**Zweck:** Service für Zugriffs-/Freischaltungs-Validierung von Media-Einträgen

**Verwendung in PlaylistService:** `PlaylistEntryAccessResolver` prüft mittels dieses Service, ob ein Nutzer auf Einträge zugreifen darf

