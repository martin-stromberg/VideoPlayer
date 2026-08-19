# Datenmodell – Löschabhängigkeiten

## Relevante Entitäten

| Entität | Besitzer-FK | Bemerkung |
|---------|------------|-----------|
| `MediaSource` | – | Wurzelelement |
| `MediaCollection` | `MediaSourceId` | Kann verschachtelte `ChildCollections` haben (via `ParentMediaCollectionId`) |
| `MediaItem` | `MediaCollectionId` | Inhalt der `MediaCollection` |
| `MovieMediaItem` | `MediaItemId`, `MovieId` | Verknüpfungstabelle |
| `TVShowEpisodeMediaItem` | `MediaItemId`, `TVShowEpisodeId` | Verknüpfungstabelle |
| `Movie` | `MediaSourceId`, `MovieCollectionId` | Zugehörig zur Quelle |
| `MovieCollection` | `MediaSourceId` | Zugehörig zur Quelle |
| `MovieGenre` | `MovieId`, `GenreId` | Verknüpfungstabelle |
| `TVShow` | `MediaSourceId` | Zugehörig zur Quelle |
| `TVShowSeason` | `TVShowId` | Zugehörig zur Show |
| `TVShowEpisode` | `TVShowSeasonId` | Zugehörig zur Staffel |
| `TVShowGenre` | `TVShowId`, `GenreId` | Verknüpfungstabelle |

## Löschreihenfolge

1. Verknüpfungen lösen, die auf `MediaItem` verweisen:
   - `TVShowEpisodeMediaItem`
   - `MovieMediaItem`
2. `MediaItem` löschen
3. `MediaCollection` löschen
4. Verknüpfungen lösen, die auf `Movie`/`TVShow` verweisen:
   - `MovieGenre`
   - `TVShowGenre`
5. `TVShowEpisode` löschen
6. `TVShowSeason` löschen
7. `TVShow` löschen
8. `Movie` löschen
9. `MovieCollection` löschen
10. `MediaSource` löschen

## Technische Einschränkung

Alle OnDelete-Verhalten sind entweder `ClientSetNull` (Standard) oder `Restrict`; es gibt keine kaskadierenden Datenbanklöschungen. Alle abhängigen Datensätze müssen daher explizit vor der Wurzel entfernt werden.
