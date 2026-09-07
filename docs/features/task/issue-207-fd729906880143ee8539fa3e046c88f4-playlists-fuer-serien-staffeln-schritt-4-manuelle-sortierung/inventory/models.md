# Datenmodelle

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Eindeutige Playlist-ID |
| `UserId` | string | Benutzer-ID des Besitzers (Fremdschlüssel zu ApplicationUser) |
| `Name` | string | Playlist-Name (max. 255 Zeichen) |
| `Description` | string? | Optionale Playlist-Beschreibung |
| `SortMode` | PlaylistSortMode | Sortiermodus: `ByReleaseDate` oder `Manual` |
| `CreatedAt` | DateTime | Erstellungszeitstempel |
| `UpdatedAt` | DateTime | Aktualisierungszeitstempel |
| `PlaylistEntries` | ICollection<PlaylistEntry> | Einträge dieser Playlist |

## `PlaylistEntry`
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Eindeutige Eintrag-ID |
| `PlaylistId` | long | Playlist-ID (Fremdschlüssel) |
| `Playlist` | Playlist | Navigations-Referenz zur Playlist |
| `MediaType` | string | Medientyp (Film, Serie, Staffel, Episode, Filmsammlung) |
| `MediaId` | long | Medien-ID des referenzierten Inhalts |
| `ParentMediaType` | string? | Typ der Sammlung, über die dieser Eintrag hinzugefügt wurde |
| `ParentMediaId` | long? | ID der Sammlung, über die dieser Eintrag hinzugefügt wurde |
| `AddedAt` | DateTime | Zeitstempel des Hinzufügens zur Playlist |
| `SortOrder` | long? | Manuelle Sortierreihenfolge (null für ByReleaseDate-Mode) |

**Wichtige Details:**
- `SortOrder` ist nullable und wird nur in `Manual`-Mode Playlists gesetzt
- Eindeutiger Index auf `(PlaylistId, MediaType, MediaId)` verhindert Duplikate
- Index auf `(PlaylistId, SortOrder)` für sortierte Abfragen im Manual-Mode
