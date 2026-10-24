# Bestandsaufnahme: Test-Ausgangszustand

## Rahmendaten

| Feld | Wert |
|---|---|
| Zeitpunkt des Laufs | 2026-10-24, ~16:50–17:05 (lokale Zeit, Windows) |
| Branch | `task/issue-267-d1de0b6aa7e9460587888689975bbdf9-tv-sender-ffmpegffprobe-per-do` |
| Commit | `4108c00e5570669624a3d431c0da97b76ba857aa` („TV-Sender: InternalMediaStreamController an Feature-Flag binden (Issue #259) (#275)") |
| Uncommittete Änderungen | nur untracked: `docs/features/task-issue-267-…/` (Feature-Arbeitsverzeichnis) |
| SDK | .NET SDK 10.0.402 (`C:\Program Files\dotnet\sdk`) |
| Ziel-Runtime | net10.0 |
| Umgebung | Windows, git bash |
| ffmpeg/ffprobe auf dem Testsystem | vorhanden (Locator-E2E-Tests liefen; `FfmpegLiveStreamSegmenterE2ETests` nicht übersprungen) |

## Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| Build (Debug) | `dotnet build VideoPlayer.sln` | Repo-Root | 0 | — | 0 Fehler (142 Warnungen: BL0008 + vereinzelt CS8602) | — | [build-debug.txt](test-results/build-debug.txt) |
| Volle Testsuite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Root | 1 | 2245 | 2 | 1 | [test-run-full.txt](test-results/test-run-full.txt) (Gesamtzeit ~5,0 min) |
| Wiederholung der 2 Fehlschläge | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName=VideoWebPlayer.Tests.ClientLibrary.VideoWebPlayerClientTests_ProgramGuide.PairedDevice_RequestProgramGuideUpcoming_ReturnsBoundedEntries\|FullyQualifiedName=VideoWebPlayer.Tests.ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext"` | Repo-Root | 0 | 2 | 0 | 0 | [test-run-retry-failures.txt](test-results/test-run-retry-failures.txt) |

## Nachgewiesene bestehende Testfehler (Ausgangslauf)

| Test | Suite/Datei | Fehlerbild | Bewertung |
|---|---|---|---|
| `VideoWebPlayer.Tests.ClientLibrary.VideoWebPlayerClientTests_ProgramGuide.PairedDevice_RequestProgramGuideUpcoming_ReturnsBoundedEntries` | `VideoWebPlayer.Tests/ClientLibrary/VideoWebPlayerClientTests_ProgramGuide.cs:79` | `HttpRequestException` 404 Not Found von `RequestProgramGuideUpcomingAsync` | **Flaky unter Volllauf-Parallelität**: im isolierten Wiederholungslauf bestanden. Wahrscheinliches Bild: `api/program-guide`-Endpunkte hängen seit dem letzten Commit (`4108c00`, Feature-Flag-Bindung) am `tvSenderEnabled`-Setup-Schalter; unter parallelen Tests, die das Flag schalten, antwortet der Endpunkt 404. Im Ausgangslauf nachweislich fehlgeschlagen → als preexisting dokumentiert, nicht als stabil. |
| `VideoWebPlayer.Tests.ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext` | `VideoWebPlayer.Tests/ProgramGuideE2ETests.cs:93` | `Assert.Equal` — Expected: `OK`, Actual: `NotFound` | Dasselbe Fehlerbild (404 auf program-guide), im isolierten Lauf bestanden → flaky/preexisting wie oben. |

## Übersprungene Tests

| Test | Grund |
|---|---|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `[Fact(Skip = "…")]` in `DevicePlaylistE2ETests_Playback.cs:76` — dokumentierter Produktiv-Befund: `api/continue-watching` antwortet 500 bei Film ohne Filmsammlung; absichtlich übersprungen, preexisting. |

Zusätzlich „weich übersprungen" (Early-Return ohne Skip-Markierung, zählen als bestanden):
- `FfmpegLiveStreamSegmenterE2ETests` (2 Tests) und `MediaProbeHttpIntegrationTests` (2 Tests) kehren vorzeitig zurück, wenn kein `ffmpeg`/`ffprobe` auf dem System liegt — auf diesem Testsystem liefen sie durch (keine Skips im Log).

## Testklassen im betroffenen Bereich

### `FfprobeLocatorTests` — `VideoWebPlayer.Tests/Services/FfprobeLocatorTests.cs`
`Resolve_ConfiguredPath_Wins`, `Resolve_ConventionDirectory_IsUsed`, `Resolve_PathEnvironment_IsFallback`, `Resolve_NotFound_ReturnsNull`, `Resolve_ConfiguredPathMissing_ReturnsNull`. Nutzt internen Ctor mit `_baseDir`/`_pathDir`-Tempverzeichnissen.

### `FfmpegLocatorTests` — `VideoWebPlayer.Tests/Services/FfmpegLocatorTests.cs`
`Resolve_ConfiguredPathExists_ReturnsConfigured`, `Resolve_ConfiguredPathMissing_ReturnsNull`, `Resolve_ConventionToolsDirectory_ReturnsCandidate`, `Resolve_PathEnvironment_ReturnsMatch`, `Resolve_NothingFound_ReturnsNull`. Deckt die `tools/ffmpeg/`-Konvention bereits ab — Grundlage für den „nach Installation findet Resolve() das Binary"-Regressionstest.

### `MediaDurationProbeServiceTests` — `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs`
24 Tests, u. a. `ProbePendingAsync_NoFfprobe_LeavesItemsUnprobed` (Missing-Binary-Verhalten), lokale/SFTP-/HTTP-Probe-Pfade, Retry-/Attempt-Logik, Parallelität.

### `MediaDurationProbeServiceTests_Progress` — `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs`
9 Tests zur Fortschritts-Aggregation, inkl. `GetProgressAsync_NonAdmin_Throws` (Admin-Erzwingung).

### `MediaDurationProbeServiceTests_Reset` — `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Reset.cs`
8 Tests zu Einzel-/Gesamt-Reset inkl. Non-Admin-Wurf.

### `FfprobeMediaDurationProbeTests` — `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`
~20 Tests: Output-Parsing, Exit-Code-/stderr-Klassifikation, `ProbeFileAsync_NoBinary_ReturnsTransientFailure`, Credential-Maskierung, Timeouts — injizierbarer `executeAsync`-Delegate.

### `MediaDurationProbeWorkerTests` — `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`
`ExecuteAsync_ProcessesPendingItems`, `ExecuteAsync_FeatureFlagEvaluatedPerCycle`.

### `LiveStreamCoordinatorTests` — `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs`
12 Tests, u. a. `SynchronizeAsync_FfmpegMissing_ReportsUnavailable` (deckt `LiveStreamSyncAction.FfmpegUnavailable` ab — nach Installation würde derselbe Takt `Started` liefern), Drift-/Fallback-Logik.

### `LiveStreamWorkerTests` — `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
`ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks`, `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts`.

### `FfmpegLiveStreamSegmenterTests` — `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
~16 Tests mit `FakeSegmenterProcess` (stderr-Pipe-Simulation), Argument-Aufbau, Exit-Klassifikation, Token-/Credential-Maskierung.

### `FfmpegLiveStreamSegmenterE2ETests` — `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
`RealFfmpeg_ProducesPlaylistAndSegments`, `SecondJob_PlaylistContinuesAndMarksDiscontinuity` — echte ffmpeg-Prozesse (Early-Return ohne Binary).

### `MediaProbeHttpIntegrationTests` — `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs`
`HttpProbe_RangeEndpoint_ReturnsDuration`, `InternalStream_RangeRequest_ReturnsPartialContent` — echter Kestrel + echtes ffprobe via `WebApplicationFactory`.

### `TVSenderAdminTests` — `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
bUnit gegen echte SQLite (`ProgramScheduleTestDb`): `RendersFourTabs_WithRulesActive`, `SwitchesTabs_AndShowsProbeDetailTables`, `RuleEditor_*` (3), `Sendeplan_*` (2), `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes`, `ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus`. Hilfsklasse `EmptyListHandler : HttpMessageHandler` (Z. 421). DI-Aufbau in `CreateContextAsync` (Z. 31-61) — dort würde ein künftiger Download-Service registriert/gestubbt werden.

### `TVSenderAdminE2ETests` — `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
Playwright: `Admin_CreateEditDeactivateDeleteRule`, `Admin_ProbeReset_SingleItemAndAllFailed`, `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`.

### `EnumContractTests` — `VideoWebPlayer.Tests/EnumContractTests.cs`
Pinnt `ManualBackupJobStatus`/`RestoreBackupJobStatus`-Wertemengen — ein neuer Download-Status-Enum würde analog pinnbar sein.

## Hilfsmethoden / Test-Doubles

- `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`): Shared-SQLite-Fixture (real SQLite, FKs aktiv), `CreateAsync(dbName, ct)` (setzt `Setup.TvSenderEnabled = true`), `AddUserAsync(email, isAdmin)`, `CreateService`, `CreateGuideService`, `CreateSlotAsync`, `EnsureSourceAsync` u. a.
- `MediaProbeTestDoubles` (`VideoWebPlayer.Tests/Helpers/MediaProbeTestDoubles.cs`): `PresentLocator` (IFfprobeLocator → `"ffprobe"`), `FixedDurationProbe`, `StubMediaSourceReader`.
- `BunitContext` + `ctx.AddAuthorization().SetAuthorized("admin")` mit `IsAdmin`-/`NameIdentifier`-Claims (Muster in `TVSenderAdminTests`).

## Testlücken für diese Anforderung

- Kein Download-/Installations-Service → keine Tests für Download-Fortschritt, Entpackung, Executable-Bit, Zielpfad `tools/ffmpeg/`, Fehlerpfade (HTTP-Fehler, kaputtes Archiv, fehlende Schreibrechte).
- Keine UI-Tests zu „Binary fehlt"-Hinweisen oder Download-Button — die Komponente nutzt die Locators bisher gar nicht.
- Kein Fake-`HttpMessageHandler`-Muster für den neuen Dienst (nur `EmptyListHandler` für `VideoWebPlayerClient`).
- xz/tar-Entpackung ist nicht getestet (kein solcher Code vorhanden); `System.IO.Compression`-Nutzung existiert nur in `Services/Backups/VideoWebPlayerBackupData*.cs` (Backup-Archivformat von msTools.Backup, kein generischer Extractor).
