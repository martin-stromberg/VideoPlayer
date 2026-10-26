# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-26, Testlauf ca. 14:24–14:29 +0100 (Mitteleuropäische Zeit)
- Branch und Commit-ID: `task/issue-266-5b50eb3176084182b85b8f729e910919-tv-sender-warm-up-phase-nach-a`, Commit `7d8c9b5a10445e724c2f896c2449ea1bb53b4c46` („Medienquellen: Merkmal 'Im Menue ausblenden' fuer Utility-Quellen (Issue #284) (#288)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Dateien unter `docs/features/task/issue-266-…/` (Aufgaben-Dokumentation inkl. dieser Bestandsaufnahme); keine Änderungen an Produktiv- oder Testcode
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash); .NET SDK 10.0.402, Laufzeit .NET 10.0.14; ffmpeg 9.0.2 im PATH (echte ffmpeg-E2E-Tests liefen); Playwright-Chromium installiert (Browser-E2E-Tests liefen real, `SkipBrowser` nicht ausgelöst); Test-Host-Umgebung `Testing` (dort ist `LiveStreamWorker` nicht als Hosted Service registriert)
- Ermittelte Testsuiten und Quellen der Testbefehle: Eine Testsuite `VideoWebPlayer.Tests` (xunit.v3, `VideoWebPlayer.Tests.csproj`, net10.0). Voller Befehl laut `AGENTS.md` Abschnitt 7: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Die CI (`.github/workflows/staging-ci.yml`, `pr-staging-ci.yml`) splittet denselben Lauf in `--filter "Category!=E2E"`, `--filter "Category=Integration"` und `--filter "Category=E2E"` (Release-Workflow: ungefiltert). E2E-Tests tragen `[Trait("Category", "E2E")]`; ffmpeg-E2E-Tests kehren still zurück, wenn kein ffmpeg im PATH liegt; Playwright-Tests setzen `SkipBrowser`, wenn Chromium nicht startet.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| Baseline Debug (volle Suite, ungefiltert) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --no-build --logger "trx;LogFileName=inventory-baseline-debug.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/issue-266-…/inventory/test-results` | Repo-Wurzel | 0 | 2446 | 0 | 2 | [console-debug.log](test-results/console-debug.log), [inventory-baseline-debug.trx](test-results/inventory-baseline-debug.trx) |

Vorbereitender Build: `dotnet build VideoPlayer.sln -c Debug` — 0 Fehler, 141 Warnungen (Bestand, u. a. BL0008 in Account-Seiten, eine CS8602 in `LiveStreamE2ETests.cs:75`).

### Nachgewiesene bestehende Testfehler

Keine. Im dokumentierten Ausgangslauf schlug kein Test fehl (0 Fehlgeschlagen).

### Testlücken und Ausführungsprobleme

Übersprungene Tests im Baseline-Lauf (beide mit dokumentiertem Grund, keine Ausführungsfehler):

| Test-ID | Grund |
|---|---|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Bewusst übersprungen (`Skip`): bekannter Bestandefehler — `api/continue-watching` antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehört; laut Skip-Begründung außerhalb des Auftrags. |
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Plattformbedingt übersprungen: Unix-Dateirechte nur unter Unix prüfbar; Lauf erfolgte unter Windows. |

Anforderungsbezogene Testlücken (Was die vorhandenen Tests **nicht** abdecken):

- Keine Tests zu einer Warm-up-/Standby-Auslieferung — ein solcher Mechanismus existiert im Produktivcode nicht.
- Keine Tests für den Zeitraum zwischen Worker-Start (`ClearAsync`) und dem ersten echten Segment: `LiveStreamWorkerTests.ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` prüft nur, dass das Leeren und der erste Tick laufen, nicht was währenddessen ausgeliefert wird.
- `Cache-Control` wird nur auf der `.m3u8`-Antwort geprüft (`GetFile_M3u8_InjectsAccessTokenIntoSegmentUris`, `PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist`); für `.ts`-Segment-Antworten existiert kein Cache-Header-Test (und im Code kein Header).
- `GetFile_InvalidOrMissing_ReturnsNotFound` deckt den 404-Pfad für fehlende `live.m3u8` ab — genau den Pfad, den die Anforderung durch die Standby-Auslieferung ersetzen will; dieser Test wird bei Umsetzung voraussichtlich angepasst/erweitert.
- Der `_initialDelay`-Default von 10 s ist nicht direkt getestet (beide Worker-Tests injizieren `initialDelay: TimeSpan.Zero`); die Reihenfolge Delay → `ClearAsync` → erster Tick ist nur implizit durch den Code gegeben.
- Kein Test für das Verhalten, wenn vor dem ersten Tick noch Altsegmente/Altplaylist des Vorlaufs im Verzeichnis liegen (Clear läuft erst nach `_initialDelay`).

## Testklassen

### LiveStreamControllerTests

Datei: `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs` (xUnit, `IAsyncLifetime`; Fixture: `ProgramScheduleTestDb` (SQLite), echter `LiveStreamSegmentStore` + `ProgramSettingsService`, `FakeAuthService`, Temp-Content-Root)

- `GetFile_M3u8_InjectsAccessTokenIntoSegmentUris` — Playlist-Rewrite setzt `?access_token=` (escaped) in alle Segment-URIs; `#`-Zeilen bleiben unberührt; Content-Type `application/vnd.apple.mpegurl`; `Cache-Control: no-store`.
- `GetFile_M3u8WithoutToken_LeavesUrisUntouched` — ohne Query-Token keine URI-Änderung.
- `GetFile_TsSegment_ReturnsVideoMp2t` — `.ts`-Auslieferung als `FileStreamResult` mit `video/mp2t`.
- `GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` — Auslieferungs-Stream trägt `FileShare.ReadWrite | FileShare.Delete` (Zweitgriff + Löschen während offener Auslieferung, Inhalt vollständig lesbar).
- `GetFile_InvalidOrMissing_ReturnsNotFound` (Theory) — `../evil.ts`, `sub/evil.ts`, `a b.ts`, `not-there.ts` und **fehlende `live.m3u8`** → 404.
- `GetFile_UserWithoutTvAccess_Returns403` — Benutzer ohne `TvSenderUserAccess`-Zeile → `ForbidResult`.
- `GetFile_AdminWithoutTvAccessRow_ReturnsFileOr404Not403` — Admin implizit freigegeben (kein 403).
- `GetFile_NotLoggedIn_Returns401` — ohne `CurrentUser` → `UnauthorizedObjectResult`.

### LiveStreamCoordinatorTests

Datei: `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` (`IAsyncLifetime`; Fakes: `FakeGuide`, `FakeLocator`, `FakeSegmenter`, `StubStreamTokenService`; echte SQLite-DB via `ProgramScheduleTestDb`, echter `LiveStreamMediaResolver`/`LiveStreamSegmentStore`/`ProgramSettingsService`)

- `SynchronizeAsync_RunningSlot_StartsSegmentation` — laufender Slot → `Started`, `LiveStreamJob` mit korrekten Feldwerten (SlotId, Dateipfad, Position, Restlaufzeit, Segmentlänge 6, ffmpeg-Pfad).
- `SynchronizeAsync_SameSlotWithinDrift_Unchanged` — Drift ≤ 30 s → `Unchanged`, kein neuer Job.
- `SynchronizeAsync_SameSlotBeyondDrift_Restarts` — Drift > 30 s → `Started` mit neuer Position.
- `SynchronizeAsync_DifferentSlot_RestartsOnNewSlot` — Slot-Wechsel → Neustart.
- `SynchronizeAsync_ScheduleGap_StopsAndWakesAtNextStart` — `Now == null` → `Stopped`, `WakeAtUtc` = nächster Slot-Start, Stop-Aufruf.
- `SynchronizeAsync_FfmpegMissing_ReportsUnavailable` — `Resolve() == null` → `FfmpegUnavailable` + Stop.
- `SynchronizeAsync_UnresolvableMedia_ReportsMediaUnavailable` — unbekannte MediaItemId → `MediaUnavailable`.
- `SynchronizeAsync_RemoteMedia_UsesHttpInput` — SFTP-Quelle → `HttpUrl`-Eingang.
- `SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe` — `LastUnexpectedExit.HttpTransportFailure` am selben Slot → Pipe-Eingang, kein neuer Token-Call.
- `SynchronizeAsync_NonTransportExit_KeepsHttpInput` — Nicht-Transport-Fehler → HTTP bleibt.
- `SynchronizeAsync_HttpTransportFailureOtherSlot_KeepsHttpInput` — Transportfehler an anderem Slot → HTTP bleibt.

### LiveStreamWorkerTests

Datei: `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs` (`IAsyncLifetime`; echter DI-Container mit Fakes; `initialDelay: TimeSpan.Zero` injiziert)

- `ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` — Altsegment `seg_stale.ts` wird gelöscht, danach läuft ein Sync-Tick (Stop-Aufruf am `FakeSegmenter`).
- `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` — `TvSenderEnabled = false` → laufender Segmenter wird gestoppt, nie gestartet.

### LiveStreamSegmentStoreTests

Datei: `VideoWebPlayer.Tests/Services/LiveStreamSegmentStoreTests.cs` (deklariert zusätzlich ein lokales `internal TestWebHostEnvironment` im Namespace `VideoWebPlayer.Tests.Services`)

- `GetDirectoryAsync_Default_UsesDataLiveStreamUnderContentRoot` — Standardpfad `Data/LiveStream` + Anlage.
- `GetDirectoryAsync_ConfiguredRelative_ResolvesAgainstContentRoot` — relativer Konfigurierungspfad → Content-Root.
- `GetDirectoryAsync_ConfiguredAbsolute_UsedAsIs` — absoluter Pfad unverändert.
- `TryResolveFileAsync_ExistingFile_ReturnsPath` — existierende Datei auflösbar.
- `TryResolveFileAsync_InvalidOrMissing_ReturnsNull` (Theory) — Traversal/Unterverzeichnis/Leerzeichen/leer/fehlend → `null`.
- `SweepExpiredSegmentsAsync_DeletesOnlyOldSegments` — nur alte `seg_*.ts` gelöscht, Playlist bleibt.
- `ClearAsync_RemovesEverything` — Verzeichnis komplett leer.

### FfmpegLiveStreamSegmenterTests

Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs` (Fake-Prozess `FakeSegmenterProcess` über injizierbare `_startProcess`-Fabrik; `ControlledPipeStream` für stderr; `ListLogger`)

- `StartAsync_FileInput_BuildsExpectedArguments` — komplette Argumente: `-re`, `-ss`, `-nostdin`, libx264/aac, `-hls_time 6`, `-hls_list_size 12`, `delete_segments`, `append_list`, `discont_start`, `program_date_time`, Segment-Muster `seg_\d{14}_42_%05d.ts`, `-t`, Ziel `live.m3u8`.
- `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` — HTTP-URL als `-i`, kein stdin-Redirect.
- `StartAsync_PipeInput_UsesPipeArgumentAndRedirectsStdin` — `pipe:0` + stdin-Redirect.
- `StartAsync_SetsRunningStateAndSlot` — `IsRunning`/`CurrentSlotId`/`ExpectedPositionSeconds` gesetzt.
- `StopAsync_KillsProcessAndClearsState` — Kill + Zustandsreset.
- `StopAsync_Kill_IsNotReportedAsUnexpectedExit` — absichtlicher Kill → `LastUnexpectedExit` bleibt `null`, kein Warning.
- `UnexpectedExit_LogsWarningWithExitCodeAndStderrTail` — vorzeitiges Ende → `LiveStreamExitInfo` (SlotId, ExitCode, kein HTTP-Flag) + Warning mit Tail.
- `UnexpectedExit_MasksStreamTokenInStderr` — Stream-Token wird in Logs maskiert (`***`).
- `UnexpectedExit_HttpInputTransportMarker_SetsHttpTransportFailure` — Marker bei HTTP-Eingang → `HttpTransportFailure = true`.
- `UnexpectedExit_PipeInputTransportMarker_NoHttpTransportFlag` — gleiche Marker bei Pipe-Eingang → Flag `false`.
- `ExpectedEndOfRun_NoWarningAndNoUnexpectedExit` — Ende am Laufende (Toleranz) → planmäßig.
- `StartAsync_ClearsLastUnexpectedExit` — Neustart setzt Diagnose zurück.
- `StartAsync_SecondJob_StopsPreviousProcess` — zweiter Job killt den ersten.
- `StartAsync_FailedStart_ThrowsAndStaysStopped` — `startProcess == null` → `InvalidOperationException`, bleibt gestoppt.
- `ExpectedPositionSeconds_NoJob_IsNull` — ohne Job `null`.

### FfmpegLiveStreamSegmenterE2ETests

Datei: `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs` (`Category=E2E`; echter ffmpeg-Prozess, generiertes Testvideo; stiller Rückkehr ohne ffmpeg)

- `RealFfmpeg_ProducesPlaylistAndSegments` — echte Segmentierung erzeugt `live.m3u8` mit `#EXTM3U` und `seg_*.ts`.
- `SecondJob_PlaylistContinuesAndMarksDiscontinuity` — N17-Regression: zweiter Job auf laufendem → Playlist führt beide Generationen, `#EXT-X-DISCONTINUITY` vor erstem Segment der neuen Generation, `#EXT-X-PROGRAM-DATE-TIME` vorhanden.

### LiveStreamMediaResolverTests

Datei: `VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs` (`IAsyncLifetime`; `StubStreamTokenService`; SQLite via `ProgramScheduleTestDb`)

- `ResolveAsync_LocalSource_ReturnsFilePath` — lokale Quelle → `FilePath`, kein Token-Call.
- `ResolveAsync_LocalSourceMissingFile_ReturnsNull` — fehlende Datei → `null`.
- `ResolveAsync_RemoteSource_ReturnsHttpUrl` — SFTP → `HttpUrl`, ein Token-Call.
- `ResolveAsync_UseHttpInputFalse_ReturnsPipe` — Flag aus → Pipe.
- `ResolveAsync_TokenServiceNull_FallsBackToPipe` — keine URI → Pipe.
- `ResolveAsync_OptionDisabled_FallsBackToPipe` — `LiveStreamOptions.UseHttpInput = false` → Pipe.
- `ResolveAsync_RunDuration_SizesTokenLifetime` — 90 min Restlaufzeit → Token-Lifetime 105 min.
- `ResolveAsync_UnknownItem_ReturnsNull` — unbekannte ID → `null`.

### LiveStreamE2ETests

Datei: `VideoWebPlayer.Tests/LiveStreamE2ETests.cs` (`Category=E2E`; `DeviceClientTestBase` — In-Prozess-Host via `PairingWebApplicationFactory`, echtes HTTP, gekoppeltes Gerät via QR-Bootstrap)

- `Anonymous_RequestPlaylist_Returns401` — ohne Token → 401.
- `Anonymous_RequestSegment_Returns401` — ohne Token → 401.
- `PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist` — `access_token`-Query → 200, umgeschriebene Playlist mit Token in Segment-URIs, `Cache-Control: no-store`.
- `PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` — Segment → 200, `video/mp2t`, Inhalt stimmt.
- `PairedDevice_RequestInvalidOrMissing_Returns404` (Theory) — `missing.ts`, `..%2Fsecret.ts`, `a%20b.ts` → 404.

### LiveTvE2ETests

Datei: `VideoWebPlayer.Tests/LiveTvE2ETests.cs` (Playwright; `PlaylistsE2ETestBase` mit Kestrel-Host; `SkipBrowser`-Fallback)

- `LiveTv_ViaNavMenu_ShowsPlayerAndProgram` — Navigation → `/live-tv`: Jetzt-/Gleich-Karten, Vorschau, `#live-tv-video` sichtbar, m3u8-Request trägt `access_token`.
- `LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming` — Sendelücke → `#live-tv-no-program` („Aktuell läuft keine Sendung" + nächste Sendung), kein Video-Element; Wiederanlauf bewusst nicht E2E-geprüft (Kommentar im Test).

### LiveTvTests (bUnit)

Datei: `VideoWebPlayer.Tests/Components/LiveTvTests.cs` (bUnit + Moq; `ControllableTimeProvider` für die 15-s-Pollschleife; `TokenClient` mit Test-Token)

- `Now_LoadsAndRendersCurrentNextAndUpcoming_AndAttachesStream` — Darstellung + `liveTv.attach` mit `access_token`.
- `Now_ProgressFill_WritesInvariantPercent_UnderGermanCulture` — Fortschrittsbalken invariant formatiert.
- `Now_WithoutRunningSlot_ShowsNoProgramHintAndUpcoming_WithoutAttach` — Sendelücke → `#live-tv-no-program`, kein Attach.
- `Upcoming_RendersCompactProgramCards_WithPosterBadgeAndDescription` — Programmkarten mit Poster/Badge/Beschreibung/Fallback.
- `Page_NotAuthenticated_ShowsLoginForm_WithoutGuideCall` — kein Guide-Aufruf unangemeldet.
- `Page_GuideCallFails_ShowsFriendlyLoadError` — 500 → `#live-tv-error`.
- `Page_GuideCallForbidden_ShowsAccessHint` — 403 → Zugriffshinweis.
- `Page_NotifyStreamUnsupported_ShowsBrowserHint` — `#live-tv-unsupported`.
- `Page_FeatureNotReleased_ShowsHint_WithoutGuideCallOrPoll` — Flag aus → `#live-tv-disabled`.
- `Page_UserWithoutTvAccess_ShowsForbiddenHint_WithoutGuideCall` — ohne Freigabe → `#live-tv-no-access`.
- `PollTick_RefreshesUpcomingList` — Timer-Tick aktualisiert Vorschau.
- `PollTick_NowFails_UpcomingStillRefreshes` — Now-Ausfall blockiert Vorschau nicht.
- `Page_NotifyStreamError_ShowsRetryHint` — `NotifyStreamError` → `#live-tv-stream-error` („nicht verfügbar" + „erneut versucht").

### NavMenuLiveTvTests

Datei: `VideoWebPlayer.Tests/Components/NavMenuLiveTvTests.cs` (bUnit; `NavMenuTestContext`)

- `NavMenu_ContainsExactlyOneLiveTvEntry_ForAuthorizedUser` — genau ein `/live-tv`-Link „Live-TV".
- `NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased` — Flag aus → kein Link.
- `NavMenu_HidesLiveTvEntry_WhenUserNotReleased` — ohne Allowlist-Zeile → kein Link.
- `NavMenu_ShowsLiveTvEntry_ForAdminWithoutAccessRow` — Admin sieht Link ohne Zeile.

### ProgramSettingsServiceLiveStreamTests

Datei: `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs` (`IAsyncLifetime`; SQLite via `ProgramScheduleTestDb`)

- `Getters_DefaultToLiveStreamDefaults` — 6 s / `null` / 30 min.
- `UpdateLiveStreamSettingsAsync_PersistsValues` — Roundtrip der drei Einstellungen.
- `UpdateLiveStreamSettingsAsync_EmptyDirectory_ClearsToDefault` — leerer Pfad → `null`.
- `UpdateLiveStreamSettingsAsync_InvalidValues_Throws` (Theory) — Länge 0/-5/301, Retention 0/-1 → `ArgumentException`.

## Hilfsmethoden und Testdoubles

### ProgramScheduleTestDb

Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`

- `CreateAsync(dbName, ct)` — Shared-In-Memory-SQLite (`cache=shared`), `EnsureCreated`, seedet `Setup { TvSenderEnabled = true }` und die Trailer-Clipgruppe.
- `SeedTrailerClipGroupAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync` — Clipgruppen-Seeds.
- `CreateService` / `CreateGuideService` — `ProgramScheduleService`/`ProgramGuideService` auf der Test-DB.
- `CreateSlotAsync`, `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMediaItemAsync`, `SeedProbeItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`, `LinkShowGenreAsync`, `CreateRuleAsync`, `GetChannelAsync` — Daten-Seeds für Sendeplan-/Sender-Tests.
- `SaveDiagAsync` — `SaveChanges` mit FK-Diagnose im Fehlerfall.
- `SlotsAsync`, `AssertNoDisplacedAsync`, `AssertGapless` — Assertions zum Sendeplan.

### DeviceClientTestBase

Datei: `VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`

- `CreateUserAsync`, `CreateBootstrapTicketAsync`, `PairDeviceAsync`, `CreateUserAndPairDeviceAsync(email, isAdmin, grantsTvAccess)` — Benutzer + QR-Pairing-Bootstrap → Bearer-Token (das `access_token`-Muster der E2E-Tests).
- `GrantTvSenderAccessAsync` — `TvSenderUserAccess`-Zeile anlegen.
- `CreateScope`, `CreateHttpClient`, `CreateRecordedClient` — Server-Zugriff und HTTP-Clients gegen den In-Prozess-Host.
- `RecordingHttpHandler`/`RecordedRequest` — protokolliert ausgehende Requests des Clients.

### PlaylistsE2ETestBase

Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

- `InitializeAsync` — `WebApplicationFactory` + Kestrel (dynamischer Port), Playwright-Chromium headless; `PlaywrightException` → `SkipBrowser = true`.
- `LoginAsync`, `SeedUsersAsync`, `Page`, `ServerUrl`, `Services` — Browser-/Server-Anbindung der E2E-Tests.

### TestWebHostEnvironment

Dateien: `VideoWebPlayer.Tests/Helpers/TestWebHostEnvironment.cs` (Namespace `…Helpers`) und eine zweite lokale Variante in `VideoWebPlayer.Tests/Services/LiveStreamSegmentStoreTests.cs` (Namespace `…Services`)

- Minimaler `IWebHostEnvironment`-Stub mit setzbarem `ContentRootPath` (Temp-Verzeichnis) für Store-/Controller-Tests.

### FakeAuthService

Datei: `VideoWebPlayer.Tests/Helpers/FakeAuthService.cs`

- `IAuthService`-Double mit setzbarem `CurrentUser` (für `CheckLoggedIn`/403-Pfade der Controller-Tests).

### ListLogger&lt;T&gt;

Datei: `VideoWebPlayer.Tests/Helpers/ListLogger.cs`

- `ILogger<T>`-Double: `Entries` (Level + Nachricht) für Log-Assertions (u. a. stderr-Tail-/Maskierungs-Tests des Segmenters).

### ControllableTimeProvider

Datei: `VideoWebPlayer.Tests/Helpers/ControllableTimeProvider.cs`

- `TimeProvider`, dessen Timer nur über `TickTimers()` feuern — treibt die `PeriodicTimer`-Pollschleife der `LiveTv`-bUnit-Tests deterministisch.

### Lokal in den Testdateien deklarierte Doubles

- `LiveStreamCoordinatorTests`: `FakeGuide` (`IProgramGuideService`), `FakeLocator` (`IFfmpegLocator`), `FakeSegmenter` (`ILiveStreamSegmenter`, sammelt `Jobs`/`StopCalls`), `StubStreamTokenService`.
- `LiveStreamWorkerTests`: `FakeGuide`, `FakeLocator`, `FakeSegmenter`, `NullStreamTokenService`.
- `FfmpegLiveStreamSegmenterTests`: `ControlledPipeStream` (steuerbarer stderr-Stream), `FakeSegmenterProcess` (`ISegmenterProcess`, `SimulateExit`/`Killed`).
- `LiveTvTests`: `TokenClient` (`VideoWebPlayerClient` mit Test-Token), `CreateGuideMock` (`IProgramGuideApiClient`-Moq).
