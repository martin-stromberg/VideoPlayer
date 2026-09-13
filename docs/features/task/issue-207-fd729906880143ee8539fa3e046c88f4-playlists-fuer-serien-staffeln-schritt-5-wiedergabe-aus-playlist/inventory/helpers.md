# Hilfsmechanismen und Utilities

## `PlaylistEntryMediaTypeResolver`
**Datei:** `VideoWebPlayer.Client/Models/PlaylistEntryMediaTypeResolver.cs`

Zentrale Utility-Klasse für Media-Type-Umwandlung und Validierung:

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ToPlayerMediaType` | entryMediaType: string | "episode" oder "movie" | Konvertiert PlaylistEntry MediaType in VideoPlayer-Format |
| `ResolveItemStreamType` | entryMediaType: string | "tvshowepisode" oder "movie" | Konvertiert zu API-Pfad-Segment für Stream-Endpoints |
| `IsPlayable` | mediaType: string | bool | Prüft, ob Media-Type direkt abspielbar ist |

**Status für Anforderung:**
- `IsPlayable(string mediaType)` ist korrekt implementiert:
  - Gibt `true` nur für "Movie" und "TVShowEpisode" zurück
  - Wird verwendet in `PlaylistService.ResolveFirstPlayableEntry` (korrekt)
  - Wird verwendet in `PlaylistService.FindAdjacentPlayableEntryAsync` (korrekt)
  - Wird verwendet in `PlaylistEntriesList.IsPlayableEntry` (aber unvollständig - prüft nicht IsAccessible)
  - **FEHLEND:** Wird NICHT verwendet in `PlaylistService.ResolveExplicitStartEntry` (das ist das Problem)

---

## Exception-Typen

### `PlaylistAccessDeniedException`
- Wird geworfen von `PlaylistService.ResolveExplicitStartEntry` wenn Benutzer keinen Zugriff hat
- Wird geworfen von `PlaylistService.StartPlaylistAsync` wenn Playlist nicht gehört
- **Status:** Korrekt für Zugriffsprüfung, aber würde auch für nicht-abspielbare Einträge geworfen (falsch - sollte InvalidOperationException sein)

### `InvalidOperationException`
- Wird geworfen von `PlaylistService.ResolveFirstPlayableEntry` wenn keine abspielbaren Einträge vorhanden sind
- Wird geworfen von `PlaylistService.StartPlaylistAsync` bei internen Problemen
- **Status:** Sollte auch von `ResolveExplicitStartEntry` geworfen werden, wenn der Eintrag nicht abspielbar ist (aber wird nicht)

### `KeyNotFoundException`
- Wird geworfen von `PlaylistService.ResolveExplicitStartEntry` wenn Eintrag nicht zur Playlist gehört
- **Status:** Korrekt
