# DTOs (Client-Modelle)

## Bestehende DTOs

### `DtoPlaylist`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylist.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Playlist-Identifier |
| `Name` | `string` | Playlist-Name |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string` | Sortiermodus als String (z. B. `"ByReleaseDate"`, `"Manual"`) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `UpdatedAt` | `DateTime` | Letzter Aktualisierungszeitstempel |

---

### `DtoCreatePlaylistRequest`
Datei: `VideoWebPlayer.Client/Models/DtoCreatePlaylistRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Name` | `string` | Playlist-Name (erforderlich) |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string?` | Sortiermodus (optional, Standard: `"ByReleaseDate"`) |

---

### `DtoUpdatePlaylistRequest`
Datei: `VideoWebPlayer.Client/Models/DtoUpdatePlaylistRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Name` | `string` | Playlist-Name (erforderlich) |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string?` | Sortiermodus (optional) |

---

### `PlaylistSortModeValues` (Konstanten)
Datei: `VideoWebPlayer.Client/Models/PlaylistSortModeValues.cs`

| Konstante | Wert | Bedeutung |
|-----------|------|-----------|
| `ByReleaseDate` | `"ByReleaseDate"` | Sortierung nach Erscheinungsdatum |
| `Manual` | `"Manual"` | Manuelle Sortierung |

**Zweck:** Zentrale String-Konstanten für Sortiermodi zur Vermeidung von Duplikaten über DTOs und Razor-Komponenten

---

## Nicht vorhanden (zu erstellen):

### `DtoPlaylistEntry` (neu)
Benötigte Eigenschaften:
- `Id` (long)
- `PlaylistId` (long)
- `MediaType` (string)
- `MediaId` (long)
- `MediaTitle` (string, für UI-Anzeige)
- `ParentMediaType` (string?, nullable)
- `ParentMediaId` (long?, nullable)
- `ParentMediaTitle` (string?, nullable)
- `AddedAt` (DateTime)

---

### `DtoAddMediaToPlaylistRequest` (neu)
Benötigte Eigenschaften:
- `mediaType` (string, erforderlich)
- `mediaId` (long, erforderlich)

---

### `DtoRemoveMediaFromPlaylistRequest` (neu, optional)
Benötigte Eigenschaften:
- `mediaType` (string, erforderlich)
- `mediaId` (long, erforderlich)

