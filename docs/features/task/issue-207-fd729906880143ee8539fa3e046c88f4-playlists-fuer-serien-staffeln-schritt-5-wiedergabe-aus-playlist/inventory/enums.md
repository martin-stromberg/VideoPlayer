# Enums - Playlist-Wiedergabe

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung | Sortierlogik |
|------|-----------|-------------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum | Release Date (nullable) → Hierarchie/Episodennummer → Aufnahmezeitpunkt (`AddedAt`) |
| `Manual` | Manuelle Sortierung durch Benutzer | `PlaylistEntry.SortOrder` (aufsteigend) → `AddedAt` (Fallback für null) |

### Verwendung in Wiedergabe
- Bestimmt die Reihenfolge, in der `GetNextPlaylistEntryAsync` und `GetPreviousPlaylistEntryAsync` Einträge durchlaufen
- Die Sortierung wird durch `PlaylistService.SortPlaylistEntriesForModeAsync` durchgeführt

---

## `MediaType`
Datei: `VideoWebPlayer/Data/MediaType.cs`

### Enum-Werte und Merkmalstabelle

| Wert | Abspielbar | Sammlung | Direct Unlock | Unlock ID Space | Kaskade |
|------|-----------|----------|--------------|-----------------|---------|
| `Movie` | ❌ | ❌ | ❌ | MovieCollection | Keine |
| `MovieCollection` | ❌ (Skip in Playback) | ✅ | ✅ | MovieCollection | → Filme |
| `TVShow` | ❌ (Skip in Playback) | ✅ | ✅ | TVShow | → Staffeln, Episoden |
| `TVShowSeason` | ❌ (Skip in Playback) | ✅ | ❌ | TVShow | → Episoden |
| `TVShowEpisode` | ✅ | ❌ | ❌ | TVShow | Keine |

### Für Wiedergabe Relevant
- **Abspielbare Einträge:** Nur `TVShowEpisode` ist direkt abspielbar (Filme sind über MovieCollection referenzierbar, aber nicht direkt)
- **Sammel-Einträge:** `TVShow`, `TVShowSeason`, `MovieCollection` sollten bei Playlist-Wiedergabe übersprungen werden (Annahme: werden beim Hinzufügen aufgelöst)
- **Hierarchie-Auflösung:** Für Unlock-Prüfung müssen `Movie` und `TVShowEpisode` zu ihrer Sammlung/Serie aufgelöst werden

---

## `UnlockIdSpace` (intern)
Datei: `VideoWebPlayer/Services/MediaHierarchyRegistry.cs`

| Wert | Bedeutung | Verwendung |
|------|-----------|-----------|
| `MovieCollection` | ID-Space für Filmsammlungen | Prüft gegen `UnlockedMediaEntry` für Movies/MovieCollections |
| `TVShow` | ID-Space für Serien | Prüft gegen `UnlockedMediaEntry` für TVShows/TVShowSeasons/TVShowEpisodes |

### Wichtig für Wiedergabe
- Beide ID-Spaces sind unabhängig (beide starten bei 1)
- Darf nicht vermischt werden (z.B. eine MovieCollection-ID niemals gegen TVShow-Unlocks prüfen)
- Wird von `PlaylistEntryAccessResolver.CheckEntryAccessible` verwendet

