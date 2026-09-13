# Datenmodelle

## `ContinueWatchingDto`
Datei: `VideoWebPlayer.Client/Models/ContinueWatchingDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| MediaType | string | Medientyp (z.B. "movie" oder "episode") |
| Entry | DtoMediaEntry | Die Medieneinheit (DtoMovie oder DtoTVShowEpisode) |
| PositionSeconds | long | Letzte Wiedergabeposition in Sekunden |
| DurationSeconds | long? | Gesamtdauer des Mediums in Sekunden, oder null wenn unbekannt |
| Title | string | Titel des Eintrags |
| PosterPictureId | long? | ID der Poster-Grafik, oder null wenn nicht vorhanden |
| WatchedAt | DateTime? | Zeitstempel des letzten Zuschauens, oder null wenn unbekannt |
| PlaylistId | long? | ID der zugehörigen Playlist, oder null wenn außerhalb eines Playlist-Kontexts erstellt |
| PlaylistName | string? | Name der Playlist, oder null wenn PlaylistId null |
| PlaylistEntryId | long? | ID des PlaylistEntry, das dieses Medium derzeit in der Playlist referenziert, oder null wenn nicht mehr Teil der Playlist |

**Fehlende Eigenschaft (Problem 1):**
- `Id: long` – Die Datenbank-ID der zugehörigen `ContinueWatchingEntry`. Wird in `ContinueWatchingService.GetListAsync` nicht gesetzt.

---

## `DtoPlaylistPlaybackStart`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistPlaybackStart.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| PlaylistId | long | ID der Playlist |
| PlaylistName | string | Name der Playlist |
| TotalCount | int | Gesamtanzahl (nicht verwaister) Einträge der Playlist |
| CurrentPosition | int | 1-basierte Position des Start-Eintrags in der aktuellen Sortierreihenfolge |
| CurrentEntryId | long | ID des aufgelösten Start-Eintrags |
| CurrentEntry | DtoPlaylistEntry | Der aufgelöste Start-Eintrag |
| StreamUrl | string | Relative Stream-URL des Start-Eintrags (ohne `access_token`-Parameter) |
| MediaType | string | Medientyp in Videoabspiel-Konvention (z.B. "movie" oder "episode") |
| MediaId | long | ID des Mediums des Start-Eintrags |

**Fehlende Eigenschaft (Problem 4):**
- `StartPositionSeconds: long` – Die Wiedergabeposition im Video, aus dem `ContinueWatchingEntry.PositionSeconds` befüllt. Wird nicht vorhanden und somit in `PlaylistDetail.razor` nicht übergeben.

---

## `ContinueWatchingEntry`
Datei: `VideoWebPlayer/Data/Models/ContinueWatchingEntry.cs`

Datenbankmodell (wird durch Lesen referenzierter Dateien nicht vollständig angezeigt), aber relevant für die Konfiguration:
- `Id: long` – Primärschlüssel (wird von der Datenbank-ID repräsentiert)
- `UserId: string` – Benutzer-ID
- `MovieId: long?` – ID des Films (NULL für Episoden)
- `TVShowEpisodeId: long?` – ID der Episode (NULL für Filme)
- `PlaylistId: long?` – ID der zugehörigen Playlist (NULL für nicht-Playlist-Einträge)
- `Position: TimeSpan` – Aktuelle Wiedergabeposition
- `Duration: TimeSpan?` – Gesamtdauer des Mediums
- `ListOrder: int` – Sortierposition in der Weiterschauen-Liste
- `UpdatedAt: DateTime` – Zeitstempel der letzten Aktualisierung
