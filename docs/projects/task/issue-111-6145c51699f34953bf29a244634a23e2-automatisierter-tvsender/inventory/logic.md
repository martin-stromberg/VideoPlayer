# Logik — Bestandsaufnahme

Logikklassen unter `VideoWebPlayer/Services/`, `VideoWebPlayer/Controllers/`, `VideoWebPlayer/Hubs/`, `VideoWebPlayer/Components/`. Aufgenommen sind Klassen, die für die Anforderung relevant sind (als Vorbild, Wiederverwendung oder Erweiterungspunkt).

## Explizit nicht vorhanden (verifiziert)

- `ScheduleService`/`IScheduleService`, `PlaylistEngine`/`ILiveProgramService`, `LiveSegmenter`, `LiveStatusService`, `PlaybackDurationService` — keine Treffer.
- `LiveTvController`, `ScheduleRulesController` — keine Treffer.
- Segmentierung/Transcodierung: keine FFmpeg-/ffprobe-Anbindung, kein HLS/DASH (kein `.m3u8`/`EXT-X`/`.mpd` irgendwo im Code oder in Paketreferenzen).
- `ScheduleGeneratorWorker`, `LiveSegmenterWorker` — keine Treffer.

## Scan- und Klassifikations-Pipeline (möglicher Erweiterungspunkt für Laufzeitermittlung)

### `MediaSourceScanService`

Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs` — `BackgroundService`, registriert via `AddHostedService` (`ServiceCollectionExtensions.cs:293`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected | Periodischer Lauf: ruft `MediaSourceScanner`, danach `MediaSourceClassifier` (`ClassifyMediaItemsAsync`, ggf. `ClassifyMediaCollectionsAsync`), benachrichtigt via `MediaUpdateNotificationService` |

### `MediaSourceScanner`

Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync` | public | Scannt alle Quellen |
| `ScanNextMediaCollection` | public | Scannt die nächste fällige Kollektion (`MediaCollection.ScanDueAt`) |
| `ScanMediaCollectionAsync(long)` | public | Scannt eine Kollektion |
| `ScanCollectionTreeAsync(long)` | public | Scannt einen Verzeichnisbaum |

### `MediaSourceClassifier`

Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` — ordnet `MediaItem`s zu `Movie`/`TVShow*` zu, liest NFO-Dateien (`tvshow.nfo` u. a. via `IMediaSourceReader`), ruft `LoadFromXml` der Entities. Serialisiert sich selbst über `_classificationRunning`/`Interlocked` (nur eine Klassifizierung gleichzeitig, weitere werden gequeuet).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync` | public | Klassifiziert alles |
| `ClassifyMediaItemsAsync` | public | Items → Movies/Episoden |
| `ClassifyMediaCollectionsAsync` | public | Kollektionen → Shows/Staffeln/Filmsammlungen |
| `ClassifyCollectionTreeAsync(long)` | public | Teilbaum |
| `ReloadGenres` / `CheckReloadGenres` | public | Genre-Neuladen |
| `BackfillMissingActorsAsync` | public | Schauspieler-Nachzug (via `ActorBackfillWorker`) |

### `IMediaSourceReader`-Implementierungen

- `LocalMediaSourceReader` (`Services/LocalMediaSourceReader.cs`): `ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`, `OpenFileStream` — Dateisystem-Zugriff, liefert den `Stream` für den Stream-Endpoint.
- `SftpMediaSourceReader` (`Services/SftpMediaSourceReader.cs`): SFTP-Pendant (SSH.NET), `SftpStreamWrapper` für Streams.
- `MediaSourceReaderDispatcher`: wählt Reader nach `MediaSource.SourceType`.

## Playlist-Funktionalität (Issue #207 — wichtigste Kollisionsstelle)

### `PlaylistService`

Datei: `VideoWebPlayer/Services/PlaylistService.cs` (~1.800 Zeilen), implementiert `IPlaylistService`. Benutzereigene Playlists — **fachlich unabhängig vom geplanten Live-Programm**, aber Namen und Begriffswelt („Playlist", „Entries", „Play/Next/Previous") kollidieren.

Öffentliche Methoden (Auswahl): `GetPlaylistsAsync`, `GetPublicPlaylistsAsync`, `GetPlaylistAsync`, `SetPlaylistPublicAsync` (Admin+Owner-Regel), `CreatePlaylistAsync`, `UpdatePlaylistAsync`, `DeletePlaylistAsync`, `AddMediaToPlaylistAsync` (Kaskade: Show→Staffeln→Episoden, Collection→Movies), `RemoveMediaFromPlaylistAsync`, `GetPlaylistEntriesAsync`, `GetPlaylistEntriesPagedAsync`, `ReorderPlaylistEntryAsync`, `BatchReorderPlaylistEntriesAsync`, `GetMaxSortOrderAsync`, `MoveEntryToBeginningAsync`, `MoveEntryBetweenAsync`, `ChangeSortModeAsync`, `SetPlaylistGenresAsync`, `ResetPlaylistGenresAsync`, `ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync`, `GetNextPlaylistEntryAsync`, `GetPreviousPlaylistEntryAsync`, `AdvancePlaylistAsync`, `StartPlaylistAsync`, `GeneratePlaylistCoverAsync`, `PreviewPlaylistCoverAsync`, `SetPlaylistCoverAsync`, `GetPlaylistCoverAsync`, `DeletePlaylistCoverAsync`.

### `PlaylistBackfillCoordinator` / `PlaylistBackfillService`

Dateien: `Services/PlaylistBackfillCoordinator.cs`, `PlaylistBackfillService.cs` — **Architekturvorbild für den Scheduler**: markergetriebene Nachverarbeitung statt Polling.

| Methode | Klasse | Kurzbeschreibung |
|---------|--------|------------------|
| `ProcessPendingAsync` | Coordinator | Verarbeitet offene `PlaylistBackfillMarker` in Blöcken (`PlaylistSettings.BackfillBatchSize`, `BackfillBlockPauseSeconds`); Ergebnis `PlaylistBackfillRunSummary` |
| `RunSafetySweepAsync`/`RunSafetySweepIfDueAsync` | Coordinator | Täglicher Sicherheitslauf über alle Playlists mit Sammel-Medien; Fälligkeit persistent über `Setup.PlaylistBackfillLastSweepAt` |
| `GetSafetySweepDueAtAsync` | Coordinator | Nächster Fälligkeitszeitpunkt |
| `PlanPendingAsync` | Service | Liest Marker → Arbeitsplan (`PlaylistBackfillPlan`, `IsEmpty`) |
| `ReleaseMarkersAsync` | Service | Marker nach Verarbeitung/Fehlschlag freigeben |
| `LoadSweepBlockAsync` | Service | Nächster Playlist-Block für Sweep |
| `BackfillPlaylistsAsync` | Service | Neue Kinder markierter Sammel-Medien in betroffene Playlists einfügen |

### `PlaylistBackfillWorker`

Datei: `Services/PlaylistBackfillWorker.cs` — `BackgroundService`, `AddHostedService` (`ServiceCollectionExtensions.cs:301`). **Muster für `ScheduleGeneratorWorker`:** schläft bis (a) `IPlaylistBackfillSignal` feuert (Scan-Ende/Medienerzeugung, Settle-Zeit `BackfillSettleSeconds`), (b) der Safety-Sweep fällig wird (einzelner Timer, `TimeProvider`-injizierbar, `MaxTimerWait = 24 d`), (c) Start-Catch-up nach `initialDelay`. Fehler pro Arbeitseinheit gekapselt (`GuardedAsync`).

### `ApplicationDbContext.PlaylistBackfillMarking` (SaveChanges-Hook)

Datei: `Data/ApplicationDbContext.PlaylistBackfillMarking.cs` (partial `ApplicationDbContext`). Überschreibt `SaveChanges`/`SaveChangesAsync`: neue/umgehängte `TVShowEpisode` (markiert Staffel+Show), `TVShowSeason` (Show), `Movie` (Collection) schreiben deduplizierte Marker per `INSERT ... ON CONFLICT DO UPDATE` in derselben Transaktion. Umgeht nur Roh-SQL/Restore — dafür gibt es den Safety-Sweep. **Vorbild für „neue Episode → Sendeplan anfassen".**

### `PlaylistEntryAccessResolver` / `MediaHierarchyRegistry` / `MediaEntryFilter`

- `PlaylistEntryAccessResolver` (`Services/PlaylistEntryAccessResolver.cs`, internal): `LoadAccessCheckDataAsync` (freigeschaltete Quellen/Unlocks des Benutzers), `LoadUnlockHierarchyMappingsAsync`, `LoadMediaSourceMappingsAsync`, `ResolveUnlockMediaId`, `ResolveMediaSourceId`, `ResolveAccessibilityAsync`, `CheckEntryAccessible` — **serverseitige Zugriffsauflösung pro Medieneintrag**, wiederverwendbar für „darf der Sender diesen Inhalt planen".
- `MediaHierarchyRegistry` (`Services/MediaHierarchyRegistry.cs`, internal static): Registry `MediaTypeHandler` pro `MediaType` mit `LoadCascadeChildrenAsync` und **`GetHierarchySequenceAsync` → `(ParentId, SequenceNumber)`** — liefert Episoden-Sequenznummern innerhalb der Serie/Staffel; Grundlage einer möglichen Fortsetzungslogik. `ParseMediaType`/`TryParseKnownMediaType`/`GroupMediaIdsByType`.
- `MediaEntryFilter` (`Services/MediaEntryFilter.cs`, internal static): `IsIgnoredEntry` — Dateinamen-Filter beim Scan.

## Streaming-Endpoints (bestehende Auslieferung — kein HLS)

### `ItemsController`

Datei: `VideoWebPlayer/Controllers/ItemsController.cs` — `[Route("api/[controller]")]`, `[BearerTokenCheck]` (Klassenebene), erbt `ApiBaseController`.

| Endpoint | Kurzbeschreibung |
|----------|------------------|
| `GET api/items/{type}/{id}/stream` (`StreamMediaItem`, Z. 663) | Liefert Videodatei als `File(stream, contentType, enableRangeProcessing: true)` — HTTP-Range-Streaming der Originaldatei; `type`: `movie`, `tvshowepisode` (`tvshow` wird auf Episode normalisiert); ContentTypes: `video/mp4`, `video/x-matroska`, `video/x-msvideo`, `video/mpeg` |
| `GET api/items/{type}/{id}/download` (`Download`, Z. 683) | Gleiche Auflösung als Download |
| `GET api/items/genres`, `POST api/items/metadata`, `GET api/items`, `GET api/items/recent`, `GET api/items/{type}/{id}` | Metadaten-/Listenabfragen |

Interner Fluss: `CheckLogedIn` → `OpenMediaFileAsync` → `FindMediaItemAsync` (löst `Movie`/`TVShowEpisode` → `MediaItem` auf, ruft `EnsureAccessAsync`) → `MediaCollections`+`MediaSource` laden → `IMediaSourceReader.OpenFileStream`. `EnsureAccessAsync` (Z. 523): `MediaSourceUsers.AnyAsync(userId, sourceId)` **oder** `IsUnlockedAsync(entry)` → sonst `ForbiddenAccessException`. Fehlerbild: 400 ungültiger Typ/ID, 404 unbekannt/keine Datei, 403 keine Berechtigung, 500 defekte Datei.

### Auth-Filter (für Playlist-/Segment-Endpoints wiederverwendbar)

- **`BearerTokenCheckAttribute`** (`Controllers/Attributes/BearerTokenCheckAttribute.cs`): JWT-Validierung (`SymmetricSecurityKey` aus DI, Issuer=Audience=`Jwt:Issuer`). **Akzeptiert `Authorization: Bearer` ODER Query-Parameter `?access_token=`** — das etablierte Muster für `<video>`/`<img>`/SignalR ohne Header; wird in `docs/API.md` dokumentiert und von `VideoPlayer.razor`, `NavMenu.razor`, diversen Seiten genutzt. Direkt für HLS-Playlist/Segment-URLs nutzbar.
- **`ApiTokenCheckAttribute`** (`ApiTokenCheckAttribute.cs`): `X-API-Key`-Gate mit `ApiTokenScope` (Geräte-Token aus DB + statischer Fallback `Jwt:ApiToken:Maui`), vorgeschaltet bei `AuthController`.
- **`ConnectionCheckAttribute`**, `WhitelistIpMiddleware`, `RestoreInProgressMiddleware`, `FirstUserRedirectMiddleware`, `LoginIpBlockService`: weitere Gates.

### `ApiBaseController`

Datei: `Controllers/ApiBaseController.cs` — Basisklasse aller API-Controller: `CurrentUser` (`IAuthService.CurrentUser`), `CheckLoggedIn()`/`CheckLogedIn()`, `GetPlaceholderBytesAsync`, `Create<T>` (DTO-Mapping), `LoginAsync`/`Impersonate`.

### Admin-Muster

- `AdminSourcesController` (`Controllers/AdminSourcesController.cs`): `[Route("api/[controller]")]`, prüft `User.HasClaim("IsAdmin", "True")` → 403; injiziert `ApplicationDbContext`, `IPlaylistService`. **Muster für `ScheduleRulesController`.**
- `PlaylistsController.SetPlaylistPublic` (`PUT api/playlists/{id}/public`): Admin-Flag mit Owner-Bezug über `CurrentUser.IsAdmin`.

## Ereignis- und Benachrichtigungs-Infrastruktur

- **`EventManager`** (`Services/EventManager.cs`): typsicherer In-Process-Pub/Sub — `Subscribe<TEvent>`, `SubscribeDisposable<TEvent>`, `Unsubscribe<TEvent>`, `Publish<TEvent>`. Bestehende Events: `MediaSourceCreatedEvent`, `MediaSourceUpdatedEvent`, `MediaSourceDeletedEvent`, `BackgroundProcessingStatusEvent` (`Events/`).
- **`MediaUpdateHub`** (`Hubs/MediaUpdateHub.cs`): SignalR-Hub `/hubs/mediaupdate` (`WebApplicationExtensions.cs:100`) mit `NotifyNewVideosScanned`, `NotifyContinueWatchingUpdated`, `NotifyFavoritesChanged`.
- **`MediaUpdateNotificationService`** (`Services/MediaUpdateNotificationService.cs`): `NotifyStatusAsync`, `NotifyNewVideosScannedAsync`, `NotifyContinueWatchingUpdatedAsync`, `NotifyFavoritesChangedAsync`, `NotifyMediaUpdatedAsync` — schickt Updates an SignalR-Clients (`StatusTicker.razor`).

## Konfigurations- und Einstellungsdienste

- **`PlaylistSettings`** (`Configuration/PlaylistSettings.cs`): `IOptions`-Pattern, `appsettings`-gebunden — Batchgrößen, Pausen, Settle-Zeit, Sweep-Intervall, Cover-Limits. **Muster für Sender-/Stream-Einstellungen** (Planungshorizont, Segmentlänge).
- **`ProgramSettingsService`** (`Services/ProgramSettingsService.cs`): DB-persistente, admin-editierbare Einstellungen über `Setup`-Tabelle — `GetOrCreateSetupAsync`, `GetScanIntervalsAsync`, `UpdateScanIntervalsAsync`, `GetApplicationTitleAsync`, `GetContinueWatchingEndThresholdAsync`, `UpdateGeneralSettingsAsync`, `GetDiscoveryPublicBaseUrlAsync`, `GetMdnsAdvertisementEnabledAsync`, `UpdateMdnsAdvertisementEnabledAsync`. Admin-UI: `Components/Pages/Admin/ProgramSettings.razor` (`/admin/program-settings`).
- **`DiscoveryOptions`/`MdnsOptions`** + Validatoren (`Configuration/`): Options-Validierung mit `IValidateOptions`.

## Geräte-/Token-Auth (für Segment-Auth auf gekoppelten Geräten)

- **`DeviceTokenService`** (`Services/DeviceTokenService.cs`, `IDeviceTokenService`): `IssueAsync` → `IssuedDeviceToken` (Klartext-Token + `DeviceId`), `IsValidDeviceTokenAsync`, `RevokeAsync`, `RenameAsync`, `GetDevicesAsync` — Token als `PairedDevice.TokenHash` gespeichert.
- **`RefreshTokenService`** (`Services/RefreshTokenService.cs`, `IRefreshTokenService`): `IssueAsync`, `RotateAsync` → `RefreshRotationResult` (Rotation mit `ReplacedByHash`), `RevokeAsync`.
- **`PairingService`/`PairingBootstrapService`/`PairingUrlBuilder`/`PairingCrypto`** (`Services/`, `Services/Security/`): Pairing-Flows (`api/pairing/exchange`, `api/pairing/bootstrap` — beide ohne Auth), QR/Shortcode-Tickets.

## Weitere relevante Services

- **`ContinueWatchingService`/`ContinueWatchingBuffer`/`ContinueWatchingWorker`**: Fortschrittspuffer mit zeitverzögertem Flush (`BackgroundService` + `Channel`-ähnlichem Buffer) — zweites Worker-Muster; `Duration` wird clientseitig gemeldet (kein Scan-Feld).
- **`UnlockedMediaService`** (`IUnlockedMediaService`): `IsUnlockedAsync`, `GetUnlockedUserIdsAsync`, `SetUnlockedUsersAsync`, `GetUnlockedMovieCollectionIdsForUserAsync`, `GetUnlockedTVShowIdsForUserAsync`, `GetUnlockedSourceIdsForUserAsync`, `GetMediaSourceIdsForUserAsync` — Einzelfreischaltungen (Filmsammlung, Serie, Quelle).
- **`DataUpgradeManager`** (`Services/DataUpgradeManager.cs`): `CurrentVersion = 8`, `EnsureUpToDateAsync` — Daten-Upgrades nach App-Start (z. B. `ReloadGenres`); möglicher Ort für nachträgliches Befüllen eines neuen Dauer-Felds.
- **`FavoritesService`/`RecentEntryService`/`WatchedStatusService`/`GenreService`/`ActorBackfillWorker`/`MdnsAdvertiserWorker`/`UdpDiscoveryListener`**: weitere Features/Worker.
- **`VideoWebPlayerBackupFacade`, `ManualBackupJobService`, `RestoreBackupJobService`, `BackupSettingsService`, `VideoWebPlayerAutomaticBackupRunner`, `BackgroundProcessingGate` (`Services/Backups/`)**: Backup-Betrieb; `BackgroundProcessingGate` blockiert Hintergrundarbeit während Backup/Restore.
- **`StaticAssetVersioner`**, `MediaMetadataEditorService`/`IMediaMetadataWriteCoordinator`, `InternalConnectionService`, `DiscoveryResponseBuilder`, `MdnsServiceProfileBuilder`, `UpdateAdminService`/`UpdateSettingsService`/`VideoWebPlayerUpdateBackupService`/`UpdateBackupCoordinator` (`Services/Updates/`).

## Blazor-Komponenten (UI-Muster)

- **`VideoPlayer.razor`** (`Components/Shared/Media/`): `<video @ref src="@StreamUrl">` mit `MediaType`/`MediaId`/`AuthToken`/`StartPositionSeconds`; Stream-URL `/api/items/{type}/{id}/stream?access_token={AuthToken}` (Z. 401); JS-Interop `videoPlayer.*` (reload, getCurrentPosition, setStartPosition, registerTimeUpdate) und `continueWatching.attach`; Fehlerzustand 403/404 mit `MediaAccessErrorMessages.PlaybackFailed`; `PlaylistPlaybackContext`-Parameter für Playlist-Badge/Navigation. **Kein hls.js, kein MSE — nativer Progressive-Download.**
- **`MediaSearchSelector.razor`** (`Components/Playlists/`): Such-Auswahldialog mit Debounce über alle 5 `MediaType`s (Movie, Episode, Season, Show, Collection) — **Vorbild für die Regel-Inhaltsauswahl**; respektiert Zugriffsrechte (E2E `Search_UserWithoutAccess_MediumNotInResults`).
- **Admin-Seiten** (`Components/Pages/Admin/`): `AdminIndex.razor` (`/admin`), `ProgramSettings.razor`, `Backups.razor`, `Devices.razor`, `GenreAdmin.razor`, `Security.razor`, `Updates.razor`, `UserManagement.razor`, `MediaSources/*` — Struktur `admin-console`/`admin-page-header`; Aufnahme einer Regelverwaltung würde hier erfolgen.
- **Playlist-UI** (`Components/Playlists/`): `PlaylistDetail.razor`, `PlaylistsList.razor`, `PlaylistForm.razor`, `PlaylistEntriesList.razor`, `PlaylistGenreEditor.razor`, Cover-Dialogs u. a.
- **`NavMenu.razor`** (`Components/Layout/`): Navigation (`/playlists`, `/admin`, Medienquellen); Admin-Link nur für `IsAdmin`.

## Client-Bibliothek (`VideoWebPlayer.Client`)

- **`VideoWebPlayerClient`** (partiell: `VideoWebPlayerClient.cs`, `.Playlists.cs`, `.Pairing.cs`) + **`IPlaylistApiClient`**: HTTP-Client mit `AuthorizationToken`, Session-Erneuerung (Refresh-Rotation), Device-Token-Header. DTOs unter `Models/` (`DtoPlaylist`, `DtoPlaylistEntry`, `DtoPlaylistPlaybackStart` mit `StreamUrl`, `DtoPlaylistNavigationResult`, `MediaTypeValues`-Konstanten als String-Pendant zu `MediaType`, `PlaylistSortModeValues`).
- **`ApiDocumentationContractTests`** erzwingt, dass die Routen (mind. des `PlaylistsController`) in `docs/API.md` dokumentiert sind — neue Controller müssen die Doku erweitern.
