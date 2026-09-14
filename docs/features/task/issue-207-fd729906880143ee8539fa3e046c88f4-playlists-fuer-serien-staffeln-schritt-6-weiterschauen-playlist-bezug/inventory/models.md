# Datenmodellklassen

## `DtoPlaylistPlaybackStart`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistPlaybackStart.cs`

DTO für die Rückgabe der Wiedergabevorbereitung bei Initial-Start oder Neustart einer Playlist (`POST /api/playlists/{id}/play`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `PlaylistId` | `long` | ID der Playlist |
| `PlaylistName` | `string` | Name der Playlist (zur Anzeige im Badge) |
| `TotalCount` | `int` | Gesamtzahl der (nicht verwaisten) Einträge in der Playlist |
| `CurrentPosition` | `int` | 1-basierte Position des Start-Eintrags in der aktuellen Sortierung der Playlist |
| `CurrentEntryId` | `long` | ID des aufgelösten Start-Eintrags |
| `CurrentEntry` | `DtoPlaylistEntry` | Der vollständige Start-Eintrag |
| `StreamUrl` | `string` | Relative Stream-URL des Eintrags (ohne `access_token` Query-Parameter) |
| `MediaType` | `string` | Medientyp des Eintrags (z. B. `"movie"` oder `"episode"`) |
| `MediaId` | `long` | ID des Mediums des Eintrags |
| `StartPositionSeconds` | `long` | **Startposition in Sekunden** — wird aus `ContinueWatchingEntry.Position` befüllt (0 wenn keine existiert). Dies ist der Schlüssel zur aktuellen Regression. |

**Bemerkung:** `StartPositionSeconds` wird korrekt in `DtoPlaylistPlaybackStart` befüllt und bei `PlaylistDetail.OnInitializedAsync()` mit `StartPlaylistAsync()` abgerufen. Das Problem tritt auf, wenn nach einem Entry-Wechsel dieser Wert nicht aktualisiert wird.

## `DtoPlaylistNavigationResult`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistNavigationResult.cs`

DTO für das Ergebnis einer Navigationsoperation (`/play/next`, `/play/previous`, `/play/advance`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Entry` | `DtoPlaylistEntry` | Der aufgelöste Eintrag (nächster/vorheriger) |
| `Position` | `int` | 1-basierte Position des `Entry` in der aktuellen Sortierung der Playlist |

**Kritischer Befund:** `DtoPlaylistNavigationResult` enthält **KEINE** `StartPositionSeconds`! Das bedeutet, dass Szenario A aus der Anforderung (die neue Position bereits in der Navigation-API-Response) nicht ohne Backend-Änderungen implementiert werden kann.

## `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`

Ein einzelner Eintrag einer Playlist.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige ID des Eintrags |
| `PlaylistId` | `long` | ID der Playlist, zu der dieser Eintrag gehört |
| `MediaType` | `string` | Medientyp des referenzierten Mediums (z. B. "movie", "tvshow", "tvshowepisode") |
| `MediaId` | `long` | ID des referenzierten Mediums |
| `MediaTitle` | `string` | Titel des referenzierten Mediums |
| `ParentMediaType` | `string?` | Typ des übergeordneten Mediums (z. B. "tvshow" für eine Episode) oder `null` |
| `ParentMediaId` | `long?` | ID des übergeordneten Mediums oder `null` |
| `ParentMediaTitle` | `string?` | Titel des übergeordneten Mediums oder `null` |
| `AddedAt` | `DateTime` | Zeitstempel, wann der Eintrag der Playlist hinzugefügt wurde |
| `ResolvedPictureId` | `long?` | ID des auf dem Server aufgelösten Bildes (Poster mit Fallbacks) |
| `IsAccessible` | `bool` | Ob der Benutzer Zugriff auf das Medium hat |
| `SortOrder` | `long?` | Manuelle Sortierreihenfolge (nur für `Manual`-Modus) |

**Bemerkung:** Auch `DtoPlaylistEntry` enthält keine Start-Position.
