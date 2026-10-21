# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, ~08:05–08:15 (MESZ, UTC+2)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-4-sendeplan-nachfuehren` @ `86afda8` („step: Schritt 4 begonnen")
- Uncommittete Änderungen im getesteten Stand: nur neue Lifecycle-Artefakte unter `docs/features/...-schritt-4-sendeplan-nachfuehren/` (keine Quellcode-Änderungen)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, TargetFramework `net10.0`, xUnit.net VSTest Adapter 3.1.5, Playwright-Browser vorhanden (E2E laufen real); Windows, lokaler Build `Debug`
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Suite `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit, inkl. Playwright-E2E); Befehl gemäß AGENTS.md DoD: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (Debug, nach `dotnet build VideoPlayer.sln -c Debug` = 0 Fehler) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Wurzel | 1 | 1850 | 1 | 1 | [baseline-run-1.log](test-results/baseline-run-1.log) |
| Retry Einzeltest | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "FullyQualifiedName~PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest"` | Repo-Wurzel | 0 | 1 | 0 | 0 | [baseline-run-1-retry-flake.log](test-results/baseline-run-1-retry-flake.log) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs:178` (via `PlaylistsE2ETestBase.SelectEntryAsync`) | `System.TimeoutException : Timeout 30000ms exceeded.` — `waiting for Locator("#playlist-detail-selected-entry") to be visible` (Playwright-Flake unter Last) | Baseline-Lauf, [baseline-run-1.log](test-results/baseline-run-1.log); **beim isolierten Einzelretry bestanden** → als Flake dokumentiert, nicht als dauerhafter Fehler (AGENTS.md: einmalige Playwright-Ausfälle werden wiederholt und ehrlich benannt) |

### Testlücken und Ausführungsprobleme

- `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — 1× übersprungen (Alt-Skip, bekannt aus Projekt-Kontext; Skip-Bedingung im Test selbst).
- Keine Build-, Setup- oder Infrastrukturfehler.

## Testklassen

### `ProgramScheduleServiceTests_Generation` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_Generation.cs`)
- `EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay` — tägliche Regel → Sendeplätze je Tag zur `TimeOfDay`
- `EnsureCoverage_WeeklyRule_OnlyOnDayOfWeek` / `EnsureCoverage_Once_OnlyOnSingleDate` / `EnsureCoverage_Holiday_OnlyOnHoliday` — Wiederholungsmuster
- `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` — Serien-Fortschritt inkl. `ProgramRuleCursor`, Lookback konsumiert
- `EnsureCoverage_SeriesExhausted_WrapsAround` — Wrap-around bei erschöpfter Serie
- `EnsureCoverage_SlotDuration_FillsUntilTarget` — Ziellänge statt Folgenanzahl
- `EnsureCoverage_PlanIsGapless_WhenFillerExists` — lückenlose Belegung via `AssertGapless`
- `EnsureCoverage_OrphanedMaterial_ProducesNothing` — verwaiste `MaterialId` → keine Slots, `OrphanedRules` gezählt
- `EnsureCoverage_SpanningOccurrence_KeepsTrueStart` — laufende Ausstrahlung behält echten Start
- `EnsureCoverage_SecondRun_ExtendsNotDuplicates` — extend-only, keine Duplikate

### `ProgramScheduleServiceTests_Conflicts` (`ProgramScheduleServiceTests_Conflicts.cs`)
- Prioritätsauflösung zwischen überschneidenden Regeln (höhere `Priority` gewinnt, Tiebreak Regel-`Id`), atomare Ausstrahlungs-Verwerfung

### `ProgramScheduleServiceTests_Filler` (`ProgramScheduleServiceTests_Filler.cs`)
- Füller-Lückenfüllung: nur passende Inhalte, letzter Füller darf Horizont-Ende überschreiten, Restlücken-Sekunden

### `ProgramScheduleServiceTests_Access` (`ProgramScheduleServiceTests_Access.cs`)
- Vollfreigabe-Filter: `MediaSourceUsers`-Vollabdeckung, `UnlockedMediaEntries` für Show/Collection, Admin-only-Publikum → nichts planbar

### `ProgramScheduleWorkerTests` (`ProgramScheduleWorkerTests.cs`)
- `Worker_RunsEnsureCoverage_OnStartup` — echter DI-Container + In-Memory-SQLite; Worker erzeugt Sendeplätze

### `ProgramRuleServiceTests_{Crud,Validation,Authorization,Channel}`
- Regel-CRUD, Fachvalidierung, Admin-Erzwingung (`ForbiddenAccessException`), Kanal-Anlage — **Trigger-Punkt-Kandidaten**: hier wäre die Signal-Auslösung bei Create/Update/SetActive/Delete zu testen

### `ProgramSettingsServiceTests` / `ProgramSettingsE2ETests`
- Horizont/Intervall-Getter/Updater; E2E der Einstellungen

### `TVSenderAdminE2ETests` (`TVSenderAdminE2ETests.cs`, Playwright)
- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter E2E über Regel-Lebenszyklus + Sender-Einstellungen
- `NonAdmin_SeesNotAuthorized` — Admin-Gate

### `Services/Backups/VideoWebPlayerBackupDataTests`
- Alt-Backup-Regressionstests (`OptionalRestoreTables`/`OptionalRestoreColumns`); hier gehört der neue Test für `ProgramSlots.Status` hin

### `Services/PlaylistBackfillMarkerHookTests`, `PlaylistBackfillMarkerProcessingTests`, `PlaylistBackfillCoordinatorTests`
- Referenztests für Marker-Hook + Signal-Pfad

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — In-Memory-SQLite (shared cache), `EnsureCreated`
- `CreateService()` — `ProgramScheduleService` mit `ProgramRuleService` + `ProgramSettingsService` + `NullLogger`
- `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync` — Publikums-Setup
- `CreateMediaItemAsync`, `CreateMovieAsync`, `CreateShowAsync` — Inhalte mit `Duration`
- `CreateRuleAsync` — Regel direkt in DB (Validierung umgangen; gut für Vorbedingungen)
- `GetChannelAsync`, `SlotsAsync` — Zugriff
- `AssertGapless(slots, from, to)` — Lückenlosigkeits-Assertion
- `SaveDiagAsync` — SaveChanges mit FK-Diagnose
