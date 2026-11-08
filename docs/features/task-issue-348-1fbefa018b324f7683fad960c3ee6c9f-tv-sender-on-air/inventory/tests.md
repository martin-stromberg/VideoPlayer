# Tests — Bestandsaufnahme „TV-Sender: On-Air" (Issue #348)

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-08, ~21:16–21:28 Uhr +01:00 (Mitteleuropa, Windows 11, DESKTOP-CM8OBSG)
- Branch und Commit-ID: `task/issue-348-1fbefa018b324f7683fad960c3ee6c9f-tv-sender-on-air` @ `1be452bee5b006251c7f98f9de89fda07f02f07a` (identisch mit `staging`-HEAD, kein eigener Commit vorhanden)
- Uncommittete Änderungen im getesteten Stand: keine Code-Änderungen; nur das neue, untracked Verzeichnis `docs/features/task-issue-348-1fbefa018b324f7683fad960c3ee6c9f-tv-sender-on-air/` (requirement.md, todo.md, inventory/, Testnachweise)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403 (einzige installierte SDK-Version), `net10.0`, xUnit v3 3.2.2, Playwright-Browser unter `%USERPROFILE%\AppData\Local\ms-playwright` (chromium-1169…1248), ffmpeg/ffprobe 9.0.2 auf `PATH` (für E2E nötig)
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/staging-ci.yml` (Job `build-and-test`) führt drei Läufe aus — `--filter "Category!=E2E"` (Unit/Integration ohne E2E), `--filter "Category=Integration"` und `--filter "Category=E2E"`. Im Projekt existiert kein Test mit `Category=Integration` (kein `Trait("Category","Integration")` im Code — die Integration-Teststufe der CI läuft leer); faktisch gibt es zwei Suiten: alle Tests ohne E2E-Trait sowie die `[Trait("Category","E2E")]`-markierten Klassen (Playwright/WebApplicationFactory). Build vorausgehend: `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` — erfolgreich (0 Fehler, 139 Warnungen, überwiegend BL0008 in Account-Seiten; unverändert vorhanden).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit (Category!=E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=baseline-unit-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2681 von 2682 | 0 | 1 (`FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`, absichtsvoller Plattform-Skip) | [TRX](test-results/baseline-unit-tests.trx), [Konsole](test-results/baseline-unit-tests-console.log) |
| E2E (Category=E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=baseline-e2e-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 252 von 253 | 0 | 1 (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[Fact(Skip = "Befund: api/continue-watching antwortet mit 500 …")]`, absichtsvoller Skip wegen bekanntem unbeteiligtem Defekt) | [TRX](test-results/baseline-e2e-tests.trx), [Konsole](test-results/baseline-e2e-tests-console.log) |
| Integration (Category=Integration) | nicht ausgeführt — im Projekt existiert kein Test mit diesem Trait (CI-Stufe läuft leer); die Filterstufe `Category!=E2E` deckt alle übrigen Tests ab | — | — | — | — | — | — |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| — | — | **Keine nachgewiesenen Testfehler**: beide Läufe endeten mit 0 Fehlschlägen. | — |

### Testlücken und Ausführungsprobleme

- `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` wurde unter Windows nicht ausgeführt (`NotExecuted`; `Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix-Dateirechte lassen sich nur unter Unix prüfen.")`) — absichtsvoller Plattform-Skip, in der CI (ubuntu-latest) ausgeführt.
- `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` ist mit `[Fact(Skip = "Befund: api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehoert …")]` dauerhaft deaktiviert — dokumentierter, vom On-Air-Vorhaben unbeteiligter Bestanddefekt (`ContinueWatchingService`), in der CI ebenfalls übersprungen.
- `dotnet format`-Stufe der CI wurde für die Bestandsaufnahme nicht wiederholt (keine Testausführung); der Release-Build des Testprojekts (`-c Release`, CI-Variante) lief fehlerfrei. Ein Debug-Build der Solution wurde nicht separat ausgeführt (DoD-Prüfung, kein Testnachweis).
- Zur Anforderung existieren **keine** On-Air-bezogenen Tests — der Schalter existiert noch nicht. Deckung des bisherigen Verhaltens ist unten je Testklasse aufgeführt.

## Testklassen

### `LiveStreamWorkerTests`
`VideoWebPlayer.Tests/LiveStreamWorkerTests.cs` — Worker gegen Fakes + echte SQLite-Fixture (`ProgramScheduleTestDb`, seedet `TvSenderEnabled = true`).
- `ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` — Start leert Segment-Verzeichnis, erster Takt läuft.
- `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` — `TvSenderEnabled = false`: laufender Segmenter wird gestoppt (`StopCalls >= 1`, `Running = false`), nie gestartet — **Vorbild-Test für den Off-Air-Fall**.
- `ExecuteAsync_UndecodableSlotSkips_LogsInputUnplayableOnce` — Dedup der Dauerzustands-Logs.
- `ExecuteAsync_MediaUnavailableAfterGap_LogsInformationAgain` — Zwischenzustand setzt Dedup zurück.
- `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` — Clear + Standby-Refresh vor der Startverzögerung.
- Hilfsklassen: `FakeGuide` (`IProgramGuideService`), `FakeSegmenter` (`ILiveStreamSegmenter` mit `Running`/`StopCalls`/`StartCalls`), `FakeLocator`/`NoToolLocator`, `NullStreamTokenService`, `CreateStandby` (echter `LiveStreamStandbyProvider` über internen Overload).

### `Controllers/LiveStreamControllerTests`
`VideoWebPlayer.Tests/Controllers/LiveStreamControllerTests.cs` — Controller direkt instanziiert, Fixture-Benutzer `u1` mit `TvSenderUserAccess`-Zeile.
- `GetFile_M3u8_InjectsAccessTokenIntoSegmentUris`, `GetFile_M3u8WithoutToken_LeavesUrisUntouched` — Playlist-Rewrite.
- `GetFile_TsSegment_ReturnsVideoMp2t`, `GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen`, `GetFile_TsSegment_SetsNoStoreCacheHeader` — Segment-Auslieferung.
- `GetFile_InvalidOrMissing_ReturnsNotFound` (Theory) — Pfad-Whitelist.
- `GetFile_UserWithoutTvAccess_Returns403`, `GetFile_AdminWithoutTvAccessRow_ReturnsFileOr404Not403`, `GetFile_NotLoggedIn_Returns401`, `GetFile_AccessDeniedAfterLogin_Returns500Not401` — Gate-Kette.
- `GetFile_MissingPlaylist_ServesStandbyPlaylist`, `GetFile_SegmentlessPlaylist_ServesStandbyPlaylist`, `GetFile_StandbySegment_ServedFromMemory`, `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity`, `GetFile_MissingPlaylist_WithoutStandbyAssets_Returns404` — Standby-Pfad.
- `GetFile_FeatureDisabled_Returns404` (Theory: `live.m3u8`, `standby_0_00.ts`) — **Feature-Gate vor Standby**; Vorbild für einen Off-Air-Test.

### `Components/LiveTvTests`
`VideoWebPlayer.Tests/Components/LiveTvTests.cs` — bUnit-Tests der Live-TV-Seite.
- `Page_FeatureNotReleased_ShowsHint_WithoutGuideCallOrPoll` — Flag aus → Hinweis, kein Guide-Aufruf/Poll — Vorbild für den Off-Air-Zustand.
- `Page_UserWithoutTvAccess_ShowsForbiddenHint_WithoutGuideCall`, `Page_NotAuthenticated_ShowsLoginForm_WithoutGuideCall`, `Page_GuideCallFails_ShowsFriendlyLoadError`, `Page_GuideCallForbidden_ShowsAccessHint` — Zustandskaskade.
- `Now_LoadsAndRendersCurrentNextAndUpcoming_AndAttachesStream`, `Now_WithoutRunningSlot_ShowsNoProgramHintAndUpcoming_WithoutAttach` — Player-Attach/Nicht-Attach.
- `Page_NotifyStreamError_ShowsRetryHint`, `Page_NotifyStreamUnsupported_ShowsBrowserHint` — JS-Callback-Zustände.
- `PollTick_RefreshesUpcomingList`, `PollTick_NowFails_UpcomingStillRefreshes` — 15-s-Polling.
- `Upcoming_*`/`Now_ProgressFill_*` — Karten-/Fortschrittsdarstellung.
- `LiveTvUpcomingDisplayItemsTests` — Block-Gruppierung der Vorschau.

### `ProgramSettingsServiceTests` (+ Teilklassen)
`VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs`, `ProgramSettingsServiceLiveStreamTests.cs`, `ProgramSettingsServiceTests_AgeWindow.cs`, `ProgramSettingsServiceContentScopeTests.cs`, `ProgramSettingsServiceTests_SetupRace.cs` — SQLite-Fixture; Defaults + Roundtrips aller Settings-Methoden, u. a. `Getters_DefaultToLiveStreamDefaults`, `UpdateLiveStreamSettingsAsync_PersistsValues`/`_InvalidValues_Throws`, `UpdateLiveStreamStandbyClipAsync_Roundtrip`/`_Invalid_Throws` — Vorbild für `Get/UpdateTvSenderOnAir`-Tests.

### `TvSenderUserAccessTests`
`VideoWebPlayer.Tests/TvSenderUserAccessTests.cs` — `IsGranted_RequiresFlagAndAllowlistOrAdmin` (kombiniertes Gate), `UpdateAllowedUserIds_ReplacesAtomically`, `AccessRow_RequiresExistingUser_FkEnforced` (echtes SQLite).

### `LiveStreamCoordinatorTests`
`VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` — Takt-Entscheidungen gegen Fakes: Start/Unchanged/Restart (Drift, Slotwechsel), Sendelücke → `Stopped` + `WakeAtUtc`, `FfmpegUnavailable`, `MediaUnavailable`, komplette Eingangskaskade (HTTP → sftp → pipe:0) inkl. Slot-Gedächtnis und `UndecodableInput`-Skip.

### `ProgramScheduleWorkerTests`
`VideoWebPlayer.Tests/ProgramScheduleWorkerTests.cs` — u. a. `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased`, `Worker_DisabledFeature_SkipsPurge` (Flag-Auswertung je Takt — Off-Air darf den Plan **nicht** stoppen).

### `Services/MediaDurationProbeWorkerTests`
`VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs` — u. a. `ExecuteAsync_FeatureFlagEvaluatedPerCycle` (Flag je Takt neu gelesen — wirkt ohne Neustart; auch Off-Air unbeteiligt).

### `InternalMediaStreamControllerTests`
`VideoWebPlayer.Tests/InternalMediaStreamControllerTests.cs` — u. a. `Stream_FeatureDisabled_ValidToken_Returns404` (einziges Gate: `TvSenderEnabled`); Hilfsmethode `SetTvSenderEnabledAsync`.

### `Controllers/ProgramGuideControllerTests`
`VideoWebPlayer.Tests/Controllers/ProgramGuideControllerTests.cs` — `GetNow_FeatureNotReleased_Returns404`, `GetUpcoming_FeatureNotReleased_Returns404`, Zugriffs-Gates (401/403/200), `GetUpcoming_HoursOutOfRange_Returns400`.

### `Services/*` (Stream-Kette)
- `LiveStreamSegmentStoreTests` — Verzeichnisauflösung, `TryResolveFileAsync`-Whitelist, Retention-Sweep, `ClearAsync_RemovesEverything`.
- `LiveStreamStandbyProviderTests` — rotierende Playlist, Default-/Custom-Satz, Cache-Pfad, Transcode-Kaskade, Fehlschlag-Sperre.
- `FfmpegLiveStreamSegmenterTests` (+`_FirstOutput`) — Prozess-Argumente je Eingang, `StopAsync`, Playlist-Fortschreibung.
- `FfmpegLiveStreamSegmenterE2ETests` (Category=E2E) — echte ffmpeg-Prozesse.
- `LiveStreamMediaResolverTests`, `FfprobeMediaDurationProbeTests`, `MediaDurationProbeServiceTests*` — Eingangs-/Probe-Logik.
- `ProgramScheduleSignalTests`, `ProgramScheduleServiceTests_*` — Signal/Plangenerierung.

### `Components/TVSenderAdminTests*` (bUnit)
`TVSenderAdminTests`, `_AgeWindow`, `_ClipBlocks`, `_ClipGroups`, `_ContentScope`, `_DayBar`, `_FfmpegDownload`, `_Standby` — Tabs, Regel-Editor, Sendeplan, Laufzeit-Tab, Standby-Karte inkl. `StandbyCard_NonAdmin_Blocked`. Für einen On-Air-Umschalter im Einstellungen-Tab ist `StandbyCard_*` das Struktur-Vorbild.

### `Components/NavMenuLiveTvTests`
- Live-TV-Eintrag: sichtbar nur bei Flag + Benutzerfreigabe; verborgen sonst; Admin implizit.

### `Services/Backups/VideoWebPlayerBackupDataTests_*`
Backup-Regression je Spalte/Tabelle, Muster `LegacyBackupArchiveBuilder.RemoveColumnsAsync`:
- `VideoWebPlayerBackupDataTests_TvSenderEnabled.ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_RestoresDefaultFalse` — **direktes Vorbild** für den On-Air-Alt-Backup-Test (DoD-Pflicht); Ziel-Default der neuen Spalte hängt von der offenen Default-Frage ab (`true` empfohlen).
- `..._LiveStreamStandbyMediaItem`, `..._ProgramScheduleContentScope`, `..._AgeWindows`, `..._TvSenderUserAccess`, `..._StrandKey`, `..._ProgramSlotIsClipFill` u. a. — gleiche Mechanik je betroffener Spalte/Tabelle.

### E2E (Category=E2E)
- `LiveStreamE2ETests` (`DeviceClientTestBase`) — 401/404/Token-Rewrite/Segment/Standby/Discontinuity/no-store/konfigurierter Standby-Clip gegen laufenden Kestrel-Host.
- `LiveTvE2ETests` (`PlaylistsE2ETestBase`) — Nav → Player + Programm, Sendelücke-Hinweis, Warm-up-Standby ohne Fehlerhinweis; Seeds `TvSenderEnabled = true` + `TvSenderUserAccess`.
- `ProgramGuideE2ETests` — now/upcoming 401/200/400.
- `TVSenderAdminE2ETests*` (`Helpers/TVSenderAdminE2ETestBase` — `OpenAdminPageAsAdminAsync`, `UpdateSetupAsync`, `EnableTvSenderAsync`) — Admin-UI e2e.
- `ProgramSettingsE2ETests` — Allgemein-Seite: Persistenz mDNS/Discovery-URL, Validierung, Non-Admin-Sperre.
- `MediaProbeHttpIntegrationTests` — interner Media-Stream-Range-Pfad.

## Hilfsmethoden

### `ProgramScheduleTestDb`
`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — In-Memory-SQLite mit `EnsureCreated` + `Setup { TvSenderEnabled = true, ProgramScheduleUnknownRatingMode = Unrestricted }`-Seed (Zeile 43). Hilfsmethoden: `CreateSlotAsync`, `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMediaItemAsync`, `SeedProbeItemAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync`, `LinkMovie/ShowGenreAsync`, `CreateRuleAsync`, `GetChannelAsync`, `SeedTrailerClipGroupAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync`, `CreateService`, `CreateGuideService`, `SaveDiagAsync`. **Wichtig:** neue Pflicht-Spalten auf `Setup` werden von dieser Seed-Zeile übernommen — Defaults der Entity müssen testfreundlich sein bzw. explizit gesetzt werden.

### `LegacyBackupArchiveBuilder`
`VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs` — `CreateSeededBackupAsync` (Backup-Archiv aus frischer DB), `RemoveColumnsAsync` (Spalten aus einem Archiv entfernen = Alt-Backup-Simulation), `ReadTableColumnsAsync`.

### `TestWebHostEnvironment`, `ListLogger`, `ControllableTimeProvider`
`VideoWebPlayer.Tests/Helpers/` — Content-Root-Fake, Queue-Logger, steuerbarer `TimeProvider` (Timer-Ticks ohne Echtzeit).

### `TVSenderAdminE2ETestBase` / `PlaylistsE2ETestBase` / `DeviceClientTestBase`
`VideoWebPlayer.Tests/Helpers/` — E2E-Grundgerüste: `UpdateSetupAsync(Action<Setup>)`, `EnableTvSenderAsync`, `OpenAdminPageAsAdminAsync`, Seed-/Login-/Browser-Helfer, Kestrel-Host via `WebApplicationFactory`.
