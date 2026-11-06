# Interfaces — Bestandsaufnahme

## Explizit nicht vorhanden (verifiziert)

Keine der von der Anforderung genannten Contracts existiert: `IScheduleService`, `IPlaylistEngine`/`ILiveProgramService`, `ILiveSegmenter`, `IChannelAuthService`, `ILiveStatusService`, `IPlaybackDurationService`.

## `IPlaylistService` — Namenskonflikt (bestätigt)

Datei: `VideoWebPlayer/Services/IPlaylistService.cs` (370 Zeilen), Implementierung `PlaylistService`. Verwaltet **benutzereigene Medien-Playlists** — nicht das zeitgesteuerte Live-Programm. Die geplante „Playlist-Engine" darf diesen Namen nicht übernehmen (Anforderung schlägt `ILiveProgramService` o. ä. vor).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPlaylistsAsync` | userId, genreId?, ct | `Task<DtoPlaylist[]>` | Playlists des Benutzers (Genre-Filter) |
| `GetPublicPlaylistsAsync` | userId, genreId?, ct | `Task<DtoPlaylist[]>` | Öffentliche Playlists |
| `SetPlaylistPublicAsync` | playlistId, userId, requesterIsAdmin, isPublic, ct | `Task<DtoPlaylist>` | Öffentlich-Flag (Admin+Owner) |
| `GetPlaylistAsync` | playlistId, userId, ct | `Task<DtoPlaylist?>` | Einzelabruf mit Zugriffsprüfung |
| `CreatePlaylistAsync` | userId, name, description?, sortMode?, ct | `Task<DtoPlaylist>` | Anlegen |
| `UpdatePlaylistAsync` | playlistId, userId, name, description?, sortMode?, ct | `Task<DtoPlaylist>` | Umbenennen (sortMode ignoriert) |
| `DeletePlaylistAsync` | playlistId, userId, ct | `Task` | Löschen |
| `AddMediaToPlaylistAsync` | playlistId, userId, mediaType, mediaId, ct | `Task<DtoPlaylistAddResult>` | Hinzufügen inkl. Kaskade (Show→Staffeln→Episoden, Collection→Movies) |
| `RemoveMediaFromPlaylistAsync` | playlistId, userId, mediaType, mediaId, confirmContinueWatchingRemoval, ct | `Task` | Entfernen (ggf. ContinueWatching-Ersatz) |
| `GetPlaylistEntriesAsync` / `GetPlaylistEntriesPagedAsync` | playlistId, userId, [pageNumber, pageSize,] ct | `Task<DtoPlaylistEntry[]>` / `Task<DtoPlaylistEntriesPagedResult>` | Einträge (verwaiste werden entfernt) |
| `ReorderPlaylistEntryAsync` / `BatchReorderPlaylistEntriesAsync` | playlistId, userId, entryId/reorderOperations, ct | `Task` / `Task<DtoPlaylistEntry[]>` | Manuelle Sortierung |
| `GetMaxSortOrderAsync` | playlistId, userId, ct | `Task<long?>` | Max. `SortOrder` |
| `MoveEntryToBeginningAsync` / `MoveEntryBetweenAsync` | playlistId, userId, entryId, [targetSortOrder,] ct | `Task<DtoPlaylistEntry>` / `Task` | Quick-Move / Drag&Drop |
| `ChangeSortModeAsync` | playlistId, userId, newSortMode, confirmLossOfManualOrder, ct | `Task<DtoPlaylist>` | Moduswechsel |
| `SetPlaylistGenresAsync` / `ResetPlaylistGenresAsync` | playlistId, userId, [genreIds,] ct | `Task<DtoPlaylist>` | Manuelle Genre-Override / Neu-Berechnung |
| `GetNextPlaylistEntryAsync` / `GetPreviousPlaylistEntryAsync` / `AdvancePlaylistAsync` | playlistId, userId, currentEntryId, ct | `Task<DtoPlaylistNavigationResult?>` | Nächster/vorheriger abspielbarer und **zugänglicher** Eintrag (Zeit-unabhängig, Reihenfolge-basiert) |
| `StartPlaylistAsync` | playlistId, userId, entryId?, ct | `Task<DtoPlaylistPlaybackStart>` | Wiedergabe-Startauflösung (inkl. Stream-URL) |
| `ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync` | mediaSourceId, ct | `Task` | Ersatzauflösung bei Quellen-Löschung — aufgerufen von `AdminSourcesController` |
| Cover-Methoden (`GeneratePlaylistCoverAsync`, `PreviewPlaylistCoverAsync`, `SetPlaylistCoverAsync`, `GetPlaylistCoverAsync`, `DeletePlaylistCoverAsync`) | — | — | Cover-Verwaltung |

## `IPlaylistBackfillSignal` — Muster für „Plan-Neuaufbau"-Signal

Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs` (Interface + Singleton-Implementierung `PlaylistBackfillSignal`, One-Slot-Channel, coalescing).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyMarkersWritten` | — | `void` | Marker wurden committed; während eines Scans nicht weckend |
| `BeginScan` | — | `IDisposable` | Scan-Scope; Ende des letzten Scopes weckt den Worker einmal |
| `WaitAsync` | ct | `ValueTask` | Worker-Wartepunkt |

## `IMediaSourceReader` — Datei-/Stream-Zugriff (Erweiterungspunkt für Laufzeitermittlung)

Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`; Implementierungen `LocalMediaSourceReader`, `SftpMediaSourceReader` (Dispatcher: `MediaSourceReaderDispatcher`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadRootDirectory` | source | `IEnumerable<MediaEntry>` | Wurzelverzeichnis lesen |
| `ReadDirectoryEntries` | collection | `IEnumerable<MediaEntry>` | Verzeichnisinhalt lesen |
| `FileExistsAsync` | collection, fileName | `Task<bool>` | Existenzprüfung |
| `ReadFileAsync` | collection, fileName | `Task<string?>` | Textdatei lesen (NFO) |
| `ReadFileStreamAsync` | collection, fileName | `Task<Stream?>` | Datei-Stream |
| `OpenFileStream` | collection, fileName | `Stream?` | Sync-Stream für den Stream-Endpoint |

## `IAuthService` — aktueller Benutzer / Login

Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs` (Interface + Implementierung in einer Datei).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CurrentUser` | — | `ApplicationUser?` | Aktuell authentifizierter Benutzer (Basis von `ApiBaseController.CurrentUser`, `IsAdmin`-Claim) |
| `LoginAsync` | `AuthenticationRequest` | `Task<AuthorizationToken>` | JWT-Login |
| `ImpersonateAsync` | `ImpersonateRequest` | `Task<AuthorizationToken>` | Impersonation (intern genutzt von `InternalVideoWebPlayerClient`) |

## `IUnlockedMediaService` — Einzelfreischaltungen

Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`, Implementierung `UnlockedMediaService`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry`, ct | `Task<bool>` | Ist Sammlung/Serie für aufrufenden Benutzer freigeschaltet |
| `GetUnlockedUserIdsAsync` | `DtoMediaEntry`, ct | `Task<string[]>` | Freigeschaltete Benutzer |
| `SetUnlockedUsersAsync` | `DtoMediaEntry`, userIds, ct | `Task` | Freischaltungen setzen |
| `GetUnlockedMovieCollectionIdsForUserAsync` / `GetUnlockedTVShowIdsForUserAsync` / `GetUnlockedSourceIdsForUserAsync` | userId, ct | `Task<long[]>` | Freischalt-IDs eines Benutzers |
| `GetMediaSourceIdsForUserAsync` | userId, ct | `Task<long[]>` | Für Benutzer freigegebene Quellen (`MediaSourceUser`) |

## `IDeviceTokenService` / `IRefreshTokenService` — Geräte-Auth

Dateien: `Services/DeviceTokenService.cs`, `Services/RefreshTokenService.cs` (Interface + `internal` Implementierung je Datei).

`IDeviceTokenService`: `IssueAsync(deviceName?, createdByUserId?, ct)` → `IssuedDeviceToken`; `IsValidDeviceTokenAsync(token, ct)`; `RevokeAsync(deviceId, ct)`; `RenameAsync(deviceId, newName, ct)`; `GetDevicesAsync(ct)`.

`IRefreshTokenService`: `IssueAsync(userId, deviceId, ct)` → `IssuedRefreshToken`; `RotateAsync(refreshToken, ct)` → `RefreshRotationResult` (Success/UserId/DeviceId/NewToken); `RevokeAsync(refreshToken, ct)`.

## `IPlaylistApiClient` — Client-seitiges API (Namensnähe beachten)

Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`, Implementierung `VideoWebPlayerClient.Playlists.cs` (partielle Klasse `VideoWebPlayerClient`). Spiegelt die Playlist-REST-Routen; ein `LiveTv`-API-Client würde als weitere partielle Datei/eigenes Interface ergänzt (Muster `VideoWebPlayerClient.Pairing.cs`).

## Weitere Interfaces (weniger relevant)

`IFavoritesService`, `IGenreService`, `IMediaMetadataWriteCoordinator`, `IBackgroundProcessingGate`, `IDemoDataSetService`, `IUpdateBackupService` (`Services/` bzw. `Services/Backups/`, `Services/Updates/`, `Services/DemoData/`).
