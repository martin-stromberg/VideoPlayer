# Tests — Bestandsaufnahme Issue #337

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-05, Testlauf ca. 18:47–18:54 Uhr (Mitteleuropäische Zeit, UTC+01:00)
- Branch und Commit-ID: `task/issue-337-d8572aba5a3946358cf750455ba7e092-sendeplan-jeder-commit-mit-sen`, Commit `b2e41085ab88c9d23e73f4d78e33116fde2d94b0` („Filme einer Filmsammlung in Releasedatum-Reihenfolge senden (Issue #334) (#339)")
- Uncommittete Änderungen im getesteten Stand: keine Code-Änderungen; nur das ungetrackte Arbeitsverzeichnis `docs/features/task/issue-337-…/` (Anforderung/Bestandsaufnahme selbst). `git status --porcelain` zeigte genau einen Eintrag (`?? docs/features/task/issue-337-…/`).
- Testumgebung und Runtime-/SDK-Versionen: Windows 11 (10.0.26300), .NET SDK 10.0.403, Laufzeit Microsoft.NETCore.App 10.0.14, xunit.v3 3.2.2 mit VSTest-Adapter 3.1.5, Microsoft.Playwright 1.62.0 (Chromium lokal installiert: `chromium-1248` u. a. unter `%USERPROFILE%\AppData\Local\ms-playwright`), Testprojekt `net10.0`, Konfiguration Debug (Default von `dotnet test`).
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - Projektüblicher Befehl (AGENTS.md, Definition of Done): `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vom Repo-Root — führt die **gesamte** Suite aus (Unit + Integration + E2E).
  - CI-Aufteilung (`.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml`): drei Läufe in Release — `--filter "Category!=E2E"` (blockierend, mit Coverage-Gate 70 %), `--filter "Category=Integration"`, `--filter "Category=E2E"`. Kategorisierung über `[Trait("Category", "E2E")]` an den E2E-Klassen.
  - `release.yml`: voller Lauf ohne Filter als Release-Gate.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (vollständige Suite) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=baseline.trx" --logger "console;verbosity=normal"` | Repo-Root `D:\Repositories\softwareschmiede\d8572aba-5a39-4635-8cf7-50455ba7e092` | 1 | 2903 | 1 | 2 | [baseline-console.log](test-results/baseline-console.log), [baseline.trx](test-results/baseline.trx) |
| Wiederholung (nur der fehlgeschlagene Test) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName~PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest" --logger "trx;LogFileName=baseline-retry-playlistskip.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 1 | 0 | 0 | [baseline-retry-playlistskip-console.log](test-results/baseline-retry-playlistskip-console.log), [baseline-retry-playlistskip.trx](test-results/baseline-retry-playlistskip.trx) |

Gesamtdauer des Baseline-Laufs: 6,5615 Minuten. Anmerkung: Die Zähler differieren zwischen Konsolausgabe (2906 gesamt = 2903 bestanden + 1 nicht bestanden + 2 übersprungen) und TRX (`executed="2904"`, `passed="2903"`, `failed="1"`).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs` (Z. 178, via `Helpers/PlaylistsE2ETestBase.cs` Z. 298 `SelectEntryAsync`) | `System.TimeoutException : Timeout 30000ms exceeded.` — Playwright wartete auf `Locator("#playlist-detail-selected-entry")` visible. Dauer 44 s. | Baseline-Lauf: [baseline.trx](test-results/baseline.trx), Log-Zeilen ~1420/1435. **Wiederholungslauf einzeln: bestanden** (10 s, Exit 0) — Fehlerbild entspricht dem in AGENTS.md beschriebenen gelegentlichen Playwright-Flackern unter Last, kein reproduzierbarer Defekt. |

Der Baseline-Fehlschlag steht **nicht** in Beziehung zur Anforderung (Playlist-Wiedergabe-E2E, kein Sendeplan-/Signal-Bezug) und war im Einzel-Nachlauf nicht reproduzierbar.

### Testlücken und Ausführungsprobleme

Übersprungene Tests (2):

| Test | Grund |
|------|-------|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Absichtlich übersprungen per `[Fact(Skip = "…")]` (`DevicePlaylistE2ETests_Playback.cs` Z. 76): dokumentierter, nicht behobener Befund — `api/continue-watching` antwortet mit HTTP 500, wenn ein Film zu keiner Filmsammlung gehört. |
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Plattformbedingt: „Unix-Dateirechte lassen sich nur unter Unix prüfen." |

Keine Build-, Setup- oder Infrastrukturfehler; Playwright/Chromium war verfügbar und die E2E-Suite lief vollständig durch. Es wurde weder Produktivcode noch Tests oder Testkonfiguration verändert.

## Testklassen

### `ProgramScheduleSignalTests`
Datei: `VideoWebPlayer.Tests/ProgramScheduleSignalTests.cs`

- `Notify_WakesWaiter` — `NotifyScheduleChanged` weckt einen wartenden `WaitAsync` sofort.
- `Notify_IsCoalesced` — zwei Meldungen ohne Leser koaleszieren zu einer Weckung.
- `Notify_DuringScan_IsSuppressed_UntilScanEnds` — Meldung im `BeginScan`-Scope weckt nicht; Scope-Ende weckt genau einmal.
- `ScanEnd_WithoutSuppressedNotification_DoesNotWake` — Scope ohne unterdrückte Meldung weckt nicht.

Kein Test existiert für `_liveChannel`/`WaitLiveAsync` — die Live-Leitung des Signals ist ungetestet.

### `ProgramScheduleWorkerTests`
Datei: `VideoWebPlayer.Tests/ProgramScheduleWorkerTests.cs`

- `Worker_RunsEnsureCoverage_OnStartup` — Worker materialisiert Slots nach Start.
- `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased` — Feature-Flag aus → keine Slots.
- `Worker_Run_PurgesExpiredRules` — Lauf bereinigt abgelaufene Einmal-Regeln.
- `Worker_DisabledFeature_SkipsPurge` — Flag aus → auch Purge pausiert.
- `Worker_Run_CleansSelfDisplacedDuplicates` — Lauf entfernt bewiesene Selbst-Verdrängungs-Duplikate.

Alle Tests instanziieren den Worker **ohne** `signal`-Parameter — das Signal-Aufwachen des Workers ist nicht getestet.

### `MediaDurationProbeWorkerTests`
Datei: `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`

- `ExecuteAsync_ProcessesPendingItems` — Worker probt anstehende Items selbständig.
- `ExecuteAsync_FeatureFlagEvaluatedPerCycle` — Flag wird je Zyklus gelesen.
- `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake` (Issue #290) — Backlog-Schleife im `BeginScan`-Scope: drei Batch-Signale → genau ein Wecken am Scope-Ende; verifiziert per `GatedScheduleSignal` (zählt `BeginScanCount`/`NotifyCount`, hält `Dispose` offen). Das ist der zentrale Nachweis des **bestehenden** Bündelverhaltens innerhalb von Scopes.
- `GatedScheduleSignal` (private Test-Double-Klasse) — implementiert `IProgramScheduleSignal` vollständig (inkl. `NotifyLiveScheduleChanged`, `WaitLiveAsync`), delegiert an echtes `ProgramScheduleSignal`, hält Scope-Dispose kontrolliert offen.

### `MediaDurationProbeServiceTests`
Datei: `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs` (u. a.)

- `ProbePendingAsync_SuccessfulProbe_NotifiesScheduleSignal` — geschriebene Duration → 1× `NotifyCalls`.
- `ProbePendingAsync_FailedProbe_DoesNotNotifyScheduleSignal` — Fehlprobe → 0× `NotifyCalls`.
- `ProbePendingAsync_LegacyFailedItem_IsPickedUpAgain` — Altbestand wird wieder geprüft → 1× `NotifyCalls`.
- `FakeScheduleSignal` (private Test-Double-Klasse) — `IProgramScheduleSignal` mit `NotifyCalls`-Zähler, `BeginScan` = No-Op, `WaitAsync`/`WaitLiveAsync` nie zurückkehrend.

### `PlaylistBackfillMarkerHookTests`
Datei: `VideoWebPlayer.Tests/Services/PlaylistBackfillMarkerHookTests.cs`

16 Tests gegen echtes SQLite zum Markierungs-Hook (Dedupe, Transaktion, Rollback, InMemory-Fallback, SQL-Kommando-Anzahl). Relevante Lücke: `WrittenMarkers_NotifyTheSignal_ButNothingMarkedDoesNot` verifiziert nur `IPlaylistBackfillSignal` (`CountingSignal`); `NewContext` übergibt **keinen** `IProgramScheduleSignal` — das Schedule-Signal-Feuern des `SaveChanges`-Hooks (Z. 85/94/133/142 der partiellen Datei) ist **nicht testbedeckt**.

### `MediaSourceScanCoordinatorScopeTests`
Datei: `VideoWebPlayer.Tests/Services/MediaSourceScanCoordinatorScopeTests.cs`

- `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` — simuliertes `NotifyScheduleChanged` während des Scans: 0 Weckungen bei offenem Scope, genau 1 nach Scope-Ende (`GatedMetadataWriteCoordinator` als Haltepunkt).
- `RunFullScanAsync_WaitsForRestoreLease_ThenRuns` — Gate-Lease-Blockade während Restore.

### `MediaSourceScanServiceTests`
Datei: `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`

- `ExecuteAsync_RunsScanAndClassification_WhenNoCollectionsExist`, `ExecuteAsync_AddsMediaItem_WhenSourceContainsMovieFile`, `ExecuteAsync_ClassifiesEpisodes_WhenSeriesStructureExists`, `ExecuteAsync_DetectsNewSeason_WhenEpisodesAddedAfterFirstScan`, `Services_ScannerAndClassifier_IncrementalSeriesProcessing` — Worker-Scanpfade.
- `Worker_AndManualFullScan_ProduceSameResult` — Regressionstest (Issue #333): Worker- und Coordinator-Lauf liefern identische Ergebnisse inkl. `BackfillWakeCount`/`ScheduleWakeCount` (`WakeCounter` auf `WaitAsync` beider Signale).

### `PlaylistBackfillSignalTests` (Referenz-Signal, nicht betroffen)
Datei: `VideoWebPlayer.Tests/Services/PlaylistBackfillSignalTests.cs`

- `NotifyOutsideAScan_WakesTheWaiter`, `NoSignal_NothingWakes`, `NotifyDuringAScan_WakesOnlyWhenTheScanEnds`, `ScanEnd_WakesEvenWithoutAnyMarker`, `OverlappingScans_WakeOnlyWhenTheLastOneEnds`, `SeveralSignals_AreCoalescedIntoOne` — das Schwester-Signal inkl. abweichender Scan-Ende-Weckbedingung.

### `LiveStreamWorkerTests`
Datei: `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`

- `ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks`, `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts`, `ExecuteAsync_UndecodableSlotSkips_LogsInputUnplayableOnce`, `ExecuteAsync_MediaUnavailableAfterGap_LogsInformationAgain`, `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` — Worker-Ticks ohne Signal-Parameter; das `WaitLiveAsync`-Aufwachen ist nicht getestet.

### `TVSenderAdminTests` / `TVSenderAdminE2ETests` u. a.
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`, `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` (+ `TVSenderAdminE2ETests_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminDayBarE2ETests`, `TVSenderAdminValidityE2ETests`)

- `TVSenderAdminTests` registriert `IProgramScheduleSignal` = `new ProgramScheduleSignal()` im bUnit-Kontext (Z. 56).
- `TVSenderAdminE2ETests` (Z. 605 ff.): „Jetzt generieren" → Plan-Neuaufbau über das Signal; wartet den materialisierten Slot per DB-Polling ab (kommentiert: laufender Scan kann das Signal unterdrücken — die 4-s-Wartezeit der Seite ist keine Garantie).

### `ApplicationDbContextTests`
Datei: `VideoWebPlayer.Tests/ApplicationDbContextTests.cs`

- `DeleteMediaSourceAsync_*` (9 Tests: Kaskadenlöschung, Event-Publishing, Fremdquellen-Unversehrtheit u. a.) — verifizieren `MediaSourceDeletedEvent`, aber **nicht** `NotifyScheduleChanged` nach dem Commit (Z. 448).

### `ProgramRuleServiceTests_*` / `ProgramClipGroupServiceTests`
Dateien: `VideoWebPlayer.Tests/ProgramRuleServiceTests_*.cs`, `VideoWebPlayer.Tests/ProgramClipGroupServiceTests.cs`

Umfangreiche CRUD-/Fachlogik-Tests. **Lücke:** Keine dieser Dateien injiziert ein `IProgramScheduleSignal` — die `NotifyScheduleChanged`-Aufrufe in `ProgramRuleService` (4 Stellen) und `ProgramClipGroupService` (5 Stellen) sind nicht testbedeckt.

### `ProgramScheduleConventionTests`
Datei: `VideoWebPlayer.Tests/ProgramScheduleConventionTests.cs`

- `DirectPassStepCalls_OnlyInApprovedStepTests` — Quellcode-Scan: Pass-Einzelschritte (`EnsureCoverageAsync`, `ReconcileAsync`, `PurgeExpiredRulesAsync`, `CleanupSelfDisplacedSlotsAsync`) dürfen in Tests nur in der freigegebenen Dateiliste direkt aufgerufen werden; alles andere muss über `ExecutePassAsync` laufen. Für neue Signal-/Bündeltests relevant, falls sie Planläufe simulieren.

## Hilfsmethoden

### `MediaSourceScanCoordinatorTestServices` (`VideoWebPlayer.Tests/Helpers/`)
- `Build(connectionString, messages, gate, playlistBackfillSignal, programScheduleSignal, notificationService, writeCoordinator, timeProvider, mediaSourceReader)` — Service-Provider um Shared-Memory-SQLite für Coordinator-Tests; beide Signale standardmäßig als Singleton (`ProgramScheduleSignal` ↔ `IProgramScheduleSignal`), austauschbar.
- `SeedAsync(serviceProvider, rootDirectory, genresChanged, ct)` — legt `Setup` + lokale `MediaSource` an.

### `GatedMetadataWriteCoordinator` (`VideoWebPlayer.Tests/Helpers/`)
- `Hold`/`Release`/`WaitUntilEnteredAsync` — hält den Classifier-Schreibsperren-Eintritt fest: deterministischer Haltepunkt, während der der Coordinator-Scope noch offen ist.

### `WakeCounter` (privat in `MediaSourceScanServiceTests.cs` Z. 742)
- `IAsyncDisposable`, zählt erfolgte `WaitAsync`-Erweckungen eines Signals im Hintergrund-Task.

### `MediaProbeTestDoubles` (`VideoWebPlayer.Tests/Helpers/`)
- `PresentLocator` (`IFfprobeLocator`, immer auflösbar), `FixedDurationProbe` (`IMediaDurationProbe`, feste Dauer).

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/`)
- Test-DB-Hülle der `ProgramScheduleServiceTests_*`; `CreateRuleAsync` nutzt `ProgramRuleService.ApplyValidity` (internal, via `InternalsVisibleTo`).

### `PrimetimeSeed` (`VideoWebPlayer.Tests/Helpers/`)
- `StepAsync` u. a. — standardisierter `ExecutePassAsync`-Einstieg für Plan-Tests (Convention-konform).

### `ListLogger<T>`, `TestableMediaSourceScanService`, `TestHelpers`, `IncrementingTimeProvider`, `ManualTimeProvider`, `ControllableTimeProvider` (`VideoWebPlayer.Tests/Helpers/`)
- Logging-Queue, Worker-Testable, Warteschleifen-Helfer (`WaitForMessageCountAsync`, `WaitForTvShowEpisodeCountAsync`) und steuerbare `TimeProvider` für Worker-Tests.
