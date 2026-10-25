# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-25, Builds ~09:55–10:10 Uhr MEZ (UTC+1), Testläufe 10:11–10:19 Uhr MEZ.
- **Branch und Commit-ID:** `task/issue-277-1f22729c5f434f6994c5aee41f073cac-tv-sender-platzhalter-clips-gr`, Commit `3bbbd490ab9f45fc01327b5766e638c0ed7dd010` („fix: ffmpeg-Download räumt staged *.tmp vor dem Failed-Snapshot weg (Flake) (#281)", 2026-10-25 10:02:33 +0100).
- **Uncommittete Änderungen im getesteten Stand:** nur die untracked Feature-Dokumente `docs/features/task/issue-277-1f22729c5f434f6994c5aee41f073cac-tv-sender-platzhalter-clips-gr/` (`requirement.md`, `todo.md`, diese Bestandsaufnahme). Keine Änderungen an Produktivcode, Tests oder Testkonfiguration.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (lokale Arbeitsstation, Git Bash/MSYS), .NET SDK 10.0.402, Laufzeit .NET 10.0.13 (`net10.0`). Test-Stack (`VideoWebPlayer.Tests.csproj`): xunit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5, bUnit 2.11.3, Moq 4.20.72, Microsoft.Playwright 1.62.0 (Chromium lokal unter `%USERPROFILE%/AppData/Local/ms-playwright` installiert), Microsoft.AspNetCore.Mvc.Testing 10.0.12, EF Core InMemory + Sqlite 10.0.12, coverlet.collector 10.0.1.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `AGENTS.md` §7 verlangt `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite) sowie `dotnet build VideoPlayer.sln` in Debug **und** Release. Die CI (`.github/workflows/pr-staging-ci.yml` und `staging-ci.yml`, identische Schritte) baut `dotnet build VideoPlayer.sln -c Debug` sowie das Testprojekt in Release (`-p:NoWarn=NU1903`), installiert Playwright-Chromium (`pwsh VideoWebPlayer.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium` — Browser lokal bereits vorhanden) und führt drei Läufe aus: `--filter "Category!=E2E"` (mit Coverage, hier ohne `--collect` ausgeführt), `--filter "Category=Integration"`, `--filter "Category=E2E"`.
- **Builds vor den Läufen (Nachweis im Konsolenlog der Shell, nicht separat archiviert):** `dotnet restore VideoPlayer.sln` (ok), `dotnet build VideoPlayer.sln -c Debug --no-restore` — **0 Fehler, 142 Warnungen** (Bestand: `BL0008`, `CS860x`, `CS0414`, `CS0436`), `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` — **0 Fehler, 142 Warnungen**. Es wurden weder Code noch Tests oder Testkonfiguration verändert.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=baseline-non-e2e.trx" --logger "console;verbosity=normal" --results-directory docs/features/task/issue-277-…/inventory/test-results` | Repo-Root | 0 | 2.066 | 0 | 1 | [TRX](test-results/baseline-non-e2e.trx), [Konsole](test-results/baseline-non-e2e.log) |
| 2 | `dotnet test … --filter "Category=Integration" --logger "trx;LogFileName=baseline-integration.trx" …` | Repo-Root | 0 | 0 | 0 | 0 | [TRX](test-results/baseline-integration.trx), [Konsole](test-results/baseline-integration.log) — **kein Test entsprach dem Filter** |
| 3 | `dotnet test … --filter "Category=E2E" --logger "trx;LogFileName=baseline-e2e.trx" …` | Repo-Root | 0* | 223 | 2 | 1 | [TRX](test-results/baseline-e2e.trx), [Konsole](test-results/baseline-e2e.log) |
| 4 (Wiederholung) | `dotnet test … --filter "FullyQualifiedName~FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity" --logger "trx;LogFileName=rerun1-segmenter.trx" …` | Repo-Root | 0 | 1 | 0 | 0 | [TRX](test-results/rerun1-segmenter.trx) |
| 5 (Wiederholung) | `dotnet test … --filter "FullyQualifiedName~BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack" --logger "trx;LogFileName=rerun1-backupupload.trx" …` | Repo-Root | 0 | 1 | 0 | 0 | [TRX](test-results/rerun1-backupupload.trx) |

*`dotnet test` meldet bei Testfehlschlägen „Fehler beim Testlauf"; der Prozess-Exit-Code des Befehls wurde nicht separat ausgegeben (Pipeline in `tail`). Beide Fehlschläge sind dokumentiert und einzeln wiederholt worden.

**Gesamt (disjunkte Filter):** 2.289 bestanden, 2 fehlgeschlagen (im Gesamtlauf; in Einzelwiederholung bestanden), 2 übersprungen. Laufzeiten: ~1,5 min (Lauf 1), <1 s (Lauf 2), ~4,2 min (Lauf 3).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.Services.FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` | E2E / `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs:124` | `Assert.Null()` schlug fehl: erwartet `null`, tatsächlich `LiveStreamExitInfo { SlotId = 2, ExitCode = 0, HttpTransportFailure = False, … }` | Lauf 3 ([TRX](test-results/baseline-e2e.trx)); **Einzelwiederholung bestanden** (Lauf 4, [TRX](test-results/rerun1-segmenter.trx)) → Flake |
| `VideoWebPlayer.Tests.BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack` | E2E / `VideoWebPlayer.Tests/BackupUploadE2ETests.cs:387` | `Assert.Equal()` schlug fehl: erwartet `http://127.0.0.1:60973/`, tatsächlich `…/admin/backups?backupStatus=…` | Lauf 3 ([TRX](test-results/baseline-e2e.trx)); **Einzelwiederholung bestanden** (Lauf 5, [TRX](test-results/rerun1-backupupload.trx)) → Flake |

Beide Fehlschläge sind im Ausgangslauf dokumentiert und dürfen als preexisting/flaky gelten; ein erneutes Fehlschlagen dieser Tests ist unabhängig davon zu bewerten.

### Testlücken und Ausführungsprobleme

- **Übersprungene Tests (2):**
  - `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` (Lauf 3) — per `Skip` deaktiviert mit dokumentiertem Befund: „api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehoert (ContinueWatchingService.Create ohne Null-Pruefung). Wird laut Auftrag in diesem Lauf nicht behoben." (Kodierung im Log: `gehoert`/`Pruefung`.)
  - `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` (Lauf 1) — `NotExecuted`: „Unix-Dateirechte lassen sich nur unter Unix prüfen." Plattformbedingter Skip auf Windows, kein Fehler-Nachweis.
- **`Category=Integration` findet 0 Tests:** kein Test trägt `Trait("Category", "Integration")` (per Suche verifiziert — 0 Treffer). Der CI-Schritt ist faktisch leer.
- **E2E-Trait-Lücke:** Drei Playwright-E2E-Testklassen tragen **kein** `[Trait("Category", "E2E")]` und laufen daher im `Category!=E2E`-Lauf mit (verifiziert — sie stehen in Lauf 1): `TVSenderAdminE2ETests` (4 Tests), `TVSenderAdminE2ETests_FfmpegDownload` (2 Tests), `LiveTvE2ETests` (2 Tests) — alle ohne Trait in den Dateien; Basisklassen (`PlaylistsE2ETestBase` u. a.) tragen ebenfalls kein Trait. Alle 8 bestanden. Folge: `Category=E2E` (Lauf 3, 226 Tests) umfasst diese Klassen nicht; die volle E2E-Abdeckung ist verteilt auf Lauf 1 + Lauf 3.
- Keine Build-/Setup-/Infrastrukturfehler aufgetreten. Playwright-Browser lokal vorhanden, kein Installationslauf nötig.

## Testklassen (für die Anforderung relevant)

### Sendeplan-Generierung (`ProgramScheduleServiceTests_*`, alle gegen **echtes SQLite** via `ProgramScheduleTestDb`)

- `ProgramScheduleServiceTests_Generation` — Wiederholungsmuster, Serien-Fortschritt/Wrap-around, `SlotDuration`-Füllung (`EnsureCoverage_SlotDuration_FillsUntilTarget`), Lückenlosigkeit mit Füller, spanning occurrences, extend-only.
- `ProgramScheduleServiceTests_Boundary` — **direkt relevant für die Karenzzeit:** `EnsureCoverage_RemainderBelowMinRemain_PlansFiller` (Startschwelle `_minStartRemain`), `EnsureCoverage_ShortRemainder_OvershootingItem_Completes`, `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor`, `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`, `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence`, `ProgramScheduleOptions_MinStartRemain_Normalizes`, `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes`, plus `DeleteDisplacedSlots_*`, `ResetSchedule_*`.
- `ProgramScheduleServiceTests_Filler` — `EnsureCoverage_OnlyFillerRule_FillsEntireHorizon`, `EnsureCoverage_FillerItemTooLong_IsSkipped_NoFittingFiller_LeavesGap` (Restlücke über `RemainingGapSeconds`).
- `ProgramScheduleServiceTests_ClipSource` — **direkt relevant für Clip-Kandidatenmenge:** `EnsureCoverage_ClipSourceFiller_FillsGapsWithBareMediaItems`, `EnsureCoverage_ClipSource_ExcludesClassifiedItems`, `EnsureCoverage_ClipSource_SkipsItemsWithoutDurationAndNonVideos`, `EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots`, `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns`, `EnsureCoverage_ClipSource_MissingSource_IsOrphaned`, `Reconcile_ClipSourceFillerSlots_StayScheduled`, `GetRuleDiagnoses_ClipSource_CountsProbePending`.
- `ProgramScheduleServiceTests_Trailer` — **direkt relevant für die reservierte „Trailer"-Gruppe:** `EnsureCoverage_TrailerFiller_AirsTrailerBeforeScheduledMovie`, `EnsureCoverage_TrailerOfUnscheduledMovie_IsNotAired`, `EnsureCoverage_BeyondLeadWindow_FallsBackToNormalFiller`, `EnsureCoverage_TrailerWithoutDuration_IsNotAired`, `Reconcile_KeptTrailerSlot_StaysScheduled`.
- `ProgramScheduleServiceTests_Reconcile` — **direkt relevant für die Reproduzierbarkeit mit Clip-Slots:** `Reconcile_NoChanges_LeavesEverythingScheduled`, Deaktivierte/gelöschte Regel, Zeitänderung, Cursor-Fortsetzung ohne Sprung, `Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_LongAiring_*` (inkl. Beyond-MaxLookback-Verdrängung), `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`.
- `ProgramScheduleServiceTests_Interleaving` — Strang-Interleaving, `StrandOrderKey_IsDeterministic_AndNotAlphabetic`, Cursor-Fortsetzung je Show.
- `ProgramScheduleServiceTests_Conflicts` — Priorität/Tie-Break bei Konflikten.
- `ProgramScheduleServiceTests_Access` — Dauer-Pflicht und Publikums-Freigabe.
- `ProgramScheduleServiceTests_CursorSelection` — `SelectStrandCursor`-Winner-Logik, NULL-StrandKey-Duplikate.
- `ProgramScheduleServiceTests_Diagnosis` — Zähler der `ProgramRuleDiagnosis` (u. a. `ProbePending`/`ProbeExhausted` — Kennzeichnung „keine Laufzeit" bereits abgedeckt).
- `ProgramScheduleServiceTests_Genres` — Genre-Expansion, Quell-Filter, `RuleLinks_CascadeOnRuleAndGenreDelete` (Join-Kaskade-Muster).

### Regel-Service (`ProgramRuleServiceTests_*`)

- `ProgramRuleServiceTests_Validation` — Eingabevalidierung inkl. Materialart-Matrix (`CreateRule_DisallowedMaterialType_Throws`, `[Theory]`), `CreateRule_WithoutEpisodeCountAndDuration_Throws`, `GetAllowedMaterialTypes_ReturnsExpectedSets`. **Ort für Karenzzeit-/„Trailer"-Reservierungs-Tests.**
- `ProgramRuleServiceTests_ClipSource` — `MediaSourceClips` nur für `Filler`, unbekannte Quelle → `KeyNotFoundException`.
- `ProgramRuleServiceTests_Trailer` — `Trailer`-Material nur für `Filler`.
- `ProgramRuleServiceTests_GenreMaterial` — Join-Link-Roundtrip/-Ersetzung (`CreateAndUpdateRule_GenreLinksRoundTrippedAndReplaced`), `GetGenreMaterialOptions_*` — **Muster für Gruppen-Links/-Auswahl.**
- `ProgramRuleServiceTests_Crud`, `…_Channel`, `…_Authorization` — CRUD, Kanal, Admin-Erzwingung.

### Persistenz-/Backup-Constraints

- `ProgramRuleCursorUniquenessTests`, `ProgramRuleCursorNullStrandKeyMigrationTests` — echtes SQLite, Unique-/Migrations-Verhalten.
- `ProgramSlotTests_DateTimeKind` — UTC-Kind des Slot-Konverters.
- `TvSenderUserAccessTests` — Allowlist, FK-Erzwingung.
- `Services/Backups/VideoWebPlayerBackupDataTests*` — Alt-Backup-Regression je Tabelle/Spalte, darunter `…_ProgramRuleLinks` (`ReadFromAsync_LegacyBackupWithoutProgramRuleLinks_RestoresSuccessfully`), `…_Trailer`, `…_StrandKey`, `…_TvSenderUserAccess`, `…_DevicePairing`, Basis-`VideoWebPlayerBackupDataTests` (u. a. `ReadFromAsync_LegacyBackupWithoutTVChannelTablesAndHolidayFederalState_RestoresSuccessfully`, `ReadFromAsync_LegacyBackupWithoutProgramScheduleTablesAndSettings_RestoresSuccessfully`, `ReadFromAsync_LegacyBackupWithoutDurationColumnsInMediaItems_RestoresSuccessfully`). Hilfsklasse `LegacyBackupArchiveBuilder` (`CreateSeededBackupAsync`, `RemoveTablesAsync`, `RemoveColumnsAsync`, `AppendRowsAsync`, `ReadTableColumnsAsync`).

### UI / API / E2E

- `Components/TVSenderAdminTests` (bUnit) — Tabs, Regel-Dialog, Trailer-Checkbox/Clip-Quelle-Select nur bei Füller, Sendeplan-Karten, Probe-Reset.
- `Components/TVSenderAdminTests_FfmpegDownload` (bUnit) — ffmpeg-Download-Hinweis/Dialog.
- `TVSenderAdminE2ETests` (Playwright, **ohne E2E-Trait**) — `Admin_CreateEditDeactivateDeleteRule` (Muster für den Regel-Dialog-E2E), `Admin_ProbeReset_SingleItemAndAllFailed`, `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`.
- `TVSenderAdminE2ETests_FfmpegDownload`, `LiveTvE2ETests`, `ProgramGuideE2ETests` — Playwright/API-E2Es.
- `Components/LiveTvTests`, `Components/NavMenuLiveTvTests` (bUnit) — Live-TV-Seite/Navigations-Freigabe.
- `Controllers/ProgramGuideControllerTests` — Feature-Release-404, AuthZ-Matrix (403 ohne Sender-Freigabe), `hours`-Validierung.
- `ClientLibrary/VideoWebPlayerClientTests_ProgramGuide` — Client-Programmauskunft.
- `ProgramGuideServiceTests` — Now/Upcoming, bare-`MediaItem`-Titel-Fallback (`GetNowAsync_BareMediaItemSlot_FallsBackToItemName`, `GetNowAsync_BareItemSlot_ReturnsNoCardMetadata`), `IsFiller`, `SeasonNumberFromName`.
- `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`, `ProgramSettingsServiceTests*` (u. a. `ProgramScheduleSettings_DefaultTo48HoursAnd15Minutes`, `ProgramSettingsServiceTests_SetupRace`), `ProgramSettingsE2ETests`.

**Keine Tests** zu Clip-Gruppen, Gruppen-Zuordnung oder Karenzzeit — der Bereich ist neu.

## Hilfsmethoden / Testinfrastruktur

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
Zentrales Fixture der Sender-Tests: echtes SQLite (`file:{name}?mode=memory&cache=shared`, FKs wirksam), seeded `Setup { TvSenderEnabled = true }`.

- `CreateAsync(dbName, ct)` — Fixture-Aufbau.
- `CreateService(scheduleOptions?)` — `ProgramScheduleService` mit `ProgramRuleService`/`ProgramSettingsService`/`NullLogger`, optional `IOptions<ProgramScheduleOptions>` — **Einstiegspunkt für Karenzzeit-/Options-Varianten.**
- `CreateGuideService()` — `ProgramGuideService`.
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, movieId?, episodeId?, ruleId?, ruleType, status)` — direkter Slot-Seed.
- `AddUserAsync(email, isAdmin)` / `EnsureSourceAsync(id)` / `GrantSourceAccessAsync(userId, sourceId)` — Nutzer/Quellen/Freigaben.
- `CreateMediaItemAsync(sourceId, duration)` / `SeedProbeItemAsync(name, duration, probedAt, attempts, ct)` — Clip-/Probe-Seeds inkl. erschöpfter Versuche.
- `CreateMovieAsync(name, sourceId, duration, collectionId, releaseDate)` — Film + MediaItem-Link (Trailer-Links lassen sich via `MovieMediaItem { IsTrailer = true }` ergänzen — Muster in den Trailer-Tests).
- `CreateShowAsync(name, episodeCount, sourceId, duration)` — Serie + Staffel + Episoden mit Items.
- `CreateGenreAsync(name, sourceId, isHidden, alternateNames, startDate, endDate)`, `LinkMovieGenreAsync`, `LinkShowGenreAsync` — Genre-Seeds.
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority, episodeCount, slotDuration, dayOfWeek, singleDate, isActive, genreIds, mediaSourceIds)` — Regel-Seed **umgehend die Service-Validierung** (direkter DB-Insert inkl. Genre-/Quell-Joins) — erweiterbar um Gruppen-Links/Karenzzeit.
- `GetChannelAsync`, `SlotsAsync`, `SaveDiagAsync` (FK-Diagnose im Fehlerfall), `AssertNoDisplacedAsync` (Displaced-Dump), `AssertGapless` (Lückenlosigkeits-Assertion `[from, to]`).

### Weitere Helper
- `PlaylistsE2ETestBase`, `LocalMediaPlaylistE2ETestBase`, `MediaBoxContextMenuE2ETestBase` — E2E-Fixtures (`WebApplicationFactory` + Playwright + Test-DB-Seeds).
- `FakeAuthService`, `TestAuthService`, `NoOpVideoWebPlayerClient` — Auth-/Client-Doubles (bUnit-Tests der Admin-Seite).
- `ManualTimeProvider`, `IncrementingTimeProvider`, `ControllableTimeProvider` — steuerbare Zeit.
- `FfmpegPackageServer`, `FfmpegToolDownloadTestDoubles`, `MediaProbeTestDoubles` — ffmpeg/ffprobe-Doubles.
- `CapturingLogger`, `ListLogger`, `SqlCommandRecorder`, `TestHelpers`, `TestImages`, `TestWebHostEnvironment`, `ItemsControllerTestFactory`, `PairingWebApplicationFactory`, `PairingTestDb`, `UnlockedMediaTestHelper`, `BackupUploadSessionServiceTestSupport`.
- DB-Strategie: Sender-Tests laufen über `ProgramScheduleTestDb` gegen echtes SQLite (AGENTS.md-Pflicht für Unique-/FK-/Löschverhalten erfüllt).
