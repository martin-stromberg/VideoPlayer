# Datenmodelle

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | long | Eindeutige ID der Playlist |
| UserId | string | Foreign Key zum Benutzer (Required) |
| Name | string | Name der Playlist (Required, maximal 255 Zeichen) |
| Description | string? | Optionale Beschreibung (maximal 2000 Zeichen) |
| SortMode | PlaylistSortMode | Sortiermodus der Playlist (Enum: `ByReleaseDate` oder `Manual`), Standard: `ByReleaseDate` |
| CreatedAt | DateTime | Erstellungszeitstempel (Required) |
| UpdatedAt | DateTime | Aktualisierungszeitstempel (Required) |
| PlaylistEntries | ICollection<PlaylistEntry> | Navigation Property zur Sammlung von Playlist-Einträgen |

**Status für Schritt 4:** `SortMode` existiert bereits; keine zusätzlichen Eigenschaften erforderlich.

## `PlaylistEntry`
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | long | Eindeutige ID des Eintrags |
| PlaylistId | long | Foreign Key zur Playlist (Required) |
| Playlist | Playlist | Navigation Property zur Playlist |
| MediaType | string | Medientyp (Required, z. B. "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection") |
| MediaId | long | ID des Medieninhalts |
| ParentMediaType | string? | Medientyp der übergeordneten Sammlung (Optional) |
| ParentMediaId | long? | ID der übergeordneten Sammlung (Optional) |
| AddedAt | DateTime | Zeitstempel beim Hinzufügen (Required) |

**Status für Schritt 4:** Eigenschaft `SortOrder` (long?, optional) **FEHLT** - muss durch Migration hinzugefügt werden. Diese Eigenschaft wird für die manuelle Sortierung benötigt.

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| ByReleaseDate | Automatische Sortierung nach Erscheinungsdatum |
| Manual | Manuelle Sortierung durch den Benutzer |

**Status für Schritt 4:** Enum existiert bereits vollständig.
