# Datenmodelle

## `ContinueWatchingEntry`

**Datei:** `VideoWebPlayer\Data\Entities\ContinueWatchingEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | long | Eindeutige Kennung des Eintrags (Primary Key) |
| UserId | string | Benutzer-ID (max. 450 Zeichen), erforderlich |
| MovieId | long? | Foreign Key zur Movie-Entität (nullable) |
| TVShowEpisodeId | long? | Foreign Key zur TVShowEpisode-Entität (nullable) |
| Position | TimeSpan | Aktuelle Wiedergabeposition, erforderlich |
| Duration | TimeSpan? | Gesamte Mediendauer (nullable) |
| UpdatedAt | DateTime | Zeitstempel der letzten Änderung |
| ListOrder | long | Benutzerspezifische Sortierreihenfolge der Liste |
| Movie | Movie? | Navigations-Property zum Film |
| TVShowEpisode | TVShowEpisode? | Navigations-Property zur Episode |

**Fehlende Eigenschaften (erforderlich für Anforderung):**
- `PlaylistId` (long?, nullable Foreign Key auf `Playlist`)
- `Playlist` (Playlist?, Navigations-Property)

**Hinweise zur Deduplizierung (aktuell):**
- Eindeutigkeit wird derzeit nicht auf Datenbankebene durch einen Unique Constraint erzwungen
- Die Deduplizierung erfolgt durch Service-Logik in `ContinueWatchingService` (Methoden `RemoveExistingTVShowEntry` und `RemoveExtsingMovieCollectionEntry`)
- Aktuell pro Serie/Filmsammlung nur ein Eintrag pro Benutzer möglich (der neueste wird beibehalten)
- Mit Playlist-Bindung muss die Deduplizierung auf die Tripel (UserId, VideoId, PlaylistId) erweitert werden

---

## `ContinueWatchingDto`

**Datei:** `VideoWebPlayer.Client\Models\ContinueWatchingDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| MediaType | string | "movie" oder "episode" |
| Entry | DtoMediaEntry | Das dargestellte Medium (Film oder Episode) |
| PositionSeconds | long | Spielposition in Sekunden |
| DurationSeconds | long? | Gesamtdauer in Sekunden (nullable) |
| Title | string | Titel des Eintrags |
| PosterPictureId | long? | Poster-Bild-ID (nullable) |
| WatchedAt | DateTime? | Zeitstempel, wann das Video als „gesehen" markiert wurde (nullable) |

**Fehlende Eigenschaften (erforderlich für Anforderung):**
- `PlaylistId` (long?, optional)
- `PlaylistName` (string?, optional, zur UI-Anzeige)

---

## `Playlist`

**Datei:** `VideoWebPlayer\Data\Playlist.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| Id | long | Eindeutige Kennung (Primary Key) |
| UserId | string | Besitzer der Playlist, erforderlich, Foreign Key |
| Name | string | Playlist-Name (max. 255 Zeichen), erforderlich |
| Description | string? | Optionale Beschreibung (max. 2000 Zeichen) |
| SortMode | PlaylistSortMode | Sortierreihenfolge (Standard: ByReleaseDate) |
| CreatedAt | DateTime | Erstellungszeitpunkt |
| UpdatedAt | DateTime | Zeitpunkt der letzten Änderung |
| PlaylistEntries | ICollection<PlaylistEntry> | Navigations-Property zu den Einträgen der Playlist |

**Hinweis:** Playlist-Entität existiert bereits und ist von Schritt 5 bekannt.

---

## `PlaylistPlaybackContext`

**Datei:** `VideoWebPlayer\ViewModels\PlaylistPlaybackContext.cs`

Record mit folgenden Eigenschaften:
- `PlaylistId` (long): Playlist-ID, die gerade abgespielt wird
- `CurrentEntryId` (long): ID des aktuell abgespielten Eintrags
- `TotalCount` (int): Gesamtzahl der Einträge in der Playlist
- `PlaylistName` (string): Name der Playlist (für Badge-Anzeige)

**Hinweis:** Immutable Record, wird klient-seitig verwendet, nie persistent gespeichert. Von Schritt 5 bereits implementiert und in `VideoPlayer.razor` verwendet.
