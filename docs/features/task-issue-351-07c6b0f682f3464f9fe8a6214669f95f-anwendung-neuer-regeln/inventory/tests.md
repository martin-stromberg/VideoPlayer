# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-10, ~05:29–05:33 UTC (06:29–06:33 +0100, Europe/Berlin)
- Branch und Commit-ID: `task/issue-351-07c6b0f682f3464f9fe8a6214669f95f-anwendung-neuer-regeln`,
  Commit `a711f01c64082ed430e7dff371a25af053b6e97c` („Backmerge from main to staging (#350)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Artefakte dieses Lifecycle-Schritts
  unter `docs/features/task-issue-351-07c6b0f682f3464f9fe8a6214669f95f-anwendung-neuer-regeln/`
  (requirement.md, todo.md, inventory/) — kein produktiver Code, keine Teständerung
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash), .NET SDK 10.0.403,
  Ziel-Framework net10.0, xunit.v3 3.2.2, echte SQLite-In-Memory-DBs
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (einzige Suite des
  Projekts); Befehle aus `.github/workflows/pr-staging-ci.yml`/`staging-ci.yml` (Release-Build +
  drei gefilterte Läufe `Category!=E2E`, `Category=Integration`, `Category=E2E`) sowie
  `AGENTS.md` („`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`")

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build VideoPlayer.sln -c Debug --nologo -v q` | Repo-Wurzel | 0 | — (0 Fehler, 139 bestehende Warnungen) | 0 | — | Konsolenausgabe (nicht archiviert; `-v q`) |
| Basis | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --filter "Category!=E2E"` | Repo-Wurzel | 0 | 2694 | 0 | 1 | [TRX](test-results/baseline-none2e.trx), [Konsole](test-results/baseline-none2e-console.log) |

Hinweis zum Filter: Mehrere `*E2ETests`-Klassen tragen **kein** `Trait("Category","E2E")`
(z. B. `TVSenderAdminE2ETests`, `TVSenderAdminClipGroupsE2ETests`) und liefen daher im
`Category!=E2E`-Lauf mit — inklusive echter Playwright-Szenarien (lokal erfolgreich).

### Nachgewiesene bestehende Testfehler

Keine — der dokumentierte Ausgangslauf endete mit 0 Fehlschlägen. Einziger nicht ausgeführter
Test: `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit`
(outcome `NotExecuted` — Unix-only-Test, unter Windows erwartbar übersprungen; kein Fehler).

### Testlücken und Ausführungsprobleme

- Tests mit `Trait("Category","E2E")` wurden im Basislauf nicht ausgeführt (in der CI eigener
  Job, `Category=E2E` im Release-Build). Umfang: alle mit dem Trait markierten
  `*E2ETests`-Dateien (u. a. `AboutPageE2ETests`, `AgeRatingE2ETests`, …). Kein Aussage über
  ihren Zustand möglich — für die Umsetzung relevant sind sie nicht direkt (kein neues UI),
  vor dem Abschluss-Commit ist die volle Suite erneut zu fahren.
- Keine Build-, Setup- oder Infrastrukturfehler.

## Testklassen (sendeplan-relevant)

### `ProgramScheduleServiceTests_Reconcile` (`ProgramScheduleServiceTests_Reconcile.cs`, 968 Zeilen)

Zentrale Reconcile-Suite: Idempotenz-No-Ops, Deaktivieren/Löschen/Umplanen von Regeln,
Cursor-Rückspulung, laufender Slot, lange Ausstrahlungen/Lookback, Repräsentantenwechsel,
Invalidierungs-Log. Methoden u. a.: `Reconcile_NoChanges_LeavesEverythingScheduled`,
`Reconcile_DeactivatedRule_DisplacesFutureAndRefills`,
`Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows`,
`Reconcile_RuleLeftWindow_DisplacesFutureAirings`,
`Reconcile_ChangedTimeOfDay_ReplansAtNewTime`,
`Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`,
`Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`,
`Reconcile_PriorityRaised_EvictsLowerPriorityAiring`,
`Reconcile_NewEpisodeInserted_ReplansInAirOrder`, `EnsureCoverage_IgnoresDisplacedSlots`,
`Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`,
`GetSchedule_ReturnsUpcomingSlotsWithStatus`, `Reconcile_LongAiring_*`,
`Reconcile_*MediaItemProbed_SlotStaysScheduled`, `Reconcile_UnlinkedMediaItem_StillDisplacesOnce`,
`Reconcile_InvalidAiring_LogsRejectionReason`, `Reconcile_IdenticalUtcNow_SecondRunIsNoOp`.

### `ProgramScheduleServiceTests_PriorityTakeover` (`ProgramScheduleServiceTests_PriorityTakeover.cs`)

Issue #314: Übernahme niederpriorer Fenster durch strikt höherpriore Regel — **deckt die
heute bereits mögliche Verschiebe-Reihenfolge ab**: niedriger priorisierter Block wird
verdrängt und im selben Lauf hinter dem neuen Block neu materialisiert
(`Reconcile_SpecialRuleDisplacesPlannedBlock_EpisodeShiftedNotSkipped`,
`Reconcile_SpecialRuleMultiDay_FollowUpDaysShiftedConsistently` mit Folgelauf-No-Op,
`EnsureCoverage_SpecialRule_TakesOverLowerPriorityWindow`,
`EnsureCoverage_EqualPrioritySuccessor_StillBoundsAiringStart`). Enthält
`SeedCollectionAsync`-Hilfsfunktion (MovieCollection anlegen + Filme mit Releasedatum).

### `ProgramScheduleServiceTests_CollectionOrder` (`ProgramScheduleServiceTests_CollectionOrder.cs`)

Issue #334: geordnete `collection:{id}`-Stränge — `EnsureCoverage_MovieCollection_AirsInReleaseOrder_AndContinuesAcrossRuns`,
`EnsureCoverage_GenreRule_CollectionStrand_KeepsReleaseOrder` (eigener Cursor je Strang),
`EnsureCoverage_MovieCollection_BlockedHead_DefersInsteadOfSkipping`,
`Reconcile_OrderedCollectionRing_StaysReproducible`. **Kein** Test zur Fortsetzungs-Kaskade
beim Verwurf eines Sammlungsfilms — fehlt für die Anforderung.

### `ProgramScheduleServiceTests_Boundary` (`ProgramScheduleServiceTests_Boundary.cs`)

Fenstergrenzen/Overshoot: `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`,
`Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts`,
`Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`,
`Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns`,
`Reconcile_ShiftedAiring_ChainedInvaders_StaysScheduled`,
`Reconcile_ShiftedAiring_EvidenceCleaned_ReplansOnce` u. a.

### `ProgramScheduleServiceTests_Issue315` / `_Issue320`

`ProgramScheduleServiceTests_Issue315.cs`: `Protected`-Regression (laufender Slot nie
geschnitten; einziger Kill = verschärftes Altersfenster am eigenen Slot-Start), geteilte
Gruppen am Lauf-Rand, Wiederhol-Läufe ohne Oszillation.
`ProgramScheduleServiceTests_Issue320.cs`: ungeschriebene Kopf-Takes/Deferral-Lücken/
Cursor-Bias der interleaved Validierung (alle „StaysScheduled").

### `ProgramScheduleServiceTests_Conflicts` (`ProgramScheduleServiceTests_Conflicts.cs`)

`EnsureCoverage_HigherPriorityWins_Overlap` (Hard-Bound-Defer, kein `DroppedConflicts`),
`EnsureCoverage_EqualPriority_LowerRuleIdWins` — **direkter Beleg des Id-Tie-Breaks**, der
die neue Regel verlieren lässt.

### Weitere berührte Suiten

`ProgramScheduleServiceTests_Stability` (Worker-/Simulations-Schleifen ohne Verdrängungs-Fluktuation),
`ProgramScheduleServiceTests_StrandRotation` (Issue #343: fortgesetzter Round-Robin-Zeiger),
`ProgramScheduleServiceTests_PrimetimeRolling` (u. a.
`EnsureCoverage_RuleCreatedMidPrimetime_SchedulesBackToPeriodStart` — Regel mitten in der
Periode angelegt → zurück zum Periodenstart; `EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning`),
`ProgramScheduleServiceTests_PrimetimeChanges`/`_PrimetimeGrace`/`_PrimetimeTrailer`,
`ProgramScheduleServiceTests_Generation`, `ProgramScheduleServiceTests_RingOrder`,
`ProgramScheduleServiceTests_Interleaving`, `ProgramScheduleServiceTests_Filler`,
`ProgramScheduleServiceTests_ClipGroups`, `ProgramScheduleServiceTests_ClipFillPeriodBounds`,
`ProgramScheduleServiceTests_ClipSource`, `ProgramScheduleServiceTests_Genres`,
`ProgramScheduleServiceTests_AgeWindow`/`_PrimetimeAgeWindow`, `ProgramScheduleServiceTests_Access`,
`ProgramScheduleServiceTests_ContentScope`, `ProgramScheduleServiceTests_CoverageBoundary`,
`ProgramScheduleServiceTests_CursorSelection`, `ProgramScheduleServiceTests_Diagnosis`,
`ProgramScheduleServiceTests_Halloween`, `ProgramScheduleServiceTests_InvalidReason`,
`ProgramScheduleServiceTests_Cleanup`, `ProgramScheduleServiceTests_Purge`,
`ProgramScheduleWorkerTests`/`_SideEffects`, `ProgramScheduleSignalTests`,
`ProgramScheduleConventionTests`, `ProgramRuleServiceTests_*`, `ProgramRuleValidityTests`,
`ProgramScheduleAgeWindowTests`, `ProgramScheduleTestDbAssertGaplessTests`,
`ProgramRuleCursor*Tests`, Backup-Tests `VideoWebPlayerBackupDataTests_Program*`,
`ProgramSlotTests_DateTimeKind`.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`, 527 Zeilen)

Echte SQLite-In-Memory-Fixture (`EnsureCreated`, FKs an; `TvSenderEnabled = true`,
`UnknownRatingMode = Unrestricted`): `CreateAsync`, `CreateService` (inkl.
`scheduleOptions`/`logger`-Parameter), `CreateGuideService`, `SeedTrailerClipGroupAsync`,
`CreateClipGroupAsync`/`AddClipToGroupAsync`, `CreateSlotAsync` (direkter Slot-Seed inkl.
Status/ClipFill), `AddUserAsync`/`EnsureSourceAsync`/`GrantSourceAccessAsync`,
`CreateMediaItemAsync`/`SeedProbeItemAsync`, `CreateMovieAsync` (inkl. `collectionId`,
`releaseDate`, Altersfreigabe), **`CreateShowAsync` — legt nur EINE Staffel an**
(`TVShowSeason "Staffel 1"`, Episoden 1..n gleicher Laufzeit); die geforderten
3–8-Staffeln-Serien mit variierenden und gelegentlich doppelten Episodenlängen deckt der
Helfer heute nicht ab — Erweiterung nötig (Anforderung), `CreateGenreAsync`,
`LinkMovieGenreAsync`/`LinkShowGenreAsync`, `CreateRuleAsync` (voller Feldumfang inkl.
`validity`), `GetChannelAsync`, `SlotsAsync`, `SaveDiagAsync`,
`LinkMovieMediaItemAsync`/`LinkEpisodeMediaItemAsync`; Assertions: `AssertNoDisplacedAsync`,
`AssertReconcileNoOpAsync`, `AssertNoSelfDisplacedAsync`, `AssertGapless`.

### `PrimetimeSeed` (`VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs`, 287 Zeilen)

Szenario-Fixture der Primetime-Regel (tägl. 20:15, 225 min, Grace 15 min, Horizont 7 Tage):
`LocalUtc`/`ToLocal`/`LocalMinutesOfDay`/`EveningDateOf`, `SetHorizonDaysAsync`,
`CreateRuleAsync`/`CreateHolidayRuleAsync`, `AddMovieGenreAsync`/`AddShowGenreAsync`/
`AddClipGroupAsync`/`AddTrailerAsync`, `StepAsync` (`ExecutePassAsync`-Iteration),
`AssertPreserved`, `AssertStartsInsidePeriod`, `AiringOf`, `AssertEveryEveningHasTitles`.

### Weitere

`ListLogger<T>` (Log-Zeilen-Assertions), `FakeScheduleSignal`, `FakeProgramScheduleService`
(Queue-basiertes `IProgramScheduleService`-Fake für Worker-Tests), `SetupSeedHelpers`,
`ControllableTimeProvider`/`ManualTimeProvider`/`ScheduleTestTimeProvider` (Worker-Tests).
