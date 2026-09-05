# Enums

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Numerischer Wert | Bedeutung |
|------|-----------------|-----------|
| `ByReleaseDate` | 0 | Automatische Sortierung nach Erscheinungsdatum |
| `Manual` | 1 | Manuelle Sortierung durch den Anwender |

**Zweck:** Bestimmt die Sortierweise von Inhalten in einer Playlist

**Hinweis:** Der String-Wert `"ByReleaseDate"` wird durch die Konstante `PlaylistSortModeValues.ByReleaseDate` definiert und auf Client-Seite verwendet.

---

## Nicht vorhanden (zu erstellen):

### `MediaType` (Enum oder String-basiert)
**Benötigte Werte für Schritt 2:**
- `"Movie"` — Einzelner Film
- `"TVShowEpisode"` — Einzelne TV-Episode
- `"TVShowSeason"` — TV-Staffel
- `"TVShow"` — Ganze TV-Serie
- `"MovieCollection"` — Filmsammlung

**Entscheidung erforderlich:** Ob als Enum in der Datenmodellklasse oder als String-Enum in der Service-Logik implementiert werden soll.

