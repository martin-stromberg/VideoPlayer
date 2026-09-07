# Interfaces und API-Clients

## `IPlaylistApiClient`
Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReorderPlaylistEntryAsync` | playlistId, entryId, request: DtoReorderPlaylistEntryRequest | Task | Ändert SortOrder eines Eintrags |
| `BatchReorderPlaylistEntriesAsync` | playlistId, request: DtoBatchReorderPlaylistEntriesRequest | Task<IEnumerable<DtoPlaylistEntry>> | Atomar mehrere Einträge reordern |
| `RequestMaxSortOrderAsync` | playlistId | Task<long?> | Gibt max. SortOrder zurück (für "An Ende") |
| `MoveEntryToBeginningAsync` | playlistId, entryId | Task<DtoPlaylistEntry> | Verschiebt Eintrag an Anfang |

## DTOs

### `DtoReorderPlaylistEntryRequest`
- `NewSortOrder`: long - neue Sortierreihenfolge

### `DtoBatchReorderPlaylistEntriesRequest`
- `Reorders`: List<(long EntryId, long NewSortOrder)> - Batch-Operationen

### `DtoPlaylistEntry`
- `Id`: long
- `MediaType`: string
- `MediaId`: long
- `MediaTitle`: string
- `ParentMediaTitle`: string?
- `AddedAt`: DateTime
- `SortOrder`: long? - Manuelle Sortierreihenfolge
- `IsAccessible`: bool - Freischaltungsstatus
- `ResolvedPictureId`: long? - Bild-ID

## Integrationspunkte

**PlaylistEntriesList.razor** nutzt:
- `PlaylistClient.AddMediaToPlaylistAsync(playlistId, request)`
- `PlaylistClient.RemoveMediaFromPlaylistAsync(playlistId, mediaType, mediaId)`
- `PlaylistClient.ReorderPlaylistEntryAsync(playlistId, entryId, request)` - wird in OnEntryDropAsync aufgerufen
- `PlaylistClient.RequestMaxSortOrderAsync(playlistId)` - wird in MoveEntryToEndAsync aufgerufen
- `PlaylistClient.MoveEntryToBeginningAsync(playlistId, entryId)` - wird in MoveEntryToBeginningAsync aufgerufen
- `PlaylistClient.RequestPlaylistEntriesPagedAsync(playlistId, pageNumber, pageSize, cancellationToken)`

**Abhängigkeitsrichtung:**
```
PlaylistEntriesList.razor (UI)
    └── IPlaylistApiClient (Dependency Injection)
        └── VideoWebPlayerClient (Implementation)
            └── PlaylistsController (API Endpoint)
                └── PlaylistService
                    └── PlaylistEntryReorderService (DB-Operationen)
```
