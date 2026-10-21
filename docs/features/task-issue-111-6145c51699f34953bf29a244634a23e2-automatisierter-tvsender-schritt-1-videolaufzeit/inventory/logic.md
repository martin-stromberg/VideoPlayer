# Logik (Services / Worker)

## `MediaSourceScanner`
Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync` | `public` | Legt Root-`MediaCollection`s je `MediaSource` an/aktualisiert sie |
| `ScanNextMediaCollection` | `public` | Scannt die älteste fällige Collection (`ScanDueAt`) |
| `ScanMediaCollectionAsync` | `public` | Scannt eine bestimmte Collection |
| `ScanCollectionTreeAsync` | `public` | Scannt einen Collection-Baum (BFS) |
| `ScanMediaCollectionInternalAsync` | `private` | Eigentlicher Scan: legt neue `MediaCollection`s/`MediaItem`s an; bei `existingItem.CreatedAt != file.CreatedAt` → `CreatedAt` aktualisieren, `Changed = true`, `ClassifiedAt = null` → **Einrastpunkt für „Dateiwechsel ⇒ Laufzeit neu prüfen"** |

Abhängigkeiten: `ApplicationDbContext`, `IMediaSourceReader`, `ProgramSettingsService`, `ILogger`, `TimeProvider` (testbar).

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (~1750 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync` / `ClassifyMediaItemsAsync` / `ClassifyMediaCollectionsAsync` / `ClassifyCollectionTreeAsync` | `public` | Klassifizierungs-Einstiegspunkte (statischer Einprozess-Lock + Queue) |
| `ProcessMediaItemsAsync` | `private` | Markiert `MediaItem`s als klassifiziert (`ClassifiedAt`, `Changed=false`) |
| `ProcessMediaCollectionsAsync` | `private` | TVShow-/Movie-Erkennung je Collection |
| `ProcessCollectionAsTVShowAsync` / `ProcessCollectionAsMovieAsync` | `private` | Erzeugt/verknüpft `Movie`/`TVShowSeason`/`TVShowEpisode` über NFO; Video-Dateien über `fileExtensions_Video` = `{ ".mp4", ".avi", ".mkv", ".mpeg" }` (Zeile 40) gefiltert |
| `BackfillMissingActorsAsync` | `public` | Backfill-Muster: Items ohne `ActorsClassifiedAt` nachziehen (vom `ActorBackfillWorker` genutzt) |

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs`

`BackgroundService`: führt `DataUpgradeManager.EnsureUpToDateAsync` aus, dann Intervall-Loop (`ProgramSettingsService.GetScanIntervalsAsync` → ScanProcessInterval, Default 1 h): `ScanAllSourcesAsync` → `ScanNextMediaCollection` (max. 64/Lauf) → `ClassifyMediaItemsAsync`/`ClassifyMediaCollectionsAsync`. Nutzt `IBackgroundProcessingGate.EnterOperationAsync("Automatischer Scanprozess")`.

## `ActorBackfillWorker`
Datei: `VideoWebPlayer/Services/ActorBackfillWorker.cs`

`BackgroundService`-Muster für einmalige Nachtragsbefüllung: einmal nach Start `classifier.BackfillMissingActorsAsync` unter `IBackgroundProcessingGate`-Lease, danach `Task.Delay(Timeout.Infinite)`.

## `PlaylistBackfillWorker` / `PlaylistBackfillCoordinator` / `PlaylistBackfillSignal`
Dateien: `VideoWebPlayer/Services/PlaylistBackfill*.cs`

Erweitertes Backfill-Muster: Signal-Weckung (`IPlaylistBackfillSignal.WaitAsync`), Settle-Time, täglicher Sicherheitslauf (`GetSafetySweepDueAtAsync`/`RunSafetySweepIfDueAsync`).

## `LocalMediaSourceReader` / `SftpMediaSourceReader` / `MediaSourceReaderDispatcher`
Dateien: `VideoWebPlayer/Services/{Local,Sftp}MediaSourceReader.cs`, `MediaSourceReaderDispatcher.cs`

Dateizugriff pro Quelltyp. `OpenFileStream(collection, fileName)` liefert lesenden Stream: lokal `FileStream` (Pfad case-insensitiv unter Quell-Root aufgelöst, ReparsePoints abgelehnt), SFTP `SftpStreamWrapper` (streamt über eigene `SftpClient`-Verbindung). `MediaItem.Path` ist bei lokalen Quellen bereits der absolute Dateipfad; `MediaItem.Name` ist der Dateiname für `OpenFileStream`.

## `MediaMetadataWriteCoordinator`
Datei: `VideoWebPlayer/Services/MediaMetadataWriteCoordinator.cs`

Serialisiert Metadaten-Schreibzugriffe (`EnterAsync` → `IAsyncDisposable`-Lease); vom Classifier genutzt.

## `IBackgroundProcessingGate` / `BackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/BackgroundProcessingGate.cs`

Gate für Hintergrundoperationen (blockiert/geblockt während Backup-Restore); `EnterOperationAsync(name, ct)` → Lease.

## `ProgramSettingsService`
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

DB-persistente Programmeinstellungen über `Setup`-Zeile: `GetOrCreateSetupAsync`, `GetScanIntervalsAsync`, `UpdateScanIntervalsAsync`, `UpdateGeneralSettingsAsync` u. a.

## `DataUpgradeManager`
Datei: `VideoWebPlayer/Services/DataUpgradeManager.cs`

`CurrentVersion = 8`, schrittweise `Upgrade_n`-Methoden beim Start (aus `MediaSourceScanService` aufgerufen). Enthält bereits `MarkAllMediaItemsAsChangedAsync` — Muster für eine einmalige „alle Items neu markieren"-Aktion.

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

| Element | Beschreibung |
|---------|--------------|
| `OptionalRestoreTables` | Tabellen, die in Alt-Backups fehlen dürfen |
| `OptionalRestoreColumns` | `"Tabelle.Spalte"`, die in Alt-Backups fehlen dürfen (fehlende Spalte → beim Insert nicht enthalten → DB-Default/`NULL`) |
| `OptionalRestoreBoolDefaults` / `...LongDefaults` / `...IntDefaults` | Fehlende Spalten mit festem Ersatzwert statt `NULL` |
| `IgnoredRestoreColumns` | Alt-Spalten, die im Payload ignoriert werden |
| `WriteToAsync` / `ReadFromAsync` | Export/Restore als ZIP (`index.json` + `entities/*.json`); Validierung in `ValidatePayloadAsync` |
| `ToDbValue` | JSON→CLR-Mapping beim Restore; `TimeSpan` läuft über den `JsonSerializer`-Fallback |

## DI-Registrierung
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (`AddVideoWebPlayerServices`)

Hosted Services: `MediaSourceScanService`, `ContinueWatchingWorker`, `ActorBackfillWorker`, `PlaylistBackfillWorker`, `MdnsAdvertiserWorker` (nicht in `Testing`-Umgebung). Options: `services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"))` bzw. `services.AddOptions<T>().Bind(...).ValidateOnStart()` für `EpisodeBackgroundImageOptions`, `MdnsOptions`, `DiscoveryOptions`.
