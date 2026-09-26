# Bestandsaufnahme: Interfaces

## `IPlaylistApiClient`

Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`

**Zweck:** Vertrag für Playlist-API-Aufrufe. Wird von `VideoWebPlayerClient` implementiert (partial).

### Playlist-Verwaltungs-Methoden

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `GetPlaylistsAsync(CancellationToken)` | - | `Task<DtoPlaylist[]>` | Alle Playlists des Benutzers abrufen |
| `GetPublicPlaylistsAsync(CancellationToken)` | - | `Task<DtoPlaylist[]>` | Öffentliche Playlists abrufen |
| `CreatePlaylistAsync(DtoCreatePlaylistRequest, CancellationToken)` | name, description | `Task<DtoPlaylist>` | Neue Playlist anlegen |
| `UpdatePlaylistAsync(long id, DtoUpdatePlaylistRequest, CancellationToken)` | id, updates | `Task<DtoPlaylist>` | Playlist umbenennen/beschreiben |
| `DeletePlaylistAsync(long id, CancellationToken)` | id | `Task` | Playlist löschen |
| `SetPlaylistPublicAsync(long id, bool isPublic, CancellationToken)` | id, flag | `Task<DtoPlaylist>` | Öffentlich-Status setzen |

### Playlist-Einträge-Methoden

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest, CancellationToken)` | playlistId, media | `Task<DtoPlaylistAddResult>` | Media zu Playlist hinzufügen |
| `RequestPlaylistEntriesPagedAsync(long playlistId, int page, int size, CancellationToken)` | playlistId, page, size | `Task<DtoPlaylistEntriesPagedResult>` | Einträge paginiert abrufen |
| `RemovePlaylistEntryAsync(long playlistId, long entryId, bool force, CancellationToken)` | playlistId, entryId, force | `Task` | Eintrag entfernen |
| `SetPlaylistGenresAsync(long playlistId, DtoSetPlaylistGenresRequest, CancellationToken)` | playlistId, genres | `Task<DtoPlaylist>` | Genres setzen |
| `ResetPlaylistGenresAsync(long playlistId, CancellationToken)` | playlistId | `Task<DtoPlaylist>` | Genres zurücksetzen |

### Playlist-Wiedergabe-Methoden (relevant für A5)

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `PlayAsync(long playlistId, long entryId, CancellationToken)` | playlistId, entryId | `Task<DtoPlaylistPlaybackStart>` | Wiedergabe starten |
| `PlayNextAsync(long playlistId, CancellationToken)` | playlistId | `Task<DtoPlaylistNavigationResult>` | Nächster Titel |
| `PlayPreviousAsync(long playlistId, CancellationToken)` | playlistId | `Task<DtoPlaylistNavigationResult>` | Vorheriger Titel |
| `PlayAdvanceAsync(long playlistId, int count, CancellationToken)` | playlistId, count | `Task<DtoPlaylistNavigationResult>` | Automatisches Weiterschalten |

**Status für A5:** Diese Methoden verwenden `PostForOptionalPlaylistNavigationResultAsync`, die in `InternalVideoWebPlayerClient` nicht überschrieben ist → Fehler ohne Impersonierung möglich.

### Playlist-Cover-Methoden

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `GetPlaylistCoverAsync(long playlistId, CancellationToken)` | playlistId | `Task<DtoPlaylistCoverResult>` | Cover abrufen |
| `UploadPlaylistCoverAsync(long playlistId, Stream stream, string contentType, CancellationToken)` | playlistId, stream | `Task<DtoPlaylistCoverResult>` | Cover hochladen |
| `RegeneratePlaylistCoverAsync(long playlistId, bool force, CancellationToken)` | playlistId, force | `Task<DtoPlaylistCoverResult>` | Cover neu generieren |
| `DeletePlaylistCoverAsync(long playlistId, CancellationToken)` | playlistId | `Task` | Cover löschen |
| `GetPlaylistCoverPreviewAsync(long playlistId, DtoPlaylistCoverPreview, CancellationToken)` | playlistId, preview | `Task<Stream>` | Cover-Vorschau |

### Playlist-Sortierung-Methoden

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `SetPlaylistSortModeAsync(long playlistId, DtoChangeSortModeRequest, CancellationToken)` | playlistId, mode | `Task<DtoPlaylist>` | Sortiermodus setzen |
| `GetMaxSortOrderAsync(long playlistId, CancellationToken)` | playlistId | `Task<DtoMaxSortOrderResult>` | Max. Sortierreihenfolge |
| `ReorderPlaylistEntryAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest, CancellationToken)` | playlistId, entryId, order | `Task<DtoPlaylist>` | Eintrag umordnen |
| `MovePlaylistEntryToBeginningAsync(long playlistId, long entryId, CancellationToken)` | playlistId, entryId | `Task<DtoPlaylist>` | Zu Anfang verschieben |
| `MovePlaylistEntryBetweenAsync(long playlistId, long entryId, DtoReorderPlaylistEntryRequest, CancellationToken)` | playlistId, entryId, position | `Task<DtoPlaylist>` | Zwischen zwei verschieben |
| `BatchReorderPlaylistEntriesAsync(long playlistId, DtoBatchReorderPlaylistEntriesRequest, CancellationToken)` | playlistId, reorders | `Task<DtoPlaylist>` | Batch-Umordnung |

**Alle diese Methoden verwenden HTTP PUT/PATCH/DELETE, die in `InternalVideoWebPlayerClient` NICHT überschrieben sind.**

---

## Andere relevante Interfaces

### `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

**Zweck:** Dispatcher für unterschiedliche Medienquellen (SFTP, Local, etc.).

**Relevanz für A4:** Wird von `ItemsController.StreamMediaItem` genutzt zum Öffnen von Dateiströmen.

### `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

**Zweck:** Prüfung, ob Media für Benutzer freigegeben ist.

**Relevanz für A4:** Wird von `ItemsController.EnsureAccessAsync` genutzt zur Prüfung der Berechtigung.

### `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs`

**Zweck:** Business-Logik für Playlists.

**Relevanz:** `PlaylistsController` nutzt diesen Service. Vorbild für A4.

### `IAuthService`
Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs`

**Zweck:** Authentifizierung und aktuelle Benutzer-Extraktion.

**Relevanz:** Basis-Service für beide Controller.
