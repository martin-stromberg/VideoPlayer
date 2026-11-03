# Logik (Bestandsaufnahme Issue #333)

Hinweis: Die Anforderung nennt `src/`. Das Repository hat kein `src/`-Verzeichnis; der Produktivcode liegt unter `VideoWebPlayer/`, die Tests unter `VideoWebPlayer.Tests/`.

## `MediaSourceScanService`
Datei: `VideoWebPlayer/Services/MediaSourceScanService.cs` (Klasse Zeile 13, Konstruktor 35–53, `ExecuteAsync` 60–213)

Ist ein `BackgroundService` und der Worker, dessen Sequenz die Anforderung als Referenz nennt.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | `protected override` | Führt beim Start optional `DataUpgradeManager.EnsureUpToDateAsync` aus (nur ohne `skipUpgrade`), wartet `initialDelay`, dann Schleife mit `loopDelay`. Startet einen Scanlauf, sobald das Intervall (`ProgramSettingsService.GetScanIntervalsAsync`, Default 1 h) abgelaufen ist. |
| `.ctor` | `public` | Parameter: `IServiceProvider`, `EventManager`, `MediaUpdateNotificationService`, `ILogger`, optional `initialDelay` (Default 10 s), `loopDelay` (Default 1 min), `skipUpgrade`, `TimeProvider`. |

Abonnierte Events: keine.
Publizierte Events: keine direkten. Wecksignale laufen über die Scopes der Signale (siehe unten). `_eventManager` wird gespeichert, im Lauf (`ExecuteAsync`) aber nicht verwendet.

### Worker-Reihenfolge eines Scanlaufs (Referenzsequenz)

| Nr. | Zeile | Schritt |
|----|-------|---------|
| 1 | 101 | `IPlaylistBackfillSignal.BeginScan()` (per `GetService`, optional) |
| 2 | 103 | `IProgramScheduleSignal.BeginScan()` (per `GetService`, optional) |
| 3 | 104 | `IBackgroundProcessingGate.EnterOperationAsync("Automatischer Scanprozess")` (Gate optional) |
| 4 | 112–113 | Zählung `MediaItems` vor dem Scan |
| 5 | 116 | `MediaSourceScanner.ScanAllSourcesAsync` |
| 6 | 121–125 | Zählung nach dem Scan, Differenz = `newItemsCount` |
| 7 | 129 | `MediaUpdateNotificationService.NotifyNewVideosScannedAsync(0L, newItemsCount)` |
| 8 | 132 | `MediaSourceClassifier.CheckReloadGenres` |
| 9 | 134–141 | Schleife `MediaSourceScanner.ScanNextMediaCollection`, max. 64 je Lauf (`maxCollectionsPerRun`, fest codiert) |
| 10a | 143–167 | Wenn mindestens eine Collection gescannt: `ClassifyMediaItemsAsync` (146). Wenn `unclassifiedBefore > 0`: `ClassifyMediaCollectionsAsync` (161) und `NotifyNewVideosScannedAsync(0L, unclassifiedBefore)` (166) |
| 10b | 169–186 | Wenn keine Collection gescannt: `ClassifyMediaCollectionsAsync` (180) und `NotifyNewVideosScannedAsync(0L, unclassifiedBefore)` (185) |

Fehlerbehandlung: `OperationCanceledException` bei Stop beendet die Schleife; andere Exceptions werden geloggt, die Schleife läuft weiter.

Die Scopes (Schritte 1 und 2) werden vor dem Gate (Schritt 3) geöffnet. Die Benachrichtigung (Schritt 7) erfolgt vor Genre-Reload und vor der Collection-Schleife.

## `MediaSourceScanner`
Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs` (Klasse Zeile 18, Konstruktor um Zeile 40)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync(CancellationToken)` | `public async Task` (Zeile 52) | Setzt je Quelle `LastScannedAt`, liest das Root-Verzeichnis und legt die Root-Collection an (`ScanDueAt = now`, `Classifyable = false`) oder aktualisiert sie, wenn `CreatedAt` abweicht. Scannt keine Unterordner, sendet keine Benachrichtigung, hat keinen Cap. |
| `ScanNextMediaCollection(CancellationToken)` | `public async Task<bool>` (105) | Wählt die älteste fällige Collection (`ScanDueAt <= now`) und scannt sie. Gibt `false` zurück, wenn keine fällig ist. |
| `ScanMediaCollectionAsync(long, CancellationToken)` | `public async Task<bool>` (130) | Scannt eine einzelne Collection per Id. |
| `ScanCollectionTreeAsync(long, CancellationToken)` | `public async Task<int>` (154) | Vollständiger Scan einer Collection und aller (vorhandenen und neu gefundenen) Unter-Collections. Gibt die Anzahl zurück. |
| `ScanMediaCollectionInternalAsync(...)` | `private` (206) | Gemeinsamer Kern der Einzelscans. |
| `UpdateParentClassifyableAsync(...)` | `private` (298) | Aktualisiert `Classifyable` des Elternknotens. |

Aufrufer:
- `MediaSourceScanService`: `ScanAllSourcesAsync` (116), `ScanNextMediaCollection` (137).
- `MediaSourceAdmin.razor::RunFullScan`: `ScanAllSourcesAsync`, `ScanNextMediaCollection` (ungedeckelte Schleife).
- `MediaSourceExplorer.razor::RescanCurrent`: `ScanCollectionTreeAsync` (263).
- Tests: `ScanEverythingAsync`, `RescanCollectionAsync`, `AgeRatingE2ETests`, `LocalMediaSourcePipelineE2ETests`, `MediaSourceScannerLocalTests`, `MediaSourceScannerTests`.

Events: keine.

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs` (Klasse Zeile 24)

Schutz: Die Klassifizierung läuft über ein statisches Flag `_classificationRunning` (`TryBeginClassification`, Zeile 187). Ist es belegt, kehren `ClassifyAllAsync`, `ClassifyMediaItemsAsync` und `ClassifyMediaCollectionsAsync` ohne Fehler zurück (Lauf wird übersprungen). `ClassifyCollectionTreeAsync` stellt die Root-Id in eine Warteschlange und gibt `false` zurück.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync(CancellationToken)` | `public async Task` (90) | Klassifiziert alle MediaItems und MediaCollections (Items, dann Collections). |
| `ClassifyMediaItemsAsync(CancellationToken)` | `public async Task` (116) | Nur MediaItems. |
| `ClassifyMediaCollectionsAsync(CancellationToken)` | `public async Task` (141) | Nur MediaCollections. |
| `ClassifyCollectionTreeAsync(long, CancellationToken)` | `public async Task<bool>` (168) | Klassifiziert eine Collection mit Unterbaum. Gibt `true` zurück, wenn ausgeführt, `false` bei Einreihung in die Warteschlange. |
| `FinishClassificationAsync(CancellationToken)` | `private` (193) | Arbeitet die Warteschlange der Collection-Bäume ab und gibt danach das Flag frei. |
| `TryBeginClassification()` | `private static` (187) | Atomares Setzen des Flags. |
| `TryBeginClassificationForCollectionTree(long)` | `private static` (212) | Startet oder reiht ein. |
| `EnterMetadataWriteAsync(CancellationToken)` | `private` (190) | Holt die Schreibsperre `IMediaMetadataWriteCoordinator` (optional). |
| `ClassifyCollectionTreeCoreAsync(long, CancellationToken)` | `private` (250) | Items und Collections eines Baums, unter Schreibsperre. |
| `GetCollectionTreeIdsAsync(long, CancellationToken)` | `private` (262) | Sammelt die Ids des Teilbaums. |
| `ProcessMediaItemsAsync` / `ProcessMediaCollectionsAsync` | `private` (296/299, 334/392) | Eigentliche Verarbeitung, jeweils mit oder ohne Id-Liste. |
| `CheckReloadGenres(CancellationToken)` | `internal async Task` (2072) | Liest `Setup.GenresChanged`. Ist es gesetzt, wird das Flag zurückgesetzt, gespeichert und `ReloadGenres` ausgeführt. Bei Fehler wird das Flag wieder gesetzt. Läuft ohne `_classificationRunning`-Schutz. |
| `ReloadGenres(CancellationToken)` | `public async Task` (1456) | Lädt die Genres neu. |
| `BackfillMissingActorsAsync` / `BackfillMissingAgeRatingsAsync` | `public async Task` (1759, 1909) | Nachträgliche Ergänzungen, außerhalb des Scan-Pfads. |
| `PublishStatus(string)` | `private` (289) | Meldet Statustexte. Ziel wurde nicht untersucht. |

Abonnierte Events: keine. Publizierte Events: siehe `PublishStatus`.

Aufrufer:
- `MediaSourceScanService`: `CheckReloadGenres` (132), `ClassifyMediaItemsAsync` (146), `ClassifyMediaCollectionsAsync` (161, 180).
- `MediaSourceAdmin.razor::RunFullScan`: `ClassifyAllAsync` (247), `CheckReloadGenres` (251).
- `MediaSourceExplorer.razor::RescanCurrent`: `ClassifyCollectionTreeAsync` (267). Der Rückgabewert (`bool`) wird nicht ausgewertet.

Hinweis (Beobachtung, keine Änderung): In `CheckReloadGenres` wird im `catch`-Zweig `setup` ohne Null-Prüfung verwendet (Zeilen 2086–2087).

## Wecksignale und Scopes

### `ProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (Klasse Zeile 62)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyScheduleChanged()` | `public` (83) | Während mindestens ein Scope offen ist: nur zählen (`_suppressedNotifications`). Sonst sofort wecken (Kanal `_channel` und `_liveChannel`). |
| `BeginScan()` | `public` (107) | Erhöht `_activeScans`, liefert einen `IDisposable`-Scope. |
| `NotifyLiveScheduleChanged()` | `public` (104) | Weckt nur den `_liveChannel`. |
| `WaitAsync(CancellationToken)` | `public` (114) | Wartet auf `_channel` (laut Interface-Dokumentation: `ProgramScheduleWorker`). |
| `WaitLiveAsync(CancellationToken)` | `public` (118) | Wartet auf `_liveChannel` (laut Interface-Dokumentation: `LiveStreamWorker`). |
| `EndScan()` | `private` (121) | Beim letzten Scope-Ende: wecken, nur wenn mindestens eine Benachrichtigung unterdrückt wurde. |

Abonnierte Events: keine. Publiziert: keine klassischen Events, nur Kanäle.

### `PlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs` (Klasse Zeile 40)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyMarkersWritten()` | `public` (51) | Während eines Scans: kein Wecken. Sonst wecken. |
| `BeginScan()` | `public` (60) | Erhöht `_activeScans`, liefert Scope. |
| `WaitAsync(CancellationToken)` | `public` (67) | Wartet auf den Kanal (`PlaylistBackfillWorker`). |
| `EndScan()` | `private` (70) | Beim letzten Scope-Ende: immer wecken. |

### Auslöser der Wecksignale im Datenbankkontext
- `VideoWebPlayer/Data/ApplicationDbContext.cs`, Zeile 448: Nach dem Commit der Quellenlöschung (Transaktion) wird `NotifyScheduleChanged()` aufgerufen. Die Datei liegt nicht in UTF-8 vor (Windows-1252); der Editor verweigert deshalb Änderungen.
- `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs`, Zeilen 84–85, 93–94, 132–133, 141–142: Nach jedem Speichern, das Backfill-Marker schreibt, werden `NotifyMarkersWritten()` und `NotifyScheduleChanged()` aufgerufen. Ohne Marker (Zeilen 108–109 bzw. 136–137) kein Aufruf.

Befund zur Anforderung: Der Sendeplan wird nicht nur bei „sendeplan-relevanten“ Commits geweckt, sondern bei jedem Commit mit Backfill-Markern. Außerhalb eines Scans wirkt dies je Commit. Innerhalb eines Scans wird es unterdrückt, solange ein Scope offen ist.

### `BackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/BackgroundProcessingGate.cs` (Interface in `IBackgroundProcessingGate.cs`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsPausedForRestore` | `public bool` (16) | `true`, wenn ein Restore die Hintergrundverarbeitung angehalten hat. |
| `EnterOperationAsync(string, CancellationToken)` | `public async` (36) | Erwirbt eine Lease für eine benannte Operation; wartet, solange ein Restore läuft. |
| `PauseForRestoreAsync(CancellationToken)` | `public async` (59) | Wird von `VideoWebPlayerBackupRestoreGuard.EnterRestoreAsync` aufgerufen. |

Aufrufer der Lease (`EnterOperationAsync`): `ContinueWatchingWorker`, `ActorBackfillWorker`, `LiveStreamWorker`, `MediaDurationProbeWorker`, `MediaMetadataEditorService`, `MediaSourceScanService` (104, Name „Automatischer Scanprozess“), `PlaylistBackfillCoordinator` (230), `ProgramScheduleWorker` (69, „Sendeplan“), `MediaSourceAdmin.razor` (229, „Manueller Komplettscan“), `MediaSourceExplorer.razor` (260, „Manueller Collection-Scan“).

Die UI-Pfade prüfen `IsPausedForRestore` vor dem Start (Admin Zeile 216, Explorer Zeile 247) und zeigen dann eine Statusmeldung.

### `MediaUpdateNotificationService`
Datei: `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` (Klasse Zeile 9)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NotifyNewVideosScannedAsync(long sourceId, int count, CancellationToken)` | `public async Task` (51) | Sendet die SignalR-Meldung zu neuen Videos. |
| `NotifyStatusAsync` (32), `NotifyContinueWatchingUpdatedAsync` (76), `NotifyFavoritesChangedAsync` (95), `NotifyMediaUpdatedAsync` (113) | `public async Task` | Weitere Meldungen, nicht Teil des Scan-Pfads. |

Aufrufer: `MediaSourceScanService` (129, 166, 185). Die Razor-Seiten `MediaSourceAdmin.razor` und `MediaSourceExplorer.razor` rufen den Dienst nicht auf.

## Die beiden manuellen Einstiege (Ist-Zustand)

### `MediaSourceAdmin.razor::RunFullScan`
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` (Zeilen 211–266)

Injiziert: `IPlaylistBackfillSignal` (Zeile 12), `BackgroundGate`, `MediaSourceScanner`, `MediaSourceClassifier`. Kein `IProgramScheduleSignal`, kein `MediaUpdateNotificationService`.

Ablauf:
1. Zeile 216: `IsPausedForRestore`-Prüfung, bei `true` Statusmeldung.
2. Zeile 229: Gate-Lease „Manueller Komplettscan“.
3. Zeile 231: `PlaylistBackfillSignal.BeginScan()`.
4. Zeile 232: `ScanAllSourcesAsync`.
5. Zeilen 237–243: `ScanNextMediaCollection`-Schleife ohne Cap, mit `Task.Yield` und Statusaktualisierung.
6. Zeile 247: `ClassifyAllAsync`.
7. Zeile 251: `CheckReloadGenres`.
8. Zeile 254: `LoadSources`. Fehler werden in Zeilen 256–260 als Status angezeigt.

Abweichungen gegenüber dem Worker (faktisch belegt):
- Kein Schedule-Scope.
- Keine Benachrichtigung `NotifyNewVideosScannedAsync`.
- Gate wird vor den Scopes geöffnet (Worker: Scopes zuerst).
- Genre-Reload nach der Klassifizierung (Worker: vor der Collection-Schleife).
- Keine Obergrenze für Collections (Worker: 64).
- Klassifizierung nur über `ClassifyAllAsync` (Worker: `ClassifyMediaItemsAsync` plus bedingt `ClassifyMediaCollectionsAsync`).

### `MediaSourceExplorer.razor::RescanCurrent`
Datei: `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` (Zeilen 242–287)

Ablauf:
1. Zeile 247: `IsPausedForRestore`-Prüfung.
2. Zeile 260: Gate-Lease „Manueller Collection-Scan“.
3. Zeile 262: `PlaylistBackfillSignal.BeginScan()`.
4. Zeile 263: `ScanCollectionTreeAsync(currentCollection.Id)`.
5. Zeile 267: `ClassifyCollectionTreeAsync(currentCollection.Id)`, Rückgabe nicht ausgewertet.
6. Zeilen 271–276: Aktuelle Collection neu laden.

Abweichungen gegenüber dem Worker (faktisch belegt): Kein Schedule-Scope, keine Benachrichtigung, kein Genre-Reload.

## Test-Helfer, die die Sequenz nachbauen

Siehe [tests.md](tests.md#hilfsmethoden). Kurz:
- `LocalMediaPlaylistE2ETestBase.ScanEverythingAsync` (`VideoWebPlayer.Tests/Helpers/LocalMediaPlaylistE2ETestBase.cs`, Zeilen 162–179): Scope nur für `IPlaylistBackfillSignal`, dann `ScanAllSourcesAsync`, `ScanNextMediaCollection`-Schleife, `ClassifyAllAsync`. Ohne Schedule-Scope, ohne Benachrichtigung, ohne Genre-Reload.
- `LocalMediaPlaylistE2ETestBase.RescanCollectionAsync` (Zeilen 187–200): Nachbau von `RescanCurrent` (Scope, `ScanCollectionTreeAsync`, `ClassifyCollectionTreeAsync`).
- `AgeRatingE2ETests.cs` (Zeilen 230–232) und `LocalMediaSourcePipelineE2ETests.cs` (Zeilen 109–111): Eigene Sequenz aus `ScanAllSourcesAsync`, `ScanNextMediaCollection`-Schleife und `ClassifyAllAsync`, ohne Scope.
