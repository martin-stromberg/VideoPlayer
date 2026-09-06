# Enums

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum (Schritt 3 - diese Anforderung) |
| `Manual` | Manuelle Sortierung durch den Anwender (zukünftig in Schritt 4) |

**Verwendung:** 
- Wird in der `Playlist` Entity als `SortMode` Eigenschaft gespeichert
- Default-Wert: `PlaylistSortMode.ByReleaseDate`
- Wird beim Erstellen und Aktualisieren von Playlists verwendet

**Für Schritt 3 relevant:**
- `ByReleaseDate` ist der Sortiermodus, für den die neue Sortierlogik implementiert werden muss
- Die neue Methode `GetPlaylistEntriesPagedAsync()` muss prüfen, ob `Playlist.SortMode == PlaylistSortMode.ByReleaseDate` und entsprechend sortieren

---

## `MediaType` (Enum)
Datei: Nicht direkt in der Anforderung erwähnt, aber in `PlaylistService` verwendet

**Verwendung im Code:** Der `PlaylistService` nutzt einen internen `MediaType` Enum für die Verwaltung von Medientypen und deren Handler.

**Mappings zu Medientyp-Strings:**
- `Movie` → `MediaTypeValues.Movie`
- `TVShow` → `MediaTypeValues.TVShow`
- `TVShowSeason` → `MediaTypeValues.TVShowSeason`
- `TVShowEpisode` → `MediaTypeValues.TVShowEpisode`
- `MovieCollection` → `MediaTypeValues.MovieCollection`

Diese werden zu Strings normalisiert und in der Datenbank als `string` in `PlaylistEntry.MediaType` gespeichert.
