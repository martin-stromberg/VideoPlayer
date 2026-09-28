# Enums

## `RecentEntryType`
**Datei:** `VideoWebPlayer/Data/RecentEntry.cs`

Beschreibt die Typen von kürzlich zugegriffen Einträgen.

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Filmeintrag |
| `MovieCollection` | Filmsammlung-Eintrag |
| `TVShow` | Serienein­trag |
| `TVShowSeason` | Staffel-Eintrag |
| `TVShowEpisode` | Episoden-Eintrag |

**Status:** Vorhanden, ähnliches Pattern sollte für `PlaylistItemType` verwendet werden.

---

## `SkipResult`
**Datei:** `VideoWebPlayer/Services/ContinueWatchingService.cs`

Ergebnis-Enum für manuelle Überssprung-Operationen bei Weiterschauen-Einträgen.

| Wert | Bedeutung |
|------|-----------|
| `NotFound` | Der angeforderte Eintrag existiert nicht für den Benutzer |
| `Replaced` | Der Eintrag wurde durch das nächste Media-Item ersetzt |
| `RemovedWithoutNext` | Der Eintrag wurde entfernt, da kein Folgeelement existiert |

**Status:** Vorhanden, relevant für Playlist-Integration in Weiterschauen-Logik.

---

## `ApiTokenScope`
**Datei:** `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`

Gültigkeitsbereich für API-Token.

**Status:** Vorhanden für API-Autorisierung, relevant für Playlist-API-Endpoints.

---

## Zu erstellende Enums

### `PlaylistSortMode`
Sollte erstellt werden für Playlist-Sortieroptionen:

| Wert | Bedeutung |
|------|-----------|
| `Automatic` | Sortierung nach Erscheinungsdatum, nicht manuell änderbar |
| `Manual` | Manuelle Sortierung mit Drag & Drop |

**Empfohlener Standort:** `VideoWebPlayer/Data/Playlists/PlaylistSortMode.cs` oder `VideoWebPlayer/Enums/PlaylistSortMode.cs`

---

### `PlaylistItemType`
Sollte erstellt werden für Playlist-Item-Typen:

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Filmeintrag |
| `TVShow` | Serienein­trag |
| `TVShowSeason` | Staffel-Eintrag |
| `TVShowEpisode` | Episoden-Eintrag |
| `MovieCollection` | Filmsammlung-Eintrag |

**Empfohlener Standort:** `VideoWebPlayer/Data/Playlists/PlaylistItemType.cs` oder `VideoWebPlayer/Enums/PlaylistItemType.cs`
