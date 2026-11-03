# Tests (Bestandsaufnahme Issue #333)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** Beginn 2026-11-03 06:29:30 (lokale Zeit, Zeitzone +01:00, laut Log-Zeitstempeln). Testlauf `run1` von ca. 06:29 bis ca. 06:35.
- **Branch und Commit-ID:** `task/issue-333-86d0f0f6e5984dfdb74ebf2ede9e11ff-scan-pipeline-orchestrierung-l`, Commit `e0005003cb98f9339c7f7e8b628400665249d4a1`.
- **Uncommittete Änderungen im getesteten Stand:**
  - Keine geänderten, getrackten Dateien (`git status` zeigt nur ungetrackte Einträge).
  - `?? .claude/`: Verzeichnis, das nicht vom Agenten dieses Laufs angelegt wurde. Nicht verändert.
  - `?? docs/features/task/issue-333-.../`: `requirement.md` und `todo.md` (Vorgängerschritte) sowie die in diesem Lauf erzeugten `inventory.md` und `inventory/`. Am Produktivcode und an den Tests wurde nichts verändert.
- **Testumgebung und Runtime-/SDK-Versionen:**
  - Windows 11 Pro 10.0.26300, Git Bash.
  - .NET SDK `10.0.403`, Zielframework `net10.0`.
  - Playwright-Browser (Chromium) wurden verwendet, Version nicht ermittelt. Die E2E-Tests liefen tatsächlich.
  - Build-Konfiguration: Debug (implizites Build durch `dotnet test`).
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Hauptsuite, xUnit, Playwright). Befehl laut `AGENTS.md` Abschnitt 7 und `.github/workflows/staging-ci.yml`.
  - `tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj`. Im CI nicht gesondert aufgeführt, deshalb zusätzlich ausgeführt.
  - Kein Testprojekt vorhanden für `VideoWebPlayer/`, `VideoWebPlayer.Client/` und `tools/SecretScan/`.
  - `package.json` enthält nur das Skript `release` (semantic-release), keine Tests.
  - CI-Unterschied: `staging-ci.yml` führt die Suite in Release aus, aufgeteilt in `Category!=E2E`, `Category=Integration` und `Category=E2E`. Dieser Lauf führt die Suite in Debug ohne Kategoriefilter aus.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| run1 (Ausgangslauf) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=run1.trx" --results-directory <run1>` (ohne Filter, Debug) | Repository-Wurzel | 1 | 2885 | 1 | 2 | [Konsole](test-results/run1/console.log), [TRX](test-results/run1/run1.trx), [Exit-Code](test-results/run1/exitcode.txt) |
| run2 (Wiederholung Einzeltest) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName~PlaylistSkipLockedEntriesE2ETest"` | Repository-Wurzel | 0 | 1 | 0 | 0 | [Konsole](test-results/run2/console.log), [TRX](test-results/run2/run2.trx), [Exit-Code](test-results/run2/exitcode.txt) |
| run3 (Wiederholung Einzeltest) | wie run2 | Repository-Wurzel | 0 | 1 | 0 | 0 | [Konsole](test-results/run3/console.log), [TRX](test-results/run3/run3.trx), [Exit-Code](test-results/run3/exitcode.txt) |
| run-tools | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --logger "trx;LogFileName=tools.trx"` | Repository-Wurzel | 0 | 6 | 0 | 0 | [Konsole](test-results/run-tools/console.log), [TRX](test-results/run-tools/tools.trx), [Exit-Code](test-results/run-tools/exitcode.txt) |

Zählwerte von run1 laut TRX-Zähler: gesamt 2888, ausgeführt 2886, bestanden 2885, fehlgeschlagen 1, nicht ausgeführt 0, Übersprungen 2 (Differenz zwischen gesamt und ausgeführt). Dauer laut Konsole: 5 m 16 s.

Hinweis zur Benachrichtigung „Exit code 0“ des Hintergrundlaufs: Dieser bezog sich auf den umschließenden Shell-Aufruf. Maßgeblich ist der Exit-Code von `dotnet test` in `exitcode.txt` (1).

Build: Die Testläufe bauten erfolgreich (`VideoWebPlayer.Client`, `VideoWebPlayer`, `VideoWebPlayer.Tests` für Debug). Warnungen wurden nicht gesondert ausgewertet. Ein Release-Build wurde nicht ausgeführt.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs:178` (Hilfsmethode `PlaylistsE2ETestBase.SelectEntryAsync`, `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs:298`) | `System.TimeoutException : Timeout 30000ms exceeded.` Warten auf `Locator("#playlist-detail-selected-entry")` bis sichtbar. | run1 ([TRX](test-results/run1/run1.trx), Ergebnis `Failed`). In den Wiederholungen run2 und run3 grün. |

Einordnung: Der Fehler ist im Ausgangslauf nachgewiesen, bei zwei Einzelwiederholungen aber nicht reproduzierbar. Er gilt damit als instabil (Playwright-Timing unter Last laut `AGENTS.md`). Er ist nicht als stabiler, vorhandener Fehler belegt. Der Testfall liegt außerhalb des Scan-Pfads (Playlist-Wiedergabe).

### Testlücken und Ausführungsprobleme

Übersprungene Tests (2):

| Test | Grund (laut Testcode / TRX) | Umfang |
|------|------------------------------|--------|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Im Testcode explizit übersprungen mit dem Befund: „api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.Create` ohne Null-Prüfung). Wird laut Auftrag in diesem Lauf nicht behoben.“ Der Test wurde in diesem Lauf nicht ausgeführt. | Ein Testfall. Der Befund betrifft den Continue-Watching-Dienst, nicht den Scan-Pfad. |
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Plattformbedingt: „Unix-Dateirechte lassen sich nur unter Unix prüfen.“ (Lauf unter Windows) | Ein Testfall, auf Windows nicht prüfbar. |

Build-, Setup- und Infrastrukturfehler: keine im Lauf festgestellt.

Testlücken im Bereich der Anforderung (Befund aus der Codesuche, keine Bewertung):
- Kein Test ruft `MediaSourceAdmin.razor::RunFullScan` oder `MediaSourceExplorer.razor::RescanCurrent` auf. Suche nach „Komplettscan“, „Neu erfassen“ und „RunFullScan“ findet in den Tests nur Kommentare und die Helfermethode `RescanCollectionAsync`, die den Explorer-Pfad nachbildet.
- Kein Regressionstest, der Worker und manuelle Pfade auf dieselbe Reihenfolge und dieselben Scopes prüft.
- Kein Test für „ein manueller Komplettscan unterdrückt Schedule-Wecken je Commit“.
- `ScanEverythingAsync` und `RescanCollectionAsync` setzen keinen Schedule-Scope, lösen keine Benachrichtigung aus und führen keinen Genre-Reload durch. Die Tests bilden damit den Ist-Zustand der UI-Pfade nur teilweise ab.

## Testklassen

Nur die für die Anforderung relevanten Klassen. Gezählt sind Fakten (`[Fact]`) bzw. Methoden laut Grep.

### `MediaSourceScanServiceTests` (`VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`)
Prüft `MediaSourceScanService.ExecuteAsync` über `TestableMediaSourceScanService`.
- `ExecuteAsync_RunsScanAndClassification_WhenNoCollectionsExist` (Zeile 22). Wartet auf die Logmeldungen „Starte Scan aller Quellen.“ und „Scan aller Quellen abgeschlossen.“ (Zeilen 78–79).
- `ExecuteAsync_AddsMediaItem_WhenSourceContainsMovieFile` (Zeile 87)
- `ExecuteAsync_ClassifiesEpisodes_WhenSeriesStructureExists` (Zeile 165)
- `ExecuteAsync_DetectsNewSeason_WhenEpisodesAddedAfterFirstScan` (Zeile 260)
- `Services_ScannerAndClassifier_IncrementalSeriesProcessing` (Zeile 367)

### `ProgramScheduleSignalTests` (`VideoWebPlayer.Tests/ProgramScheduleSignalTests.cs`)
- `Notify_WakesWaiter`, `Notify_IsCoalesced`, `Notify_DuringScan_IsSuppressed_UntilScanEnds`, `ScanEnd_WithoutSuppressedNotification_DoesNotWake`

### `PlaylistBackfillSignalTests` (`VideoWebPlayer.Tests/Services/PlaylistBackfillSignalTests.cs`)
- `NotifyOutsideAScan_WakesTheWaiter`, `NoSignal_NothingWakes`, `NotifyDuringAScan_WakesOnlyWhenTheScanEnds`, `ScanEnd_WakesEvenWithoutAnyMarker`, `OverlappingScans_WakeOnlyWhenTheLastOneEnds`, `SeveralSignals_AreCoalescedIntoOne`

### `PlaylistBackfillMarkerHookTests` (`VideoWebPlayer.Tests/Services/PlaylistBackfillMarkerHookTests.cs`)
Prüft die Marker-Hooks des DbContexts, die `NotifyMarkersWritten` und `NotifyScheduleChanged` auslösen.
- Unter anderem `WrittenMarkers_NotifyTheSignal_ButNothingMarkedDoesNot` (Zeile 276), `SaveWithoutRelevantEntities_IssuesNoMarkerCommand` (Zeile 182), `RolledBackTransaction_LeavesNoMarkerBehind` (Zeile 195) sowie weitere Fakten zu Markern (insgesamt 16 Testmethoden).

### `MediaDurationProbeWorkerTests` (`VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`)
Vorhandenes Muster für gebündelte Wecksignale, das die Anforderung als Referenz nennt.
- `ExecuteAsync_ProcessesPendingItems`, `ExecuteAsync_FeatureFlagEvaluatedPerCycle`, `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake` (Zeile 178)

### `BackgroundProcessingGateTests` (`VideoWebPlayer.Tests/BackgroundProcessingGateTests.cs`)
- `PauseForRestoreAsync_WaitsForActiveOperationAndBlocksNewOperations`, `PauseForRestoreAsync_RejectsParallelRestoreUntilLeaseIsDisposed`, `PauseForRestoreAsync_CancellationWhileWaitingResumesOperations`

### `LocalMediaPlaylistE2ETests_Backfill` (`VideoWebPlayer.Tests/LocalMediaPlaylistE2ETests_Backfill.cs`)
Nutzt `RescanCollectionAsync` (Explorer-Nachbau).
- `NewEpisodeInLocalDirectory_AfterRescan_IsBackfilledIntoThePlaylist` (Zeile 20), `RescanWithoutNewFiles_LeavesThePlaylistUnchanged` (Zeile 41)

### Weitere Klassen mit Scan-Sequenzen
- `LocalMediaPlaylistE2ETests_Playback`, `_Cover` (je zwei Tests, nutzen `ScanEverythingAsync`)
- `LocalMediaPlaylistE2ETests_SourceDeletion` (`DeletingTheLocalSource_ReplacesThePlaylistBoundContinueWatchingEntry`, `DeletingTheLocalSource_WithoutAnyRemainingTitle_RemovesTheContinueWatchingEntry`). Berührt den Quellen-Löschpfad mit `NotifyScheduleChanged`.
- `LocalMediaSourcePipelineE2ETests` (`LocalSource_ScanAndClassify_ExposesTvShowViaApi`, Sequenz Zeilen 109–111)
- `AgeRatingE2ETests` (`MovieNfoRating_ShowsBadgeOnMovieDetails`, `TvShowNfoRating_ShowsBadge_AndEpisodeInheritsIt`, `Admin_SetsAgeRating_ViaEditDropdown`; Sequenz Zeilen 230–232)
- `Services/MediaSourceScannerLocalTests` (`ScanAllSourcesAsync_CreatesRootCollection_ForLocalSource`, `ScanNextMediaCollection_PopulatesLocalDirectory`, `ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing`, `ScanNextMediaCollection_ChangedFile_ResetsDurationFields`)
- `Services/MediaSourceScannerTests` (`ScanNextMediaCollection_SkipsCollection_WhenRemoteDirectoryIsMissing`)

## Hilfsmethoden

### `LocalMediaPlaylistE2ETestBase` (`VideoWebPlayer.Tests/Helpers/LocalMediaPlaylistE2ETestBase.cs`)
- `ScanEverythingAsync()` (Zeilen 162–179) — Scope nur für `IPlaylistBackfillSignal`, dann `ScanAllSourcesAsync`, `ScanNextMediaCollection`-Schleife, `ClassifyAllAsync`. Kein Schedule-Scope, keine Benachrichtigung, kein Genre-Reload.
- `RescanCollectionAsync(long collectionId)` (Zeilen 187–200) — Nachbau von `RescanCurrent`: Scope, `ScanCollectionTreeAsync`, `ClassifyCollectionTreeAsync`.
- `RunPlaylistBackfillAsync()` (Zeilen 208–212) — ruft `PlaylistBackfillCoordinator.ProcessPendingAsync` direkt auf.

### `TestableMediaSourceScanService` (`VideoWebPlayer.Tests/Helpers/TestableMediaSourceScanService.cs`)
- `RunAsync(CancellationToken)` — ruft die geschützte `ExecuteAsync` des Workers auf.
- Stellt `MediaUpdateNotificationService` mit einem Null-Hub bereit (`NullHubContext`, `NullHubClients`, `NullClientProxy`), damit Benachrichtigungen ohne SignalR laufen.
