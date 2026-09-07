# Interfaces

## `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GetPlaylistsAsync | userId: string, cancellationToken | Task<DtoPlaylist[]> | Abrufen aller Playlists für einen Benutzer |
| GetPlaylistAsync | playlistId: long, userId: string, cancellationToken | Task<DtoPlaylist?> | Abrufen einer einzelnen Playlist |
| CreatePlaylistAsync | userId: string, name: string, description: string?, sortMode: string?, cancellationToken | Task<DtoPlaylist> | Erstellen einer neuen Playlist mit optionalem Sortiermodus |
| UpdatePlaylistAsync | playlistId: long, userId: string, name: string, description: string?, sortMode: string?, cancellationToken | Task<DtoPlaylist> | Aktualisieren einer Playlist inkl. Sortiermodus-Änderung |
| DeletePlaylistAsync | playlistId: long, userId: string, cancellationToken | Task | Löschen einer Playlist |
| AddMediaToPlaylistAsync | playlistId: long, userId: string, mediaType: string, mediaId: long, cancellationToken | Task<DtoPlaylistAddResult> | Hinzufügen von Medieninhalt zu einer Playlist |
| RemoveMediaFromPlaylistAsync | playlistId: long, userId: string, mediaType: string, mediaId: long, cancellationToken | Task | Entfernen von Medieninhalt aus einer Playlist |
| GetPlaylistEntriesAsync | playlistId: long, userId: string, cancellationToken | Task<DtoPlaylistEntry[]> | Abrufen aller Einträge einer Playlist |
| GetPlaylistEntriesPagedAsync | playlistId: long, userId: string, pageNumber: int, pageSize: int, cancellationToken | Task<DtoPlaylistEntriesPagedResult> | Abrufen einer paginierten Seite von Einträgen |

**Status für Schritt 4:** Neue Methoden **FEHLEN**:
- `ReorderPlaylistEntryAsync(playlistId: long, userId: string, entryId: long, newSortOrder: long, cancellationToken)` → Task (oder Task<DtoPlaylistEntry>)
- `BatchReorderPlaylistEntriesAsync(playlistId: long, userId: string, reorderOperations: List<(long EntryId, long NewSortOrder)>, cancellationToken)` → Task<DtoPlaylistEntry[]>
- `ChangeSortModeAsync(playlistId: long, userId: string, newSortMode: string, confirmLossOfManualOrder?: bool, cancellationToken)` → Task<DtoPlaylist> (mit möglicher 409 Conflict Response bei fehlender Bestätigung)
