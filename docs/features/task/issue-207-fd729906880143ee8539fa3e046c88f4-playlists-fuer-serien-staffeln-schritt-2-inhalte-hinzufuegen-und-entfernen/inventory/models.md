# Datenmodellklassen

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel, Playlist-Identifier |
| `UserId` | `string` | Fremdschlüssel zu `ApplicationUser`, Besitzer der Playlist |
| `Name` | `string` | Playlist-Name (erforderlich, max. 255 Zeichen) |
| `Description` | `string?` | Optionale Playlist-Beschreibung (max. 2000 Zeichen) |
| `SortMode` | `PlaylistSortMode` | Sortiermodus (Standard: `ByReleaseDate`) |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel (UTC) |
| `UpdatedAt` | `DateTime` | Letzter Aktualisierungszeitstempel (UTC) |

**Navigation:** `PlaylistEntries` ist nicht vorhanden und muss hinzugefügt werden.

**Anmerkungen:**
- Composite Unique Index auf `(UserId, Name)` existiert bereits
- Foreign Key zu `ApplicationUser` mit `OnDelete(DeleteBehavior.Cascade)` konfiguriert
- Keine `PlaylistEntries`-Navigation vorhanden (erforderlich für Schritt 2)

## `PlaylistSortMode` (Enum)
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` (0) | Automatische Sortierung nach Erscheinungsdatum |
| `Manual` (1) | Manuelle Sortierung durch den Anwender |

---

## Nicht vorhanden (zu erstellen):

### `PlaylistEntry` (neu)
- Muss mit den folgenden Eigenschaften erstellt werden:
  - `Id` (long, PrimaryKey)
  - `PlaylistId` (long, ForeignKey zu `Playlist`)
  - `Playlist` (Navigation zu `Playlist`)
  - `MediaType` (string oder Enum: `"Movie"`, `"TVShowEpisode"`, `"TVShowSeason"`, `"TVShow"`, `"MovieCollection"`)
  - `MediaId` (long, ID des Medieninhalts)
  - `ParentMediaType` (string oder Enum, nullable)
  - `ParentMediaId` (long?, nullable)
  - `AddedAt` (DateTime)
  - Composite Unique Constraint: `(PlaylistId, MediaType, MediaId)`
