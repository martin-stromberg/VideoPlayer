# Datenmodelle und DTOs

## `Playlist` (Entity)
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel der Playlist |
| `UserId` | `string` | Foreign Key zum Besitzer (`ApplicationUser`) |
| `Name` | `string` | Playlist-Name (erforderlich, max. 255 Zeichen) |
| `Description` | `string?` | Optionale Beschreibung (max. 2000 Zeichen) |
| `SortMode` | `PlaylistSortMode` | Sortiermodus: `ByReleaseDate` oder `Manual` (Standard: `ByReleaseDate`) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel (UTC) |
| `UpdatedAt` | `DateTime` | Aktualisierungszeitstempel (UTC) |

## `DtoPlaylist`
Datei: `VideoWebPlayer.Client.Models/DtoPlaylist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Playlist-ID |
| `Name` | `string` | Playlist-Name |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string` | Sortiermodus als String (`ByReleaseDate` oder `Manual`) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `UpdatedAt` | `DateTime` | Aktualisierungszeitstempel |

**Verwendung:** DTO für Übertragung zwischen Client und Server; wird von `PlaylistService.ToDto()` konvertiert.

## `DtoCreatePlaylistRequest`
Datei: `VideoWebPlayer.Client.Models/DtoCreatePlaylistRequest.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Name der neuen Playlist (erforderlich) |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string?` | Sortiermodus (Standard: `PlaylistSortModeValues.ByReleaseDate`) |

**Verwendung:** Request-Body für POST `/api/playlists`.

## `DtoUpdatePlaylistRequest`
Datei: `VideoWebPlayer.Client.Models/DtoUpdatePlaylistRequest.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Neuer Name der Playlist (erforderlich) |
| `Description` | `string?` | Neue Beschreibung |
| `SortMode` | `string?` | Neuer Sortiermodus (optional, nicht ändern wenn null) |

**Verwendung:** Request-Body für PUT `/api/playlists/{id}`.
