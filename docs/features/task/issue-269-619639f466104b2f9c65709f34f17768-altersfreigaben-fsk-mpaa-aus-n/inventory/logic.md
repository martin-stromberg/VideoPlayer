# Logik — Bestandsaufnahme

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (1910 Zeilen)

Zentrale Scan-/Klassifizierungslogik. Konstruktor-Abhängigkeiten (Zeilen 59–79): `ApplicationDbContext`, `IMediaSourceReader`, `RecentEntryService`, `EventManager`, `EpisodeBackgroundImageService`, `ILogger<MediaSourceClassifier>`, `HttpClient` (nur für Schauspieler-Bild-Downloads), optional `MediaUpdateNotificationService`, `IMediaMetadataWriteCoordinator`.

Registrierung: `services.AddScoped<MediaSourceClassifier>()` (`ServiceCollectionExtensions.cs:239`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync(CancellationToken)` | `public` | Gesamte Klassifizierung (Items + Collections), unter `TryBeginClassification`-Sperre und Write-Coordinator-Lease |
| `ClassifyMediaItemsAsync(CancellationToken)` | `public` | Nur MediaItem-Klassifizierung |
| `ClassifyMediaCollectionsAsync(CancellationToken)` | `public` | Nur Collection-Klassifizierung |
| `ClassifyCollectionTreeAsync(long, CancellationToken)` | `public` | Klassifizierung eines Collection-Baums (bei „bereits laufend" wird die ID gequeuet) |
| `BackfillMissingActorsAsync(CancellationToken)` | `public` | **Vorbild für Rating-Backfill**: iteriert `Movies`/`TVShowEpisodes` mit `ActorsClassifiedAt == null`, liest NFOs via `TryReadActorNfoAsync`, ruft `AssignActorsTo*Async`, überspringt `IsManuallyEdited` |
| `ProcessCollectionAsTVShowAsync(MediaCollection, CancellationToken)` | `private` | Prüft `tvshow.nfo` (Zeile 450), parst XML, ruft `CreateOrUpdateTVShow` + `ProcessEpisodesForTVShowAsync` |
| `CreateOrUpdateTVShow(MediaCollection, XElement, CancellationToken)` | `private` | Legt `TVShow` an/aktualisiert; `LoadFromXml` nur wenn `!IsManuallyEdited` (Zeilen 503, 512–516); Genre-Zuordnung ebenfalls geschützt (Zeile 520) |
| `ProcessEpisodesForTVShowAsync(TVShow, CancellationToken)` | `private` | Liest pro Videodatei `<name>.nfo` (Zeile 584), parst `season`/`episode`/`title`, legt Staffel/Episode an bzw. aktualisiert **inline** (Zeilen 654–684; `TVShowEpisode.LoadFromXml` wird nicht genutzt); `IsManuallyEdited`-Schutz Zeilen 675, 752 |
| `ProcessCollectionAsMovieAsync(MediaCollection, CancellationToken)` | `private` | Liest pro Videodatei nur `<name>.nfo` (Zeilen 805–810) — **kein `movie.nfo`-Fallback im Scan-Pfad** (Fallback existiert nur im Actor-Backfill); erfordert Root-Element `<movie>` (Zeile 823); `LoadFromXml` bei Neuanlage (848) bzw. Update nur wenn `!IsManuallyEdited` (858–862); Genre- (867) und Actor-Zuordnung (896) ebenfalls geschützt |
| `LinkTrailerMediaItemsAsync` / `EnsureTrailerLinkAsync` / `IsTrailerFileName` / `IsTrailerFolder` | `private` | Trailer-Konventionen (`-trailer`-Suffix, `trailers`-Ordner) |
| `AssignPicturesToMovieAsync` / `AssignPicturesToMovieCollectionAsync` / `AssignPicturesToTVShowAsync` / `AssignPicturesToTVShowSeasonAsync` / `AssignPicturesToTVShowEpisodeAsync` / `SetPictureAndMarkBackgroundForUpdateAsync` / `GetOrCreatePicture` | `private` | Bildzuordnung aus Mediendateien |
| `ParseActorInfo(XElement)` | `private` | Parst `<actor>`-Elemente (Name, Role, Order, Thumb) |
| `GetOrCreateActorsAsync` / `AssignActorsToMovieAsync` / `AssignActorsToTVShowEpisodeAsync` | `private` | Schauspieler aus NFO anlegen/zuordnen; setzen `ActorsClassifiedAt` |
| `DownloadActorPictureAsync(Actor, string, MediaCollection, CancellationToken)` | `private` | **Einzige Verwendung des `HttpClient`**: lädt Schauspieler-Thumbs aus http(s)-URLs (NFO `<thumb>`), sonst lokale Datei via `_reader`; Fehler nur `LogDebug`, kein Abbruch |
| `TryReadActorNfoAsync(MediaItem, IEnumerable<string>, CancellationToken)` | `private` | NFO-Lesung mit Kandidatenliste (`<name>.nfo`, dann `movie.nfo` bzw. `tvshow.nfo`) und Aufstieg zu Parent-Collections (Zeilen 1863–1888) |
| `ReloadGenres` / `GetOrCreateGenresAsync` / `ResolveGenresAsync` / `FindConfiguredTemplateAsync` / `CheckReloadGenres` | `private`/`internal` | Genre-Normalisierung inkl. kanonischer Auflösung über `GenreGlobalConfig` |
| `GetCommonPrefix`, `ConvertStreamToByteArray`, `DiscardPendingChanges`, `PublishStatus` | `private`/`static` | Hilfsfunktionen |

Publizierte Events: `BackgroundProcessingStatusEvent` via `EventManager.Publish` in `PublishStatus` (Zeile 287); SignalR-Benachrichtigungen via `_notificationService.NotifyStatusAsync`.
Abonnierte Events: keine (der Classifier publiziert nur).

**Wichtige Details zur Anforderung:**
- `IsManuallyEdited` wirkt eintragsweit: Bei gesetztem Flag werden `Name`, `LoadFromXml`-Felder, Genre- und Actor-Zuordnung nicht aktualisiert — erfüllt „kein Überschreiben manuell gepflegter Werte" auf Eintragsebene (nicht feldeben).
- Der reguläre Scan liest bei Filmen **ausschließlich** `<videodatei>.nfo`; `movie.nfo` wird nur in `BackfillMissingActorsAsync` als Kandidat berücksichtigt. Für `tvshow.nfo` greift die Serienerkennung auf Collection-Ebene.
- Fehler pro Collection werden gefangen (Zeilen 364–377, 420–433): `_db.ChangeTracker.Clear()`, Collection als klassifiziert markiert, weiter mit nächster — ein Exception-Pfad in einer Online-Ergänzung würde hier landen.
- Serialisierung: `_classificationRunning` + `Interlocked` — nur ein Klassifizierungslauf gleichzeitig; Schreibzugriffe laufen unter `IMediaMetadataWriteCoordinator`-Lease.

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

`BackgroundService` (Hosted Service, `AddHostedService<MediaSourceScanService>()`). Führt `DataUpgradeManager.EnsureUpToDateAsync` beim Start aus, dann Scan-Schleife: `MediaSourceScanner.ScanAllSourcesAsync`/`ScanNextMediaCollection` → `ClassifyMediaItemsAsync`/`ClassifyMediaCollectionsAsync`. Scanintervall aus `Setup` via `ProgramSettingsService.GetScanIntervalsAsync`. Fehler werden geloggt, Schleife läuft weiter.

## `ActorBackfillWorker`
Datei: `VideoWebPlayer/Services/ActorBackfillWorker.cs`

`BackgroundService` (`AddHostedService<ActorBackfillWorker>()`, Zeile 327): ruft einmalig beim Start `MediaSourceClassifier.BackfillMissingActorsAsync` auf (unter `IBackgroundProcessingGate`-Lease). Vorbild für eine evtl. einmalige Rating-Nacherfassung (Offene Frage 5).

## `MediaMetadataEditorService`
Datei: `VideoWebPlayer/Services/MediaMetadataEditorService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGenreOptionsAsync(CancellationToken)` | `public` | Distinkte Genre-Namen für Auswahllisten |
| `UpdateAsync(MediaMetadataUpdateRequest, CancellationToken)` | `public` | Dispatcher über `NormalizeType(request.ObjectType)`: `movie`, `moviecollection`, `tvshow`, `tvshowseason`, `tvshowepisode`; läuft unter `IBackgroundProcessingGate` + `IMediaMetadataWriteCoordinator` |
| `UpdateMovieAsync` / `UpdateTVShowAsync` / `UpdateTVShowEpisodeAsync` | `private` | Setzen `Name`, `ReleaseDate`, `Plot` (Film/Serie/Episode) + Genres (nur Film/Serie), immer `IsManuallyEdited = true` |
| `UpdateMovieCollectionAsync` / `UpdateTVShowSeasonAsync` | `private` | `Name` + `ReleaseDate` bzw. `PremieredAt`, `IsManuallyEdited = true` |
| `ApplyMovieGenresAsync` / `ApplyTVShowGenresAsync` / `NormalizeGenresAsync` | `private` | Genre-Normalisierung inkl. `GenreGlobalConfig`-Vorlagen |
| `ValidateCommon` / `ValidateNoPlotOrGenres` / `ValidateReleaseDateOnly` / `ValidatePremieredAtOnly` / `NormalizeType` / `NormalizeDate` / `NormalizeText` | `private static` | Validierung (Titel ≤512, Plot ≤10000, Datumsfeld je Typ) |

**Kein Freigabe-Feld** im Editierpfad; neue Felder müssten in `MediaMetadataUpdateRequest`, den `Update*Async`-Methoden und `MetadataEditModel` ergänzt werden (Offene Frage 3).

## `ItemsController`
Datei: `VideoWebPlayer/Controllers/ItemsController.cs` (`[Route("api/[controller]")]`, Basis `ApiBaseController`)

Relevante Endpunkte:

| Endpunkt | Methode | Beschreibung |
|----------|---------|--------------|
| `GET api/items` | `Get` | Paged `MediaEntryDto`-Liste; Filter: `mediaSourceId`, `search` (`ApplySearchFilter` via `AppDbFunctions.LowerInvariant` + `LIKE`), `genreId`, `includeIndividualMediaTypes`; Zugriff über `MediaSourceUsers` + `IUnlockedMediaService`-Freischaltungen |
| `GET api/items/{type}/{id}` | `Get` | Detail-DTO (`DtoMovieCollection` mit `Movies`, `DtoTVShow` mit `Seasons`/`Episodes`, `DtoTVShowEpisode` mit `Season`/`Show`, `DtoMovie` mit `Collection`); DTO-Befüllung über `ApiBaseController.Create<T>` (namensgleiche Property-Kopie per Reflection — neue Properties werden **automatisch** übernommen, sobald Entity und DTO das Feld gleich benennen); `IsUnlocked`/`IsFavorite`/`WatchedAt`-Anreicherung |
| `POST api/items/metadata` | `UpdateMetadata` | Admin-only (`IsAdmin`-Claim, sonst 403 via `ForbiddenAccessException`), ruft `MediaMetadataEditorService.UpdateAsync` |
| `GET api/items/genres` | `GetGenres` | Genre-Optionen |
| `GET api/items/recent`, `GET {type}/{id}/stream`, `GET {type}/{id}/download` | – | Weiterschauen/Streaming |

`FindEntry` (Zeile 505) löst `type` → Entity (`movie`, `moviecollection`, `tvshow`, `tvshowseason`, `tvshowepisode`); `EnsureAccessAsync`/`IsUnlockedAsync` prüfen serverseitig Zugriff.

## `ApiBaseController`
Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

`Create<T>(object)` (Zeilen 75–89): kopiert alle DTO-Properties ohne `IgnoreAssignPropertyAttribute` namensgleich von der Entity. Damit propagiert eine neue Entity-Eigenschaft automatisch ins Detail-DTO, wenn sie dort gleich heißt.

## `VideoWebPlayerClient`
Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

Relevante Methoden: `RequestMovieCollectionAsync`, `RequestMovieAsync`, `RequestTVShowAsync`, `RequestTVShowEpisodeAsync` (`api/items/{type}/{id}`), `RequestItemsAsync`/`RequestSourceItems...` (`api/items`, gemeinsamer `RequestItemsCoreAsync`-URL-Bau Zeile 694–698), `RequestGenreOptionsAsync` (`api/items/genres`), `SaveMetadataAsync(MediaMetadataUpdateRequest)` (`POST api/items/metadata`).

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

`IBackupData`-Implementierung: Export (`WriteToAsync`) und Restore (`ReadFromAsync`) aller EF-Tabellen; `GetTables()` (Zeile 1144) liest das Modell aus `_db.Model` — neue Spalten werden automatisch exportiert, müssen aber in `OptionalRestoreColumns` eingetragen werden, damit Alt-Backups restaurierbar bleiben (siehe models.md).

## `DataUpgradeManager`
Datei: `VideoWebPlayer/Services/DataUpgradeManager.cs`

Daten-Level-Upgrades beim Start (`CurrentVersion = 9`, `Setup.DataVersion`-gesteuert). Aufgerufen aus `MediaSourceScanService.ExecuteAsync` vor der ersten Scan-Iteration.

## Normalisierungslogik / Online-Quelle
**Nicht vorhanden.** Es gibt keine Klasse `AgeRatingNormalizer` o. ä., keine TMDB-/IMDb-Anbindung, keinen dedizierten HTTP-Service für Metadaten-Quellen. Der einzige ausgehende HTTP-Zugriff ist `DownloadActorPictureAsync` (NFO-Thumb-URLs) bzw. `FfmpegToolDownloadService` (ffmpeg-Pakete, eigener named `HttpClient`).
