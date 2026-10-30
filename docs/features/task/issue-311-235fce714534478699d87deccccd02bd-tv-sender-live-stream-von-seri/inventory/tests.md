# Bestandsaufnahme: Tests — Live-Stream-Eingangskette

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-29, Läufe ca. 22:20–23:05 +01:00 (Windows)
- Branch und Commit-ID: `task/issue-311-235fce714534478699d87deccccd02bd-tv-sender-live-stream-von-seri` @ `885c47242647aef2307b6fd1b44b87a8c2af2127` (`fix: Unnötige Verdrängungen im Sendeplan vermeiden (Issue #310) (#312)`)
- Uncommittete Änderungen im getesteten Stand: nur untracked Lifecycle-Artefakte `docs/features/task/issue-311-…/` (Issue-Material, requirement.md, diese Bestandsaufnahme); keinerlei Änderungen an Produktivcode, Tests oder Konfiguration.
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403, xUnit.net v3.2.2, Windows (Git Bash), ffmpeg 9.0.2-full_build (gyan.dev) über `PATH` auffindbar — die ffmpeg-E2E-Tests laufen daher real; Playwright-Chromium-Browser sind installiert (`~/AppData/Local/ms-playwright`).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/staging-ci.yml` (`build-and-test`-Job) — ein Testprojekt `VideoWebPlayer.Tests`, drei Läufe nach `Category`: `Category!=E2E` (Unit), `Category=Integration`, `Category=E2E`. AGENTS.md §7 verlangt zusätzlich `dotnet build VideoPlayer.sln` in Debug **und** Release sowie die volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Hinweis: `Category=Integration` trifft **keine** Tests — im Projekt existieren nur `Trait("Category", "E2E")`-Markierungen (75 Stellen); der Filter ist ein CI-Relikt und liefert „Kein Test entspricht dem Filter" (verifiziert).

### Build-Ausgangszustand

| Befehl | Ergebnis |
|--------|----------|
| `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler, 140 Warnungen (u. a. BL0008-Razor-Warnungen, vorhanden) |
| `dotnet build VideoPlayer.sln -c Release` | 0 Fehler, 140 Warnungen (dieselben) |

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit (Category!=E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E" --logger "trx;LogFileName=unit-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2481 | 0 | 1 | [Log](test-results/unit-tests.log), [TRX](test-results/unit-tests.trx) |
| E2E (Category=E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=E2E" --logger "trx;LogFileName=e2e-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 1 | 243 | 1 | 1 | [Log](test-results/e2e-tests.log), [TRX](test-results/e2e-tests.trx) |
| Integration (Category=Integration) | `dotnet test … -c Release --no-build --filter "Category=Integration"` | Repo-Root | 0 | 0 | 0 | 0 | Filter trifft keinen Test (kein `Category=Integration`-Trait im Projekt); keine Nachweisdatei |
| E2E-Einzel-Wiederholung ×3 | `dotnet test … -c Release --filter "FullyQualifiedName~SecondJob_PlaylistContinuesAndMarksDiscontinuity"` | Repo-Root | 0 (je Lauf) | 1 | 0 | 0 | [Log Lauf 3](test-results/e2e-rerun-3-secondjob.log) (Läufe 1+2 identisch bestanden, siehe Text unten) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.Services.FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` | `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs:127` | `Assert.Null() Failure` — `segmenter.LastUnexpectedExit` war `LiveStreamExitInfo { SlotId = 2, ExitCode = 0, HttpTransportFailure = False }`. Der zweite ffmpeg-Lauf (Input: ~6 s Datei, `-t 60`) endet bei Eingabe-EOF mit Exit 0, bevor der Test `StopAsync` aufruft; `ReportProcessExit` wertet das als unerwarteten Exit (elapsed ≈6–11 s < remaining 60 s − Toleranz 10 s). Zeitabhängige Race zwischen Playlist-Warte-Schleife und Prozessende → flaky unter Parallel-Last. | E2E-Gesamtlauf ([Log](test-results/e2e-tests.log) Zeile ~20); dreimal einzeln wiederholt → 3× bestanden |

**Einordnung:** Vorhandener, zeitabhängiger Flaky-Test im betroffenen Komponentenbereich (ffmpeg-Segmentierung, Exit-Klassifikation). Der Fehlschlag steht nicht in inhaltlichem Zusammenhang mit dem Pipe/`moov`-Problem der Anforderung, berührt aber dieselbe `LastUnexpectedExit`-Logik. Die drei erfolgreichen Einzelwiederholungen verdecken den dokumentierten Gesamtlauf-Fehlschlag nicht.

### Testlücken und Ausführungsprobleme

- `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — im Unit-Lauf **übersprungen** (`NotExecuted`): Unix-only-Test unter Windows; kein Fehler.
- `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — im E2E-Lauf **übersprungen** (`NotExecuted`); Grund im Testcode (`Skip`-Attribut, nicht weiter untersucht).
- `Category=Integration`-CI-Lauf trifft lokal keinen Test (s. o.).
- **Fachliche Testlücke für die Anforderung:** Kein Test prüft den produktiven Fehlerpfad „entfernte Quelle fällt ohne http-Basis-URL still auf `pipe:0` zurück" (`TryCreateStreamUri → null` → `ForStream`) — `LiveStreamMediaResolverTests.ResolveAsync_TokenServiceNull_FallsBackToPipe` deckt zwar den Mechanismus ab, aber es gibt keinen Test, der ein MP4 mit `moov`-Atom am Ende über `pipe:0` an ffmpeg reicht (der einzige Pipe-E2E-Test nutzt bewusst MPEG-TS, weil „MP4 scheiterte hier am moov-Atom am Dateiende", `FfmpegLiveStreamSegmenterE2ETests.cs:159-160`). Ebenso fehlt ein Test für die Pump-Vollständigkeitsdiagnose (gepumpte Bytes vs. Dateigröße — existiert nicht) und für ein Pipe-Eingangsfehler-Fallback im Coordinator (Fallback existiert nur für HTTP-Transportfehler).

## Testklassen

### `FfmpegLiveStreamSegmenterTests`
Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs` — Fake-Prozess-Unit-Tests.

- `StartAsync_FileInput_BuildsExpectedArguments` — Argumentstring mit Dateipfad + `-nostdin`.
- `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` — HTTP-URL als `-i`, `-nostdin`.
- `StartAsync_PipeInput_UsesPipeArgumentAndRedirectsStdin` — `pipe:0` + `RedirectStandardInput`.
- `StartAsync_SetsRunningStateAndSlot` — Laufzustand/SlotId.
- `StopAsync_KillsProcessAndClearsState` — Kill + Zustandsrücksetzung.
- `StopAsync_Kill_IsNotReportedAsUnexpectedExit` — geplanter Kill ≠ `LastUnexpectedExit`.
- `UnexpectedExit_LogsWarningWithExitCodeAndStderrTail` — Warnlog mit Exit-Code/stderr-Tail.
- `UnexpectedExit_MasksStreamTokenInStderr` — Token-Maskierung im Log.
- `UnexpectedExit_HttpInputTransportMarker_SetsHttpTransportFailure` — HTTP-Job + Transportmarker → `HttpTransportFailure`.
- `UnexpectedExit_PipeInputTransportMarker_NoHttpTransportFlag` — **Pipe-Job mit denselben Markern → Flag bleibt `false`** (Kernstelle der Anforderung).
- `ExpectedEndOfRun_NoWarningAndNoUnexpectedExit` — Ende am Slotende = planmäßig.
- `StartAsync_ClearsLastUnexpectedExit` — Neustart setzt Diagnose zurück.
- `StartAsync_SecondJob_StopsPreviousProcess` — Job-Wechsel stoppt Altprozess.
- `StartAsync_FailedStart_ThrowsAndStaysStopped` — `null`-Prozess → Ausnahme.
- `ExpectedPositionSeconds_NoJob_IsNull` — Positionserwartung ohne Job.
- `StopAsync_PipeInputPumpBreak_LogsCompactDebugBeforeStopReturns` — Pipe-Bruch-Log ohne Stacktrace.
- `StopAsync_StalledPump_ReturnsWithinPumpDrainTimeout` — Stallender Pump blockiert Stop nicht.
- `StartAsync_SlotSwitch_CollectsPreviousPump` — Pump-Einhohlen beim Slotwechsel.

### `FfmpegLiveStreamSegmenterE2ETests`
Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` — `[Trait("Category", "E2E")]`, echtes ffmpeg (Skip bei fehlender Binary).

- `RealFfmpeg_ProducesPlaylistAndSegments` — Datei-Input → echte `live.m3u8` + `seg_*.ts`.
- `SecondJob_PlaylistContinuesAndMarksDiscontinuity` — `append_list`/`discont_start` über Job-Wechsel (**flaky, s. oben**).
- `RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` — Pipe-Input mit gedrosselter Quelle; Stop → kompaktes Debug-Log. **Nutzt bewusst `input.ts`, weil MP4 über Pipe am `moov`-Problem scheitert.**

### `LiveStreamMediaResolverTests`
Datei: `VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs` — echtes SQLite (`ProgramScheduleTestDb`).

- `ResolveAsync_LocalSource_ReturnsFilePath` — lokale Quelle → `ForFile`.
- `ResolveAsync_LocalSourceMissingFile_ReturnsNull` — nicht auflösbare Datei → `null`.
- `ResolveAsync_RemoteSource_ReturnsHttpUrl` — entfernte Quelle → `ForHttp`.
- `ResolveAsync_UseHttpInputFalse_ReturnsPipe` — `useHttpInput=false` → `ForStream`.
- `ResolveAsync_TokenServiceNull_FallsBackToPipe` — `TryCreateStreamUri`→`null` → `ForStream` (**der produktive Fehlschlag-Fall**).
- `ResolveAsync_OptionDisabled_FallsBackToPipe` — `UseHttpInput=false` → `ForStream`.
- `ResolveAsync_RunDuration_SizesTokenLifetime` — Token-Lebensdauer = Laufzeit + Marge.
- `ResolveAsync_UnknownItem_ReturnsNull` — unbekanntes Item → `null`.

### `LiveStreamCoordinatorTests`
Datei: `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` — echtes SQLite, `FakeSegmenter`/`FakeGuide`/`FakeLocator`/`StubStreamTokenService`.

- `SynchronizeAsync_RunningSlot_StartsSegmentation`, `..._SameSlotWithinDrift_Unchanged`, `..._SameSlotBeyondDrift_Restarts`, `..._DifferentSlot_RestartsOnNewSlot`, `..._ScheduleGap_StopsAndWakesAtNextStart`, `..._FfmpegMissing_ReportsUnavailable`, `..._UnresolvableMedia_ReportsMediaUnavailable` — Takt-Entscheidungen.
- `SynchronizeAsync_RemoteMedia_UsesHttpInput` — entfernte Quelle → HTTP-Job.
- `SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe` — HTTP-Transportfehler am selben Slot → nächster Versuch Pipe.
- `SynchronizeAsync_NonTransportExit_KeepsHttpInput` — anderer Fehler → HTTP bleibt.
- `SynchronizeAsync_HttpTransportFailureOtherSlot_KeepsHttpInput` — Transportfehler an fremdem Slot → HTTP bleibt.

### `SegmenterProcessSupportTests`
Datei: `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs`

- `StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull`
- `PumpInputAsync_BrokenPipe_LogsCompactDebugWithoutStacktrace`
- `PumpInputAsync_ExpectedStopExceptions_LogWithoutStacktrace` (Theory: `ObjectDisposedException`, `OperationCanceledException`)
- `PumpInputAsync_UnexpectedError_LogsWarningWithException`
- `PumpInputAsync_SourceUnavailable_ReturnsOutcomeWithoutPump`
- `PumpInputAsync_Success_PumpsAndClosesStdin` — prüft Pumpen + stdin-Schließen, **keine Byte-/Vollständigkeitsprüfung**
- `AwaitDrainObservingFaultsAsync_*` (4 Tests) — Timeout/Fault/Result-Verhalten des Drain-Helfers.

### `InternalMediaStreamTokenServiceTests`
Datei: `VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs`

- `TryCreateProbeUri_WildcardAddress_ReturnsLoopbackUriWithResolvableToken`
- `TryNormalizeServerAddress_NormalizesHosts` / `..._NonHttpOrInvalid_ReturnsNull` (Theory)
- `TryCreateProbeUri_OnlyHttpsAddress_ReturnsNull` — **HTTPS-only → `null`** (der Produktiv-Verdachtsfall)
- `TryCreateProbeUri_Disabled_ReturnsNull`
- `TryCreateProbeUri_ConfiguredBaseUrl_WinsOverAutoDetection` / `..._InvalidConfiguredBaseUrl_FallsBackToAutoDetection`
- `ResolveMediaItemId_UnknownToken_ReturnsNull` / `..._ExpiredToken_ReturnsNull`
- `TryCreateProbeUri_NoServer_ReturnsNull`
- `TryCreateStreamUri_CustomLifetime_Honored` / `..._ProbeDisabled_StillIssuesUri` / `..._NoAddress_ReturnsNull` / `..._TinyLifetime_ClampedToMinimum`

### `InternalMediaStreamControllerTests`
Datei: `VideoWebPlayer.Tests/InternalMediaStreamControllerTests.cs` — `WebApplicationFactory`-basiert.

- `Stream_InvalidToken_Returns404`, `Stream_ValidToken_ReturnsFile`, `Stream_ValidToken_RangeRequest_Returns206PartialContent` (**Range-Verarbeitung = Seekbarkeit des HTTP-Eingangs**), `Stream_TokenIsBoundToIssuedItem_UnknownAfterInvalid`, `Stream_FeatureDisabled_ValidToken_Returns404`.

### `LiveStreamStandbySegmenterTests` / `LiveStreamStandbyProviderTests`
Dateien: `VideoWebPlayer.Tests/Services/LiveStreamStandby{Segmenter,Provider}Tests.cs`

- Segmenter: Argumentaufbau/Duration-Cap, Pipe-Input + Pump, Exit≠0/keine Segmente/Timeout-Kill/Kill-Fehlschlag, gesperrtes Alt-Segment, fehlendes ffmpeg.
- Provider: rotierende Playlist, eingebetteter Default, Vorbereitung (Erfolg/Fehlschlag/`null`-Ziel), Disk-Cache-Hit/-Miss inkl. Quelldatei-Stempel, gelöschtes MediaItem mit Cache, Retry-Sturm-Unterdrückung, Transition-Flag.

### `LiveStreamWorkerTests` / `LiveStreamSegmentStoreTests` / `LiveStreamControllerTests` / `LiveStreamE2ETests`
Dateien: `VideoWebPlayer.Tests/{LiveStreamWorkerTests,LiveStreamE2ETests}.cs`, `VideoWebPlayer.Tests/Services/LiveStreamSegmentStoreTests.cs`, `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs`

- Worker: Verzeichnis-Clear vor Startverzögerung + Ticks; Feature-Flag aus → Segmenter stoppen, nie starten.
- SegmentStore: Verzeichnisauflösung (Default/relativ/absolut), Dateinamen-Whitelist + Containment, Retention-Sweep nur `seg_*.ts`, `ClearAsync`.
- Controller: Playlist-Umschreibung mit `access_token`, `.ts`-Auslieferung + `FileShare.Delete`, 401/403/404-Matrix, Standby-Fallback, `no-store`, `#EXT-X-DISCONTINUITY`-Injektion, Feature-Flag.
- `LiveStreamE2ETests` (E2E, `DeviceClientTestBase`): 401 anonym, Playlist/Segment mit `access_token`, Standby-Übergang, konfigurierter Standby-Clip.

### `SftpMediaSourceReaderOpenFileStreamTests`
Datei: `VideoWebPlayer.Tests/Services/SftpMediaSourceReaderOpenFileStreamTests.cs`

- `OpenFileStream_WithEmptyFileName_ReturnsNullWithoutConnecting` / `..._WithWhitespaceFileName_...` — nur Edge-Cases; **kein Test des eigentlichen SFTP-Streamings** (erfordert echten Server).

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync` — echte SQLite-In-Memory-DB mit Migrationen; Seed-Helfer für `MediaSource`/`MediaCollection`/`MediaItem`, Slots, Regeln, Nutzer (`EnsureSourceAsync`, `CreateMediaItemAsync`, `CreateSlotAsync`, `CreateRuleAsync`, `SeedProbeItemAsync`, `LinkMovieMediaItemAsync`, `LinkEpisodeMediaItemAsync`, …).

### `FfmpegLiveStreamSegmenterTests`-Doubles (privat in der Testdatei)
- `FakeSegmenterProcess` — `ISegmenterProcess`-Attrappe mit `SimulateExit`, `UseBrokenPipeStandardInput`, `ControlledPipeStream` für stderr.
- `EndlessReadStream` / `NeverEndingReadStream` — endlos liefernde bzw. stallende Quell-Streams für Pump-Tests.
- `BrokenPipeInputStream` — stdin-Ersatz, der nach `Kill` `IOException` wirft.

### `LiveStreamCoordinatorTests`-Doubles (privat in der Testdatei)
- `FakeGuide`, `FakeLocator`, `FakeSegmenter` (zeichnet `LiveStreamJob`s auf), `StubStreamTokenService`.

### Weitere
- `ListLogger<T>` (`Helpers/ListLogger.cs`) — aufzeichnender `ILogger`.
- `TestWebHostEnvironment` (`Helpers/TestWebHostEnvironment.cs`) — `IWebHostEnvironment` mit ContentRoot aus Temp.
- `E2EBinaryLocator` (`Helpers/E2EBinaryLocator.cs`) — findet ffmpeg/ffprobe für E2E-Tests.
- `ThrottledReadStream` (in `FfmpegLiveStreamSegmenterE2ETests`) — gedrosselte Quelle (~33 KB/s) für den Pipe-Stop-Test.
- `DeviceClientTestBase` (`Helpers/DeviceClientTestBase.cs`) — `WebApplicationFactory`-Basis der E2E-HTTP-Tests.
- `FakeSftpMediaSourceReader` / `ThrowingSftpMediaSourceReader` / `SeriesSftpMediaSourceReader` / `BackfillSftpMediaSourceReader` (`Helpers/`) — SFTP-Reader-Doubles für andere Testbereiche.
