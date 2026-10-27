# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-27, Laufstart ~10:16 Uhr (UTC+1, lokale Windows-Zeit),
  Ende ~10:21 Uhr; Einzel-Wiederholungen ~10:25 und ~10:27 Uhr
- Branch: `task/issue-289-f979826288cb457eb41c2f788b99650a-tv-sender-platzhalter-bloecke`
- Commit-ID: `147ea8b50c6d1a5437031b633c8eb40addf442ac`
  (`feat: TV-Sender-Einstellung „Inhaltsspektrum" — Freigabe-Prüfung der Planung optional aufheben (Issue #285) (#294)`)
- Uncommittete Änderungen im getesteten Stand: ausschließlich das untracked
  Lifecycle-Verzeichnis `docs/features/task-issue-289-…-tv-sender-platzhalter-bloecke/`
  (requirement.md, todo.md, diese Bestandsaufnahme); kein Produktivcode, keine Teständerungen
- Testumgebung: Windows (Git Bash), .NET SDK 10.0.402, `net10.0`; Playwright-Browser
  chromium-1169…1248 unter `%USERPROFILE%\AppData\Local\ms-playwright` installiert —
  Playwright-E2E lauffähig
- Testsuiten und Quellen der Testbefehle: einzige Testsuite
  `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xunit.v3; Unit-/bUnit-Tests, Controller-
  und Service-Tests gegen echtes SQLite, HTTP-E2E per `WebApplicationFactory`, Playwright-E2E
  per Kestrel+Chromium). Befehl laut AGENTS.md (Definition of Done): volle Suite
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. CI-Referenz
  (`.github/workflows/staging-ci.yml`): Release-Build, dann drei Läufe mit
  `--filter "Category!=E2E"`, `Category=Integration`, `Category=E2E` — die meisten E2E-Klassen
  tragen `[Trait("Category", "E2E")]`; `TVSenderAdminE2ETests*` und
  `TVSenderAdminClipGroupsE2ETests` tragen **keinen** Trait und laufen in CI im Nicht-E2E-Lauf
  mit (sie verkraften fehlende Browser über `SkipBrowser`-Early-Return).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build Debug (Vorlauf) | `dotnet build VideoPlayer.sln -c Debug` | Repo-Wurzel | 0 | – | – | – | 0 Fehler, 140 Warnungen (Bestand: nullable/BL0008) |
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Debug --logger "trx;LogFileName=baseline-issue289.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 1 (Testfehler) | 2510 | 1 | 2 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log), [baseline-issue289.trx](test-results/baseline-issue289.trx) |
| Wiederholung 1 | `dotnet test … --filter "FullyQualifiedName~MediaBoxContextMenuInteractionE2ETests.PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally"` | Repo-Wurzel | 0 | 1 | 0 | 0 | [rerun-flaky-test.log](test-results/rerun-flaky-test.log) |
| Wiederholung 2 | (gleicher Befehl) | Repo-Wurzel | 0 | 1 | 0 | 0 | [rerun-flaky-test.log](test-results/rerun-flaky-test.log) |

Gesamtdauer Baseline: 5,25 Minuten (Runner-Angabe). Gesamtzahl laut Runner: 2513.
Anmerkung zum Exit-Code: Das Konsolen-Log endet mit „Fehler beim Testlauf." (ein
Testfehlschlag); der exakte Exit-Code von `dotnet test` wurde im Pipe-Aufruf nicht separat
festgehalten — nach Runner-Logik 1. Erfolgreich/Fehlgeschlagen/Übersprungen stammen aus der
Runner-Zusammenfassung.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `MediaBoxContextMenuInteractionE2ETests.PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally` | `VideoWebPlayer.Tests/MediaBoxContextMenuInteractionE2ETests.cs` (Playwright-E2E) | `PlaywrightException: Locator expected not to be visible` — `GetByRole(AriaRole.Menu)` blieb 5 s sichtbar (Kontextmenü „Aktionen für Film Eins" unerwartet offen; Timing-Assert) | Baseline, [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) Z. ~2245–2268 |

**Flaky-Einstufung:** Zwei Einzel-Wiederholungen (je ~8–9 s) liefen fehlerfrei — entspricht
dem in AGENTS.md beschriebenen gelegentlichen Playwright-Flackern unter Last. Der Fehlschlag
im Ausgangslauf bleibt dokumentiert und gilt als **preexisting** für genau diesen Testfall.
Betroffen ist ein E2E-Test des Medien-Kontextmenüs — fachlich nicht im Bereich dieser
Anforderung (kein Bezug zu Sendeplan/Clips).

### Testlücken und Ausführungsprobleme

- 2 Tests übersprungen (beides dokumentierte, beabsichtigte Skips — kein unbekannter Zustand):
  - `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
    — hartkodierter `[Fact(Skip = "…")]` (bekannter Alt-Befund: `api/continue-watching` 500
    bei Film ohne Filmsammlung); bereits in der Schritt-5-Baseline des TV-Sender-Projekts
    dokumentiert.
  - `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` —
    `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`: Unix-only-Test, unter Windows
    designbedingt übersprungen.
- Keine abgebrochenen oder nicht ausführbaren Suiten; Playwright-E2E lief vollständig
  (Chromium-1248 vorhanden).
- Getestet wurde die **Debug**-Konfiguration (volle Suite); die CI baut Release — ein
  Release-Baseline-Lauf wurde nicht ausgeführt (AGENTS.md verlangt den Release-Build bei der
  DoD, nicht zwingend für die Bestandsaufnahme).

## Testklassen (für diesen Bereich relevant)

### bUnit: `TVSenderAdminTests` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`)

Kontextfabrik `CreateContextAsync(fixture, adminId, …)` registriert echte Services
(`ProgramScheduleService`, `ProgramRuleService`, `ProgramSettingsService`,
`ProgramClipGroupService`, `ProgramScheduleSignal`) gegen die SQLite-Fixture plus
`VideoWebPlayerClient` mit `EmptyListHandler`.

- `RendersAllTabs_WithRulesActive` — fünf Tabs, Pane-Aktivierung via `active`-Klasse.
- `SwitchesTabs_AndShowsProbeDetailTables` — Tabwechsel + Probe-Detailtabellen.
- `RuleEditor_RendersAsOverlayDialog` — `admin-dialog-overlay`-Muster des Regel-Editors.
- `RuleEditor_TrailerCheckbox_OnlyForFillerRuleType`,
  `RuleEditor_ClipSourceSelect_OnlyForFillerRuleType_AndSetsMaterial`.
- **`Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress`** — eine Karte je Slot
  (`#tvsender-schedule-cards .program-card.tvsender-slot-row`), LIVE-Badge + Fortschritt,
  Genre, Poster, „Geplant"/„Verdrängt" (Displaced-Klasse `tvsender-slot-displaced`).
- **`Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges`** — `tvsender-day-header` je lokalem
  Kalendertag (`data-day`), `tvsender-rule-divider` mit `data-rule-id` genau am Regelwechsel.
- `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes`,
  `ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus`.

### bUnit: `TVSenderAdminTests_ClipGroups`, `_ContentScope`, `_FfmpegDownload`, `_Standby`

- `ClipGroupsTab_Visible_AndShowsSystemTrailerRow`, `ClipGroupsTab_CreateGroup_RendersRow`,
  `MemberList_MarksItemWithoutDuration`, `MemberList_FiltersLive_BySearchTerm`,
  `CandidateList_Empty_ExplainsSelectionRule`, `CandidateSearch_FiltersLive_WithoutSearchButton`,
  `RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix`.

### Service-Tests Clip-Füllung: `ProgramScheduleServiceTests_ClipGroups`

(`VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs`, Fixture
`ProgramScheduleTestDb`, Seed-Helfer `SeedClipAiringAsync` + `AiringSlotsAsync`)

- `EnsureCoverage_GraceRemainder_FillsWithDistributedClips` — Clip-Slots tragen
  `MovieId`/`TVShowEpisodeId = null` und `RuleType = rule.RuleType`; Tests identifizieren
  Clip-Slots per `clipIds`-HashSet der geseedeten Items (gleiche Klassifikation wie der
  Produktivcode).
- Karenz-/Verteilungsfälle (`RemainderAboveGrace`, `ExactlyGrace`, `NoGroups`,
  `LastClip_MayOvershootPeriodEnd`, `ClipTooLargeForFreeRoom_LeavesDocumentedGap`,
  `ClipsSkipItemsWithoutDuration`, `…NotFullySharedSource_YieldsNoClipSlots`,
  `TwoClipGroups_RoundRobinDistributed`, `ClipOvershoot_ReservedBoundWindow_ClipDropped`,
  `ClipRing_RotatesAcrossRuns`).
- Trailer-Gruppe (`TrailerGroup_AirsTrailerBeforeScheduledMovie`, `NoLeadMatch`,
  `TrailerClip_ExcludesItemsWithoutDuration`, `EpisodeCountAndSlotDuration_FillsTailWithClips`,
  `InterleavedRule_ClipFillDistributed`).
- Reconcile: `Reconcile_ClipFillSlots_StayScheduled`,
  `Reconcile_RemovedClipGroupMember_DisplacesAndReplans`,
  `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation`.

### `ProgramScheduleServiceTests_ClipFillPeriodBounds`

- `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd`,
  `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap`,
  `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod`.

### `ProgramScheduleServiceTests_ClipSource` — Füller-„Clip-Quelle" (`MediaSourceClips`)

Andere Machart als Clip-Gruppen-Füllung (`Filler`-Regel), für Offene Frage 2 relevant:
`EnsureCoverage_ClipSourceFiller_FillsGapsWithBareMediaItems`, `…ExcludesClassifiedItems`,
`…SkipsItemsWithoutDurationAndNonVideos`, `…NotFullySharedSource_YieldsNoSlots`,
`…CursorContinuesRingAcrossRuns`, `…MissingSource_IsOrphaned`,
`Reconcile_ClipSourceFillerSlots_StayScheduled`, `GetRuleDiagnoses_ClipSource_CountsProbePending`.

### `ProgramScheduleServiceTests_Reconcile` — u. a. `GetSchedule_ReturnsUpcomingSlotsWithStatus`

Einziger direkter `GetScheduleAsync`-Test: liefert `Scheduled` **und** `Displaced`, filtert
auf `Start >= fromUtc`.

### `ProgramGuideServiceTests` / `ProgramGuideServiceTests_ClipGroups`

- Now/Next-Berechnung, Displaced-Ausschluss, Titel-Fallbacks (`Unbekannt`,
  Bare-`MediaItem.Name` — so erscheinen Clips in der Auskunft), `IsFiller`-Markierung,
  Karten-Metadaten; `Guide_TrailerClipSlot_ShowsMovieDetails` (Trailer-Clip trägt `MovieId`
  → Filmdaten statt Dateiname).

### Modell-/DB-Tests

- `ProgramClipGroupTests` — Unique-Name (NOCASE), Composite-Key der Mitgliedschaft,
  Kaskaden (Gruppe/Regel/MediaItem-Löschung).
- `ProgramClipGroupServiceTests` — CRUD, reservierter Name, Systemgruppen-Schutz,
  Admin-Gate, Kandidatenfilter.
- `ProgramSlotTests_DateTimeKind` — `UtcOnRead`-Konverter gegen echtes SQLite.
- `ProgramRuleCursorNullStrandKeyMigrationTests`, `ProgramRuleCursorUniquenessTests` —
  Cursor-Strandschlüssel (Clip-Ringe nutzen `clipgroup:{id}`).

### Backup-Regressionstests (Muster für neue `ProgramSlot`-Spalte)

- `VideoWebPlayerBackupDataTests_ProgramRuleLinks` —
  `ReadFromAsync_LegacyBackupWithoutProgramRuleLinks_RestoresSuccessfully` +
  `…_CurrentBackup_RestoresProgramRuleLinks` (Legacy-Archiv per `LegacyBackupArchiveBuilder`).
- `VideoWebPlayerBackupDataTests_ProgramScheduleContentScope` — gleiches Muster für eine
  `Setups`-Spalte. `LegacyBackupArchiveBuilder`
  (`VideoWebPlayer.Tests/Services/Backups/`) baut Archive ohne die neuen Tabellen/Spalten.

### Playwright-E2E

- `TVSenderAdminE2ETests` (Basis `TVSenderAdminE2ETestBase` → `PlaylistsE2ETestBase`:
  `WebApplicationFactory` + Kestrel + Chromium, Temp-SQLite-DB; `SkipBrowser`-Early-Return
  statt Skip-Markierung, wenn der Browser nicht startet):
  `Admin_CreateEditDeactivateDeleteRule` (u. a. Tab-Struktur), `Admin_ProbeReset_…`,
  `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`,
  `Admin_StandbyClip_PickAndPersist`, `Admin_ContentScope_SelectAndPersist`,
  `Admin_ContentScope_AllContent_SchedulesUnreleased`.
- `TVSenderAdminE2ETests_FfmpegDownload` — Download-Dialog/Erfolg/Fehler.
- `TVSenderAdminClipGroupsE2ETests` — Gruppen-CRUD, Systemgruppen-Schutz,
  Clip-Zuordnung per Kandidaten-Picker, Regel-Editor-Felder; Hilfen `SeedClipItemsAsync`,
  `SwitchTabAsync` (retry-fest), `OpenSendeplanTabAsync`.
- `ProgramGuideE2ETests` (`[Trait("Category","E2E")]`, `DeviceClientTestBase`) — 401 anonym,
  paired device via Header + `access_token`-Query, Bounded-Preview-400.
- `LiveTvE2ETests`, `LiveTvTests` (bUnit mit `Mock<IProgramGuideApiClient>`) —
  Upcoming-Liste `.livetv-upcoming-row` je `DtoProgramGuideEntry`.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(dbName, ct)` — echtes SQLite InMemory (`mode=memory&cache=shared`),
  `EnsureCreated`, `Setup.TvSenderEnabled = true`, automatischer `SeedTrailerClipGroupAsync`.
- `CreateClipGroupAsync(name, ct)` — benannte Gruppe direkt in die DB.
- `AddClipToGroupAsync(groupId, mediaItemId, ct)` — Mitgliedschaft direkt.
- `SeedTrailerClipGroupAsync(ct)` — reservierte Systemgruppe (Id 1), idempotent
  (EnsureCreated führt keinen Migration-Seed aus).
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, movieId?, episodeId?, ruleId?,
  ruleType, status)` — Slot direkt in die DB (Sendeplan-Seeds ohne Generierungslauf).
- `CreateService(scheduleOptions?)` → `ProgramScheduleService`; `CreateGuideService()` →
  `ProgramGuideService`.
- `CreateRuleAsync(…, clipGroupIds, placeholderGrace, slotDuration, …)` — Regel inkl.
  `ProgramRuleClipGroups`-Links direkt.
- `GetChannelAsync`, `CreateMediaItemAsync` (mit `Duration`/`DurationProbedAt`),
  `CreateMovieAsync`, `CreateShowAsync`, `CreateGenreAsync`, `LinkMovieGenreAsync`,
  `LinkShowGenreAsync`, `SeedProbeItemAsync`, `AddUserAsync`, `EnsureSourceAsync`,
  `GrantSourceAccessAsync`, `SaveDiagAsync` (FK-Diagnose), `SlotsAsync`,
  `AssertNoDisplacedAsync`, `AssertGapless`.

**Lücke für die Anforderung:** Es existiert kein Seed-Helfer, der `ProgramSlot`s als
„Clip-Füllung" markiert — weil es das Kennzeichen nicht gibt. Block-Anzeige-Tests müssten
entweder echte Clip-Füll-Airings über `EnsureCoverageAsync` erzeugen (Muster
`SeedClipAiringAsync`) oder — je nach gewählter Clip-Erkennung — `ProgramRuleClipGroups`-Links
+ `ProgramClipGroupItems`-Mitgliedschaften passend zu den Slot-`MediaItemId`s seeden.
