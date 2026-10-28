# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): Testlauf gestartet 2026-10-28 19:06 Uhr +0100 (18:06 UTC), beendet ~19:12 Uhr +0100
- Branch und Commit-ID: `task/5d7afb603ce24d8287dd23e7ee6f7c13-gueltigkeitszeitraum-fuer-rege` @ `d1c71ce40315ef0eb4d705495774bf8192acc588`
- Uncommittete Änderungen im getesteten Stand: nur untracked Lifecycle-Artefakte unter `docs/features/task/5d7afb603ce24d8287dd23e7ee6f7c13-gueltigkeitszeitraum-fuer-rege/` (`requirement.md`, `inventory.md`, `inventory/`); kein Produktivcode, keine Teständerungen
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403, Ziel `net10.0`, Windows (`DESKTOP-CM8OBSG`), Debug-Build; SQLite-In-Memory-Tests + Playwright/Chromium-E2E
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (DoD laut `AGENTS.md` §7, `README.md` Z. 112; CI `.github/workflows/staging-ci.yml` teilt denselben Lauf nach `Category` in `!=E2E`, `Integration`, `E2E` auf). Zusätzlich existiert `tools/MarkdownLinkCheck.Tests` (CI-separat, für diese Anforderung ohne Bezug — nicht ausgeführt)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug` | Repo-Root | 0 | — | — | — | 0 Fehler, 140 Alt-Warnungen (u. a. BL0008) |
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --logger "trx;LogFileName=baseline-test-results.trx"` | Repo-Root | 0 | 2622 | 0 | 2 | [baseline-test-results.trx](test-results/baseline-test-results.trx), [baseline-console.log](test-results/baseline-console.log) |

Gesamtzahl laut TRX-`Counters`: 2624 Tests (`executed=2622`, `failed=0`), Laufzeit ~5,2 min.

### Nachgewiesene bestehende Testfehler

Keine — 0 Fehlschläge im Ausgangslauf.

### Testlücken und Ausführungsprobleme

- `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — `[SKIP]`, im Test begründet: bekannter Befund „api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehört"; bereits in früheren Inventories dokumentiert.
- `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — `[SKIP]`: „Unix-Dateirechte lassen sich nur unter Unix prüfen" (plattformbedingt auf Windows).
- Keine weiteren Einschränkungen; alle übrigen 2622 Tests (Unit + Integration + Playwright-E2E) grün.

## Testklassen

### `ProgramRuleServiceTests_Validation` (`VideoWebPlayer.Tests/ProgramRuleServiceTests_Validation.cs`)
- `CreateRule_WeeklyWithoutDayOfWeek_Throws` — `DayOfWeek`-Pflicht bei `Weekly`
- `CreateRule_OnceWithoutDate_Throws` — `SingleDate`-Pflicht bei `Once`
- `CreateRule_WeekdayWithoutDayOfWeekOrDate_IsAllowed`, `CreateRule_WeekendWithoutDayOfWeekOrDate_IsAllowed` — neue Rhythmen ohne Zusatzfelder
- `CreateRule_WithoutEpisodeCountAndDuration_Throws`, `CreateRule_SeriesFixedTime_RequiresEpisodeCount` — Längen-Pflichtlogik
- `CreateRule_DisallowedMaterialType_Throws` (Theory) — Materialart↔Regelart-Matrix
- `CreateRule_MissingMaterial_ThrowsKeyNotFound`, `CreateRule_EmptyName_Throws`, `GetAllowedMaterialTypes_ReturnsExpectedSets`
- Hilfen: `SetupAsync` (Admin-User + Service + Channel), `Base(...)` (`ProgramRuleInput`-Grundgerüst), `SeedShowAsync`

### `ProgramRuleServiceTests_Crud`
- `CreateRule_ValidInput_PersistsRule`, `GetRulesAsync_ReturnsRulesOrderedByName` (aktuelle Sortierung — für End-Sortierung ungültiger Regeln relevant), `UpdateRule_ChangesFields`, `SetRuleActive_TogglesFlag`, `DeleteRule_RemovesRule`, `CreateRule_DuplicateNameSameChannel_Fails`

### `ProgramRuleServiceTests_Channel` / `_Authorization` / `_GenreMaterial` / `_ClipGroups` / `_ClipSource` / `_Trailer`
- Kanal-Anlage/Kaskade (`DeleteChannel_CascadesRules`), Admin-Erzwingung (`Mutations_NonAdmin_ThrowForbidden`, `Mutations_UnknownUser_ThrowUnauthorized`), Genre-Links inkl. Out-of-Season-Option (`GetGenreMaterialOptions_OutOfSeasonGenre_ListedWithWindowLabel`), Clip-Gruppen-/Karenzzeit-Validierung, Clip-Quellen-/Trailer-Material

### `ProgramScheduleServiceTests_Generation`
- `EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay`, `_WeeklyRule_OnlyOnDayOfWeek`, `_Once_OnlyOnSingleDate`, `_Holiday_OnlyOnHoliday`, `_WeekdayRule_OnlyMondayToFriday`, `_WeekendRule_OnlySaturdaySunday` — decken exakt die `Recurrence`-Zweige von `BuildOccurrences` ab, an denen eine Gültigkeitsprüfung ansetzen würde
- `_SeriesRule_AdvancesEpisodesInAirOrder`, `_SeriesExhausted_WrapsAround`, `_SlotDuration_FillsUntilTarget`, `_PlanIsGapless_WhenFillerExists`, `_OrphanedMaterial_ProducesNothing`, `_SpanningOccurrence_KeepsTrueStart`, `_SecondRun_ExtendsNotDuplicates`

### `ProgramScheduleServiceTests_Reconcile`
- `Reconcile_DeactivatedRule_DisplacesFutureAndRefills` — Vorbild für „ungültige Regel verliert Sendeplätze"
- `Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows` — Verhalten der Slots nach Regel-Löschung (weiche Referenz → `Displaced`, keine Kaskade) — direkt relevant für die automatische Löschung abgelaufener Regeln
- `Reconcile_NoChanges_LeavesEverythingScheduled`, `_WorkerLoop_NoDisplacedAccumulationNoOverlap`, `_IdenticalUtcNow_SecondRunIsNoOp` u. a. Stabilitätstests

### `ProgramScheduleServiceTests_Genres`
- `EnsureCoverage_OutOfSeasonGenre_ProducesNothing`, `_InSeasonGenre_IsScheduled`, `Reconcile_SeasonalGenre_UtcNowBasis_DeterministicExpansion` — bestehende Tests der saisonalen Fenster-Semantik (Referenz für die Regel-Gültigkeit)

### `SeasonalHomeServiceTests`
- `Window_UsesInclusiveUtcDaysAndAnnualRecurrence` (Theory: Tagesgrenzen inklusiv), `Window_HandlesYearCrossingMissingAndInvalidBoundaries` — deckt u. a. ein über-Silvester-Fenster (20.12.–06.01.) und `end < start`/einzelne Grenze ab

### `GermanHolidayCalendarTests`
- Feiertagsberechnung je Bundesland (nur indirekt relevant: `Holiday`-Recurrence)

### `ProgramScheduleServiceTests_Diagnosis` / `_AgeWindow` / `_Boundary` / `_Conflicts` / `_Filler` / `_ClipGroups` / `_ClipFillPeriodBounds` / `_ClipSource` / `_ContentScope` / `_CursorSelection` / `_Interleaving` / `_RingOrder` / `_Trailer` / `_Access`
- `Diagnosis_InactiveRule_StillDiagnosed`, `Diagnosis_CountsWindowBlockedAtRuleEvaluationTime` — Diagnose läuft auch für inaktive Regeln (für „aktuell ungültige" wiederkehrende Regeln relevant)
- `DeleteDisplacedSlots_RemovesOnlyDisplaced`, `ResetSchedule_RemovesAllSlotsAndCursors` (in `_Boundary`)
- `EnsureCoverage_PlanIsGapless_WhenFillerExists` u. a. Lückenlogik — Filler-Regeln füllen Lücken (Geltungsbereich-Frage)

### `ProgramScheduleWorkerTests` / `ProgramScheduleSignalTests`
- `Worker_RunsEnsureCoverage_OnStartup`, `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased` (Feature-Flag-Gate), Signal-Wake/Coalesce/Scan-Suppression — Rahmen für eine Worker-basierte Bereinigung

### `ProgramRuleCursorUniquenessTests` / `ProgramRuleCursorNullStrandKeyMigrationTests`
- Unique-Index/FK-Kaskade der Cursor (Kaskadierung bei Regel-Löschung), Migrations-Testmuster

### `ProgramSlotTests_DateTimeKind` / `TvSenderUserAccessTests` / `ProgramGuideServiceTests*` / `ProgramClipGroup*Tests`
- UTC-Kind-Semantik der Slots, Sender-Freigabe, Programmführer über dem Sendeplan, Clip-Gruppen

### `TVSenderAdminE2ETests` (+ `_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminE2ETests_FfmpegDownload`)
- `Admin_CreateEditDeactivateDeleteRule` — Playwright-CRUD über den Regel-Editor (Dialog `#rule-editor`, Liste `.tvsender-rule-row`); Kandidat für Erweiterung um Gültigkeitsabschnitt/End-Sortierung
- `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`, `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`

### `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests*` + `Helpers/LegacyBackupArchiveBuilder`
- Regressionstest-Muster für Alt-Backups: `CreateSeededBackupAsync`, `RemoveTablesAsync`, `RemoveColumnsAsync` (Index-Metadaten + Zeilen), `AppendRowsAsync`, `ReadTableColumnsAsync`
- `VideoWebPlayerBackupDataTests_ProgramRuleLinks` — Regel-Tabellen/-Links ohne Alt-Spalten; `VideoWebPlayerBackupDataTests` u. a. `..._LegacyBackupWithoutProgramSlotStatus_RestoresWithScheduled`, `..._LegacyBackupWithoutProgramScheduleTablesAndSettings_RestoresSuccessfully` — Vorbild für den geforderten Alt-Backup-Test der neuen Gültigkeits-Spalten

### `EnumContractTests`
- Pinnt nur `RecentEntryType`, `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `FfmpegToolDownloadJobStatus`, `PairingCodeKind` — ein neuer `ProgramRuleValidityMode` wäre hier nicht zwingend zu pinnen

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync` (SQLite shared-memory, `EnsureCreated` mit FKs; seeded `Setup { TvSenderEnabled = true, ProgramScheduleUnknownRatingMode = Unrestricted }`)
- `CreateService` (`ProgramScheduleService` mit `ProgramRuleService`, `ProgramSettingsService`, `NullLogger`), `CreateGuideService`
- `CreateRuleAsync` (Z. 263) — legt `ProgramRule` direkt in DB an (Service-Validierung umgangen); Parameter u. a. `recurrence`, `dayOfWeek`, `singleDate`, `isActive`, `genreIds`, `mediaSourceIds`, `clipGroupIds`, `placeholderGrace` — hier müssten Gültigkeitsparameter ergänzt werden
- `CreateSlotAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync` (mit `startDate`/`endDate` für saisonale Fenster), `CreateMediaItemAsync`/`SeedProbeItemAsync`, `LinkMovieGenreAsync`/`LinkShowGenreAsync`/`LinkMovieMediaItemAsync`/`LinkEpisodeMediaItemAsync`, `EnsureSourceAsync`/`GrantSourceAccessAsync`, `AddUserAsync`, `GetChannelAsync`, `SeedTrailerClipGroupAsync`, `CreateClipGroupAsync`/`AddClipToGroupAsync`
- `SlotsAsync`, `AssertGapless`, `AssertNoDisplacedAsync`, `AssertReconcileNoOpAsync`, `SaveDiagAsync`

### `PairingTestDb` (`VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`)
- `CreateAsync` (SQLite shared-memory + echter `UserManager`), `CreateUserAsync`, `CreateScope` — Basis der `ProgramRuleServiceTests_*`

### `TVSenderAdminE2ETestBase` (`VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`)
- `OpenAdminPageAsAdminAsync`, `UpdateSetupAsync`/`EnableTvSenderAsync`, `SeedSharedCollectionAsync`, `SeedMovieWithItemAsync`, `SeedMovieRuleAsync` (direkter Regel-Seed), `SeedClipItemsAsync`, `SwitchTabAsync` — über `PlaylistsE2ETestBase` (WebApplicationFactory + Playwright)

### Weitere Testhelfer
- `Helpers/ControllableTimeProvider.cs`, `ManualTimeProvider.cs`, `IncrementingTimeProvider.cs` — `TimeProvider`-Doubles (u. a. für `ProgramScheduleWorker`)
- `Helpers/CapturingLogger.cs`, `ListLogger.cs` — Log-Assertions
- `Services/Backups/LegacyBackupArchiveBuilder.cs` — siehe oben
