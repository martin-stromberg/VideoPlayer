# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): Testlauf gestartet 2026-10-27 06:00:44 MESZ (05:00:44 UTC),
  beendet ca. 06:05 MESZ; Gesamtdauer 4,955 Minuten.
- Branch und Commit-ID: `task/issue-285-1144974891c040b1bd993b777e77ac41-tv-sender-einstellung-inhaltss`
  @ `1ad105f83128e1b87c2e90103a93001ef998f2d5`
  (HEAD: „fix: TV-Sender: Platzhalter-Clips innerhalb der Sendeperiode verteilen, Zeitüberlappungen vermeiden (#293)")
- Uncommittete Änderungen im getesteten Stand: keine Code-Änderungen; einziges
  unverfolgtes Verzeichnis ist `docs/features/task/issue-285-…-tv-sender-einstellung-inhaltss/`
  (requirement.md, todo.md und die hier entstehende Bestandsaufnahme) — reine
  Dokumentation, ohne Einfluss auf Build/Tests.
- Testumgebung und Runtime-/SDK-Versionen: Windows (DESKTOP-CM8OBSG), .NET SDK 10.0.402,
  Zielframework `net10.0`; Playwright 1.62.0 mit lokal installierten Browsern
  (`chromium-1169` bis `-1248` u. a. in `%USERPROFILE%\AppData\Local\ms-playwright`);
  xunit.v3 3.2.2, bunit 2.11.3, EF Core Sqlite 10.0.12.
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - Projektüblich (AGENTS.md, Definition of Done): `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
    — eine Suite, alle Kategorien; Build-Nachweis zusätzlich über
    `dotnet build VideoPlayer.sln` in Debug und Release.
  - CI (`.github/workflows/staging-ci.yml`, `pr-staging-ci.yml`): Release-Build,
    Playwright-Install, dann drei Läufe — `Category!=E2E`, `Category=Integration`
    (aktuell keine Tests mit diesem Trait), `Category=E2E`. E2E = Playwright-Tests
    (50 Dateien, ~238 Tests, `[Trait("Category", "E2E")]`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (Debug, komplette Suite) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "trx;LogFileName=inventory-baseline.trx" --logger "console;verbosity=normal" --results-directory "docs/features/task/issue-285-…/inventory/test-results"` | Repo-Wurzel | 0 | 2494 | 0 | 2 | [TRX](test-results/inventory-baseline.trx), [Konsolen-Log](test-results/inventory-baseline-console.log) |

Hinweis: `dotnet test` baute implizit in Debug. Ein separater Release-Build wurde für die
Bestandsaufnahme nicht ausgeführt (Definition of Done betrifft die Schritte, nicht die
Inventur; in der CI läuft Release).

### Nachgewiesene bestehende Testfehler

Keine — der Basislauf hatte 0 Fehlschläge.

### Testlücken und Ausführungsprobleme

| Test | Grund | Umfang |
|------|-------|--------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Plattformbedingt: prüft das Unix-Executable-Bit, auf Windows übersprungen (`outcome="NotExecuted"` im TRX) | 1 Test |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Explizit deaktiviert per `[Fact(Skip = "…")]` (DevicePlaylistE2ETests_Playback.cs Z. 76): dokumentierter **Bekanntfehler** — `api/continue-watching` antwortet mit HTTP 500, wenn ein Film keiner Filmsammlung angehört (`ContinueWatchingService.Create` ohne Null-Prüfung); laut Kommentar bewusst nicht in jenem Lauf behoben | 1 Test (E2E) |

Keine abgebrochenen Tests; keine Build-/Setup-/Infrastrukturfehler. Playwright-E2E-Tests
liefen vollständig durch (flackern laut AGENTS.md gelegentlich unter Last — in diesem Lauf
kein Ausfall).

## Testklassen (anforderungsnah)

### `ProgramScheduleServiceTests_Access`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Access.cs` — deckt heute die
Freigabe-Prüfung ab (der Code, den die neue Einstellung optional aufhebt).

- `EnsureCoverage_ItemWithoutDuration_IsSkipped` — Item ohne Laufzeit wird gezählt (`SkippedNoDuration`) und nicht eingeplant.
- `EnsureCoverage_NotFullyReleased_IsExcluded` — Quelle nur für Benutzer A freigegeben → `SkippedNoAccess >= 1`, keine Slots; nach Freigabe für B wird eingeplant.
- `EnsureCoverage_UnlockedForAll_IsScheduled` — Serie per `UnlockedMediaEntry` für alle Benutzer freigeschaltet → Episoden werden eingeplant.
- `EnsureCoverage_NoNonAdminUsers_EverythingSchedulable` — ohne Nicht-Admin-Benutzer (`Everything`-Zweig) ist alles planbar.

### `ProgramScheduleServiceTests_ClipSource` (8 Tests)
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipSource.cs` — Füller „Clip-Quelle",
u. a. `EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots` (Z. 107),
`Reconcile_ClipSourceFillerSlots_StayScheduled`, `GetRuleDiagnoses_ClipSource_CountsProbePending`.

### `ProgramScheduleServiceTests_ClipGroups` (19 Tests)
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs` — Clip-Gruppen,
u. a. `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots` (Z. 188),
Trailer-Gruppen-Tests (`EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` Z. 298),
Reconcile-Verhalten (`Reconcile_RemovedClipGroupMember_DisplacesAndReplans` Z. 427).

### `ProgramScheduleServiceTests_Trailer` (5 Tests)
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Trailer.cs` — Materialart
„Trailer anstehender Filme": Vorlauffenster, fehlende Laufzeit, `Reconcile_KeptTrailerSlot_StaysScheduled`.

### `ProgramScheduleServiceTests_Reconcile` (20 Tests)
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` — Verdrängungs- und
Neuplanungsverhalten (deaktivierte/gelöschte Regeln, geänderte Sendezeit, gelöschtes MediaItem,
Priorität, Lookback) — Muster für den „Umschalten auf restriktiv"-Fall der Anforderung.

### `ProgramScheduleServiceTests_Diagnosis` (10 Tests)
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Diagnosis.cs` — Regel-Diagnose,
u. a. `Diagnosis_UnsharedContent_CountsNoAccess` (Z. 140), `Diagnosis_NonAdmin_Throws` (Z. 225).

### Weitere `ProgramScheduleServiceTests_*`-Suiten
`_Generation` (13), `_Boundary` (12), `_Genres` (10), `_ClipSource` (8), `_Interleaving` (7),
`_Trailer` (5), `_ClipFillPeriodBounds` (3), `_CursorSelection` (3), `_Conflicts` (2),
`_Filler` (2) — Expansions-, Cursor- und Konfliktverhalten ohne Freigabe-Fokus.

### `ProgramSettingsServiceTests` / `ProgramSettingsServiceHolidayFederalStateTests` / `ProgramSettingsServiceScheduleTests`
Datei: `VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs` — Muster für Getter/Updater-Tests:
Defaults (`GetHolidayFederalStateAsync_DefaultsToNull`, `ProgramScheduleSettings_DefaultTo48HoursAnd15Minutes`),
Persistenz-Roundtrips, Validierung (`UpdateHolidayFederalStateAsync_UnknownCode_Throws`,
`UpdateProgramScheduleSettings_PersistsAndValidates`).

### `ProgramSettingsServiceLiveStreamTests` (7 Tests)
Datei: `VideoWebPlayer.Tests/ProgramSettingsServiceLiveStreamTests.cs` — Muster für
nullable/validierte Sender-Einstellungen (`GetLiveStreamStandbyMediaItemId_Default_IsNull`,
`UpdateLiveStreamStandbyClipAsync_Roundtrip`/`_Invalid_Throws`).

### `ProgramSettingsServiceTests_SetupRace` (2 Tests)
Datei: `VideoWebPlayer.Tests/ProgramSettingsServiceTests_SetupRace.cs` — Single-Row-Anlage
der `Setups`-Tabelle unter Parallelität.

### `VideoWebPlayerBackupDataTests_*` (Backup-Regressionen)
Verzeichnis: `VideoWebPlayer.Tests/Services/Backups/` — je neue `Setups`-Spalte ein
Alt-Backup-Test: Muster `VideoWebPlayerBackupDataTests_TvSenderEnabled.cs`
(`ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_RestoresDefaultFalse`) und
`VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem.cs` (nullable → `null`).

### `TVSenderAdminTests*` (bUnit)
`VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs` (9 Tests) stellt mit
`CreateContextAsync` (Z. 31–70) die Seite gegen echtes SQLite mit Admin-Claims bereit;
`TVSenderAdminTests_Standby.cs` (3 Tests) zeigt das Einstellungs-Muster: Rendern,
`#tvsender-save-settings` klicken, `Setups`-Wert prüfen (`StandbyCard_ShowsDefault_And_PersistsSelection`),
`StandbyCard_NonAdmin_Blocked` (serverseitige Absicherung statt UI-Hinweis).
`TVSenderAdminTests_ClipGroups.cs` (7), `TVSenderAdminTests_FfmpegDownload.cs` (14).

### `TVSenderAdminE2ETests*` / `ProgramSettingsE2ETests` (Playwright)
`TVSenderAdminE2ETests.cs` (u. a. `Admin_StandbyClip_PickAndPersist`, `NonAdmin_SeesNotAuthorized`),
`TVSenderAdminClipGroupsE2ETests.cs` (5), `TVSenderAdminE2ETests_FfmpegDownload.cs`,
`ProgramSettingsE2ETests.cs` (4) — E2E-Muster für Einstellungs-Persistenz.

### `TvSenderUserAccessTests` (3), `ProgramGuideServiceTests*` (18+7), `SetupsSingleRowMigrationTests` (2)
Sender-Zugriffsliste, Programmauskunft-Service, Single-Row-Index-Migration.

## Hilfsmethoden / Testinfrastruktur

### `ProgramScheduleTestDb`
Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — echtes In-Memory-SQLite
(shared cache) mit FKs; seedet `Setup { TvSenderEnabled = true }` (Z. 40) und die
System-Clip-Gruppe „Trailer" (`SeedTrailerClipGroupAsync`, Z. 49).

- `CreateAsync(dbName, ct)` / `CreateService(scheduleOptions?)` — Fixture +
  `ProgramScheduleService` mit `ProgramRuleService`/`ProgramSettingsService` auf derselben DB.
- `AddUserAsync(email, isAdmin)` / `GrantSourceAccessAsync(userId, sourceId)` / `EnsureSourceAsync(id)` —
  Benutzer- und Freigabe-Seeds für `AccessContext`-Szenarien.
- `CreateMovieAsync` / `CreateShowAsync` / `CreateMediaItemAsync` / `SeedProbeItemAsync` /
  `CreateGenreAsync` / `LinkMovieGenreAsync` / `LinkShowGenreAsync` — Inhalts-Seeds mit
  probefähigen Laufzeiten.
- `CreateRuleAsync(…)` — Regeln inkl. Genre-/Quellfilter- und Clip-Gruppen-Links.
- `CreateClipGroupAsync` / `AddClipToGroupAsync` / `CreateSlotAsync` / `GetChannelAsync` /
  `SlotsAsync` / `SaveDiagAsync` / `AssertNoDisplacedAsync` / `AssertGapless`.
- `CreateGuideService()` — `ProgramGuideService` auf derselben DB.

### `PairingTestDb`
Datei: `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs` — verwendet von den
`ProgramSettingsService*Tests` (liefert `Db` mit SQLite-Fixture).

### `LegacyBackupArchiveBuilder`
Datei: `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs` —
`CreateSeededBackupAsync`, `RemoveTablesAsync`, `RemoveColumnsAsync`, `AppendRowsAsync`,
`ReadTableColumnsAsync` — baut Alt-Backups ohne neue Spalten/Tabellen nach.

### `TVSenderAdminTests.CreateContextAsync`
bUnit-Kontext der Admin-Seite: `ProgramScheduleTestDb`, Admin-Claims (`IsAdmin`,
`NameIdentifier`), alle injizierten Dienste der Seite inkl. `ProgramScheduleSignal`.

### E2E-Basen
`PlaylistsE2ETestBase` (`Helpers/PlaylistsE2ETestBase.cs`): `WebApplicationFactory<Program>`
gegen temp. SQLite + Playwright-Browser; `TVSenderAdminE2ETestBase`
(`Helpers/TVSenderAdminE2ETestBase.cs`) seedet Admin + Sender-Freigabe + Clip-Kandidaten.
