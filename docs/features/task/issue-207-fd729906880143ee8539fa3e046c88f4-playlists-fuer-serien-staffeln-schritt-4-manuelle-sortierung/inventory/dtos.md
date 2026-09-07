# Data Transfer Objects (DTOs)

## `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID des Eintrags |
| PlaylistId | long | ID der Playlist |
| MediaType | string | Medientyp (z. B. "Movie", "TVShow") |
| MediaId | long | ID des Medieninhalts |
| MediaTitle | string | Titel des Medieninhalts |
| ParentMediaType | string? | Medientyp der übergeordneten Sammlung |
| ParentMediaId | long? | ID der übergeordneten Sammlung |
| ParentMediaTitle | string? | Titel der übergeordneten Sammlung |
| AddedAt | DateTime | Zeitstempel beim Hinzufügen |
| ResolvedPictureId | long? | Bereits auf dem Server aufgelöste Bild-ID (Poster → Banner → Fanart) |
| IsAccessible | bool | Ob der aktuelle Benutzer Zugriff auf das Medium hat |

**Status für Schritt 4:** Eigenschaft `SortOrder` (long?) **FEHLT** - muss hinzugefügt werden, um die manuelle Sortierreihenfolge im UI anzuzeigen.

## `DtoPlaylist`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylist.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID der Playlist |
| Name | string | Name der Playlist |
| Description | string? | Optionale Beschreibung |
| SortMode | string | Sortiermodus als String (z. B. "ByReleaseDate", "Manual") |
| CreatedAt | DateTime | Erstellungszeitstempel |
| UpdatedAt | DateTime | Aktualisierungszeitstempel |

**Status für Schritt 4:** Bereits vollständig; `SortMode` existiert bereits.

## `DtoPlaylistEntriesPagedResult`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistEntriesPagedResult.cs`

Struktur: Enthält paginierte `DtoPlaylistEntry`-Array und Paginierungsmetadaten.

**Status für Schritt 4:** Keine Änderungen erforderlich.

## Neue DTOs für Schritt 4

### `DtoReorderPlaylistEntryRequest` (neu erforderlich)
Für Endpoint `PUT /api/playlists/{id}/entries/{entryId}/order`
```csharp
public class DtoReorderPlaylistEntryRequest
{
    public long NewSortOrder { get; set; }
}
```

### `DtoBatchReorderPlaylistEntriesRequest` (neu erforderlich)
Für Endpoint `POST /api/playlists/{id}/entries/batch-reorder`
```csharp
public class DtoBatchReorderPlaylistEntriesRequest
{
    public List<(long EntryId, long NewSortOrder)> ReorderOperations { get; set; }
    // oder als List<DtoReorderOperation> mit Nested-Klasse
}
```

### `DtoChangeSortModeRequest` (neu erforderlich)
Für Endpoint `PATCH /api/playlists/{id}/sort-mode`
```csharp
public class DtoChangeSortModeRequest
{
    public string NewSortMode { get; set; }
    public bool? ConfirmLossOfManualOrder { get; set; }
}
```

### `DtoChangeSortModeConflictResponse` (neu erforderlich)
Response bei HTTP 409 Conflict (wenn Bestätigung erforderlich):
```csharp
public class DtoChangeSortModeConflictResponse
{
    public bool IsLossOfDataConfirmationRequired { get; set; }
}
```
