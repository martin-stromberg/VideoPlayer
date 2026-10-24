# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-24, Testlauf ca. 16:34–16:39 +02:00 (MESZ)
- Branch und Commit-ID: `task/issue-265-a432af3f73ab424494a433f4748182b6-tv-sender-hls-segment-ausliefe`, Commit `4108c00e5570669624a3d431c0da97b76ba857aa` („TV-Sender: InternalMediaStreamController an Feature-Flag binden (Issue #259) (#275)")
- Uncommittete Änderungen im getesteten Stand: ausschließlich untracked Dateien unter `docs/features/task/issue-265-.../` (`requirement.md`, `todo.md`, dieses `inventory/`-Verzeichnis inkl. Testnachweise) — kein Quellcode-/Teststand verändert; kein Eintrag in `git stash list`
- Testumgebung und Runtime-/SDK-Versionen: Windows 11 Pro (10.0.26300), .NET SDK 10.0.402, Zielframework `net10.0`, Microsoft.Playwright 1.62.0 (Paketreferenz)
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - Repo-Vorgabe (`AGENTS.md`): `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite) plus `dotnet build VideoPlayer.sln` (Debug)
  - CI (`.github/workflows/staging-ci.yml`, `pr-staging-ci.yml`): Release-Build des Testprojekts, dann getrennt `--filter "Category!=E2E"`, `--filter "Category=Integration"`, `--filter "Category=E2E"` mit TRX-Loggern; `release.yml` führt die volle Suite ohne Filter
  - Test-Kategorien im Projekt: `E2E` (71 Trait-Markierungen; HTTP-E2E über `WebApplicationFactory`-artige Basen und Playwright-Browser-Tests); keine `Integration`-Markierungen gefunden

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build (Debug) | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 | – (0 Fehler, 142 Warnungen) | – | – | [build-debug.log](test-results/build-debug.log) |
| Volle Suite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --logger "trx;LogFileName=baseline-inventory.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/issue-265-.../inventory/test-results` | Repo-Root | 0 | 2247 | 0 | 1 | [test-run-baseline.log](test-results/test-run-baseline.log), [baseline-inventory.trx](test-results/baseline-inventory.trx) |

- Gesamtzahl Tests laut Lauf: 2248 (TRX-Counter: `total="2248" executed="2247" passed="2247" failed="0"`); Gesamtzeit ca. 4,7 Minuten.

### Nachgewiesene bestehende Testfehler

Keine. Im dokumentierten Ausgangslauf schlug kein Test fehl.

### Testlücken und Ausführungsprobleme

- **1 übersprungener Test (explizit deaktiviert):** `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[Fact(Skip = "...")]` in `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` (Z. 76–77) mit dokumentiertem Grund: `api/continue-watching` antwortet mit 500, wenn ein Film keiner Filmsammlung angehört; laut dortigem Auftrag nicht zu beheben. Betrifft den Anforderungsbereich nicht. (TRX: `outcome="NotExecuted"`.)
- **Testlücke bezogen auf die Anforderung:** Es existiert kein Test, der das Löschen eines `.ts`-Segments bei offenem Auslieferungs-Stream bzw. die `FileShare`-Belegung des Segments absichert. `GetFile_TsSegment_ReturnsVideoMp2t` prüft aktuell `PhysicalFileResult` + `video/mp2t`; nach der Umstellung muss er `FileStreamResult` erwarten.
- Build-/Setup-/Infrastrukturfehler: keine (Build und Testlauf liefen ohne Fehler durch; E2E-Tests inkl. Playwright-Nutzung liefen lokal mit).

## Testklassen

### `LiveStreamControllerTests`

Datei: `VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs` (`IAsyncLifetime`; Temp-ContentRoot + `LiveStreamSegmentStore` + `ProgramScheduleTestDb`-Fixture; Controller wird direkt mit `DefaultHttpContext` aufgerufen — ohne `[BearerTokenCheck]`-Filter)

- `GetFile_M3u8_InjectsAccessTokenIntoSegmentUris` — Token wird in jede Segment-URI der Playlist injiziert, `#`-Zeilen bleiben unverändert, `CacheControl: no-store`
- `GetFile_M3u8WithoutToken_LeavesUrisUntouched` — ohne `access_token` kein Rewrite
- `GetFile_TsSegment_ReturnsVideoMp2t` — **erwartet aktuell `Assert.IsType<PhysicalFileResult>`** und `ContentType == "video/mp2t"` (Z. 94–104); muss auf `FileStreamResult` umgestellt werden
- `GetFile_InvalidOrMissing_ReturnsNotFound` (Theory: `../evil.ts`, `sub/evil.ts`, `a b.ts`, `not-there.ts`, `live.m3u8`) — 404 für Traversal, ungültige und fehlende Dateien
- `GetFile_UserWithoutTvAccess_Returns403` — Benutzer ohne `TvSenderUserAccess`-Zeile → `ForbidResult`
- `GetFile_AdminWithoutTvAccessRow_ReturnsFileOr404Not403` — Admin ohne Zeile → kein 403
- `GetFile_NotLoggedIn_Returns401` — `FakeAuthService` ohne Benutzer → `UnauthorizedObjectResult`

### `LiveStreamSegmentStoreTests`

Datei: `VideoWebPlayer.Tests/Services/LiveStreamSegmentStoreTests.cs` (`IAsyncLifetime`; Temp-ContentRoot)

- `GetDirectoryAsync_Default_UsesDataLiveStreamUnderContentRoot` / `_ConfiguredRelative_ResolvesAgainstContentRoot` / `_ConfiguredAbsolute_UsedAsIs` — Verzeichnisauflösung
- `TryResolveFileAsync_ExistingFile_ReturnsPath` — existierende Datei liefert absoluten Pfad
- `TryResolveFileAsync_InvalidOrMissing_ReturnsNull` (Theory: `../escape.ts`, `..\\escape.ts`, `sub/dir.ts`, `a b.ts`, ``, `missing.ts`) — Containment/Whitelist
- `SweepExpiredSegmentsAsync_DeletesOnlyOldSegments` — nur alte `seg_*.ts` werden gelöscht, Playlist bleibt
- `ClearAsync_RemovesEverything` — komplettes Leeren des Verzeichnisses

### `LiveStreamE2ETests` (Category `E2E`)

Datei: `VideoWebPlayer.Tests/LiveStreamE2ETests.cs` (Basisklasse `DeviceClientTestBase`; echte HTTP-Requests gegen die App; seedet ein Temp-Segmentverzeichnis über `Setup.LiveStreamSegmentDirectory` + `TvSenderEnabled = true`)

- `Anonymous_RequestPlaylist_Returns401` / `Anonymous_RequestSegment_Returns401` — ohne Token 401
- `PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist` — 200, umgeschriebene Segment-URIs mit Token, `Cache-Control: no-store`
- `PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` — 200, `video/mp2t`, vollständiger Inhalt
- `PairedDevice_RequestInvalidOrMissing_Returns404` (Theory: `missing.ts`, `..%2Fsecret.ts`, `a%20b.ts`) — 404 über HTTP

### Weitere Tests im Umfeld (nicht direkt betroffen)

- `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs` — Live-Stream-Einstellungen (Defaults, Validierung, Persistenz)
- `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` — Synchronisationsentscheidungen
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs` und `FfmpegLiveStreamSegmenterE2ETests.cs` — Segmenter-Verhalten inkl. stderr-Drain/Exit-Klassifikation
- `VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs` — Token-Service des `InternalMediaStreamController` (separater Endpunkt, kein Segment-Zugriff)

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(string dbName, CancellationToken)` — echte In-Memory-SQLite-DB (`mode=memory&cache=shared`), `EnsureCreatedAsync`, seedet `Setup { TvSenderEnabled = true }` (Z. 40)
- `Db` — `ApplicationDbContext`; `CreateService`, `CreateGuideService`, `CreateSlotAsync`, `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMediaItemAsync`, `SeedProbeItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`, `LinkShowGenreAsync`, `CreateRuleAsync`, `GetChannelAsync`, `SaveDiagAsync` (FK-Diagnose), `SlotsAsync`, `AssertNoDisplacedAsync`, `AssertGapless`

### `TestWebHostEnvironment`

- `VideoWebPlayer.Tests/Helpers/TestWebHostEnvironment.cs` — gemeinsames `IWebHostEnvironment`-Testdouble (Defaults `AppContext.BaseDirectory`)
- `VideoWebPlayer.Tests/Services/LiveStreamSegmentStoreTests.cs` (Z. 14–22) — zusätzlich ein gleichnamiges, dateilokales Double im Namespace `VideoWebPlayer.Tests.Services`; `LiveStreamControllerTests` nutzt via `using VideoWebPlayer.Tests.Helpers` die Helpers-Variante

### `FakeAuthService` (`VideoWebPlayer.Tests/Helpers/FakeAuthService.cs`)

- `CurrentUser` (setzbar) — steuert 401-/200-Pfade der Controller-Tests; `LoginAsync`/`ImpersonateAsync` liefern leere Tokens

### `DeviceClientTestBase` (`VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`)

- Basis der E2E-Tests: `CreateScope`, `CreateHttpClient`, `CreateUserAndPairDeviceAsync(..., grantsTvAccess: true)` — legt Benutzer, Pairing und TV-Sender-Freigabe an und liefert das Access-Token-Payload
