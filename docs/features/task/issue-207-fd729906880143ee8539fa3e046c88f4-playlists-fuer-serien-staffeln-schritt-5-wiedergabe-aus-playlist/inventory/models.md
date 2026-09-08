# Datenmodelle - Playlist-Wiedergabe

## `Playlist`
Datei: `VideoWebPlayer/Data/Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel der Playlist |
| `UserId` | `string` | Fremdschlüssel zum Besitzer (`ApplicationUser`) |
| `Name` | `string` | Name der Playlist (erforderlich, max. 255 Zeichen) |
| `Description` | `string?` | Optionale Beschreibung (max. 2000 Zeichen) |
| `SortMode` | `PlaylistSortMode` | Sortiermodus (ByReleaseDate oder Manual) |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt (UTC) |
| `UpdatedAt` | `DateTime` | Letzte Änderung (UTC) |
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | Enthaltene Einträge |

**Noch nicht implementiert für diese Anforderung:**
- `CurrentEntryId` (nullable `long?`) - wird in Schritt 5 benötigt zur Verfolgung der aktuellen Wiedergabeposition

**EF Core-Konfiguration:**
- Eindeutiger Index auf `(UserId, Name)` - Playlist-Namen müssen pro Nutzer eindeutig sein
- Kaskadierende Löschung bei Benutzerlöschung
- `CreatedAt` und `UpdatedAt` sind erforderlich
- `SortMode` hat einen Standardwert von `PlaylistSortMode.ByReleaseDate`

---

## `PlaylistEntry`
Datei: `VideoWebPlayer/Data/PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel des Eintrags |
| `PlaylistId` | `long` | Fremdschlüssel zur `Playlist` |
| `Playlist` | `Playlist` | Navigationseigenschaft zur Playlist |
| `MediaType` | `string` | Normalisierter Medientyp (Movie, TVShowEpisode, TVShowSeason, TVShow, MovieCollection) |
| `MediaId` | `long` | ID des referenzierten Medieninhalts |
| `ParentMediaType` | `string?` | Medientyp der Sammlung, über die dieser Eintrag hinzugefügt wurde (z.B. TVShow, TVShowSeason, MovieCollection) oder `null` für Einträge auf oberster Ebene |
| `ParentMediaId` | `long?` | ID der Sammlung, über die dieser Eintrag hinzugefügt wurde, oder `null` |
| `AddedAt` | `DateTime` | Zeitpunkt, an dem der Eintrag zur Playlist hinzugefügt wurde |
| `SortOrder` | `long?` | Manuelle Sortierreihenfolge innerhalb der Playlist (nur relevant bei `PlaylistSortMode.Manual`) |

**Wichtige Merkmale für Wiedergabe:**
- Einträge können direkt sein (oberste Ebene) oder über eine Sammlung hinzugefügt sein (`ParentMediaType`/`ParentMediaId`)
- Einträge vom Typ TVShowSeason und TVShow sind **nicht direkt abspielbar** - siehe MediaHierarchyRegistry
- Nur Episoden (TVShowEpisode) und Filme (Movie) sind direkt abspielbar
- **Annahme für Schritt 5**: Beim Hinzufügen (Schritt 2) werden Serien und Staffeln bereits zu ihren Einzeltiteln aufgelöst, daher können nur abspielbare Einträge vorhanden sein

---

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum (mit Fallback-Kette: Release Date → Hierarchie/Episodennummer → Aufnahmezeitpunkt) |
| `Manual` | Manuelle Sortierung durch Benutzer via `PlaylistEntry.SortOrder` |

---

## DTOs (Client-seitig)

### `DtoPlaylist`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylist.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Playlist-ID |
| `Name` | `string` | Playlistname |
| `Description` | `string?` | Optionale Beschreibung |
| `SortMode` | `string` | Sortiermodus als String |
| `CreatedAt` | `DateTime` | Erstellungsdatum |
| `UpdatedAt` | `DateTime` | Letztes Änderungsdatum |

**Noch nicht implementiert:**
- `CurrentEntryId` für Schritt 5

### `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`

Enthält neben Daten aus `PlaylistEntry` auch aufgelöste Werte:
- `MediaTitle` - gelöster Titel des Mediums
- `ParentMediaTitle` - gelöster Titel der Sammlung
- `ResolvedPictureId` - aufgelöste Bild-ID (Poster-Fallback)
- `IsAccessible` - ob der Eintrag für den aktuellen Benutzer zugänglich ist (basierend auf Freischaltungsprüfung)

