# Tests — Bestandsaufnahme (Issue #290)

Alle Tests laufen gegen echtes In-Memory-SQLite (`Microsoft.Data.Sqlite`, `EnsureCreated`), nicht gegen EF InMemory — die Fixture `Helpers/ProgramScheduleTestDb` kapselt das. Testklassen liegen flach in `VideoWebPlayer.Tests/`.

## Hilfsklassen

### `ProgramScheduleTestDb`
Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` (`internal sealed`, `IDisposable`)

- `CreateAsync(dbName, ct)` (Z. 27–48): Shared-Cache-SQLite, `ApplicationDbContext` ohne Signal-Parameter (also `_programScheduleSignal == null`); seedet `Setup` mit `TvSenderEnabled = true` + `ProgramScheduleUnknownRatingMode = Unrestricted` und die System-Clip-Gruppe „Trailer" (`SeedTrailerClipGroupAsync`, Z. 52–61 — nötig, weil `EnsureCreated` den Migrations-Seed nicht ausführt).
- `CreateService(scheduleOptions?)` (Z. 79–80): `ProgramScheduleService` mit `NullLogger`n, ohne `IProgramScheduleSignal`, ohne `MediaProbeOptions` — optional mit `ProgramScheduleOptions`.
- Seeds: `CreateSlotAsync` (Z. 87–111, direkte Slot-Zeile inkl. `Status`/`IsClipFill`), `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMediaItemAsync` (mit `Duration`, Z. 140–158), `SeedProbeItemAsync` (Z. 163–173), `CreateMovieAsync` (Z. 180–201), `CreateShowAsync` (Z. 206–228), `CreateGenreAsync` + `LinkMovieGenreAsync`/`LinkShowGenreAsync` (Z. 232–260), `CreateClipGroupAsync`/`AddClipToGroupAsync` (Z. 64–77), `CreateRuleAsync` (Z. 263–311, alle Regelparameter inkl. `clipGroupIds`, `placeholderGrace`), `GetChannelAsync`.
- `SlotsAsync` (Z. 333–334): alle Slots aufsteigend.
- **`AssertNoDisplacedAsync`** (Z. 338–347): statisch — schlägt fehl mit Dump aller `Displaced`-Zeilen (Start-Ende, Regel, Typ, Episode, MediaItem), sobald `summary.SlotsDisplaced > 0`; optionaler `context`-Präfix (z. B. „2. Lauf", „Lauf {i}"). Zentrale Helfer-Methode für Idempotenz-Tests — prüft nur `SlotsDisplaced`, **nicht** `SlotsCreated`.
- `AssertGapless` (Z. 350–360): lückenlose Belegung `[from, to]`.
- `SaveDiagAsync` (Z. 317–331): `SaveChanges` mit FK-Diagnose im Fehlerfall.

### Lokale Helfer in `ProgramScheduleServiceTests_Reconcile`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs`

- `LocalUtc` (Z. 19–20): lokale Zeit → UTC (Zeitzonenrobustheit).
- `ScheduledAsync` / `DisplacedAsync` (Z. 22–26): Slot-Filter je Status.

## Testklassen

### `ProgramScheduleServiceTests_Reconcile`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs` — direkt auf `ReconcileAsync` zielende Tests:

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `Reconcile_NoChanges_LeavesEverythingScheduled` (Z. 29) | Ein Reconcile-Lauf nach EnsureCoverage ohne Änderung: `AssertNoDisplacedAsync`, alle Slots bleiben `Scheduled`, Plan lückenlos; toleriert `SlotsCreated` am Fensterrand (Lauf 30 min später) |
| `Reconcile_DeactivatedRule_DisplacesFutureAndRefills` (Z. 59) | Deaktivierung → Zukunft der Regel verdrängt + aufgefüllt |
| `Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows` (Z. 93) | Regel-Löschung → `Displaced`-Zeilen behalten `ProgramRuleId`, Fenster neu belegt |
| `Reconcile_ChangedTimeOfDay_ReplansAtNewTime` (Z. 125) | `TimeOfDay`-Änderung → Neuplanung zur neuen Zeit |
| `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` (Z. 156) | Cursor-Rücklauf: verdrängte Episoden wiederholen sich ohne Sprung |
| `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` (Z. 194) | MediaItem-Löschung → Ausstrahlung verdrängt, nächste Episode rückt nach |
| `Reconcile_PriorityRaised_EvictsLowerPriorityAiring` (Z. 241) | Prioritätsänderung → Eviction der niedrigeren Regel |
| `Reconcile_NewEpisodeInserted_ReplansInAirOrder` (Z. 275) | Neue Episode mittendrin → Umplanung in Sendedreihenfolge |
| `EnsureCoverage_IgnoresDisplacedSlots` (Z. 312) | `Displaced` zählt nicht als Belegung im Folgelauf |
| `Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled` (Z. 341) | Teilweise ausgestrahlte Occurrence bleibt gültig — **inkl. zweitem Reconcile-Lauf** („2. Lauf", `AssertNoDisplacedAsync` mit Kontext) |
| `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable` (Z. 375) | Snapshot-Vergleich: künftige Ausstrahlungen positionstabil über einen Lauf |
| `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap` (Z. 407) | **3× die Worker-Kadenz** (`ReconcileAsync` + `EnsureCoverageAsync`, +15 min je Iteration): kein `Displaced`, keine Überlappung, lückenlos |
| `GetSchedule_ReturnsUpcomingSlotsWithStatus` (Z. 446) | `GetScheduleAsync` liefert `Displaced` mit |
| `Reconcile_LongAiring_StaysScheduled` (Z. 476) | >24-h-Ausstrahlung (Issue #261): Fensterverbreiterung rettet sie — inkl. zweitem Lauf |
| `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` (Z. 508) | Occurrence-Loop reicht mehrere Tage zurück |
| `Reconcile_MultiPartLongAiring_StaysScheduled` (Z. 534) | Mehrteiliger Marathon-Block, Verbreiterungs-Trigger über Kanten-Slot |
| `Reconcile_LongAiring_Shifted_StaysScheduled` (Z. 561) | Verschobener Start: Invader-Evidenz außerhalb des Basisfensters wird nachgeladen |
| `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` (Z. 596) | Kein Trigger-Treffer: validierungsgetriebene Verbreiterung (`HasRescuableInvalid`) rettet |
| `Reconcile_LongAiring_ChangedRule_StillDisplaced` (Z. 628) | Echter Stale-Fall bleibt verdrängt (Verbreiterung rettet nicht zu Unrecht) |
| `Reconcile_LongAiring_BeyondMaxLookback_Displaced` (Z. 655) | Über `MaxOccurrenceLookbackDays` hinaus wird verdrängt (mit `ProgramScheduleOptions`-Override) |

### `ProgramScheduleServiceTests_Boundary`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs`

- `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor` (Z. 30), `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts` (Z. 64), **`Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`** (Z. 108), `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor` (Z. 137), `EnsureCoverage_ShortRemainder_OvershootingItem_Completes` (Z. 175), `EnsureCoverage_RemainderBelowMinRemain_PlansFiller` (Z. 203), `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence` (Z. 235), Options-Normalisierung (Z. 269, 278), `DeleteDisplacedSlots_RemovesOnlyDisplaced`/`_NonAdmin_Throws` (Z. 291, 325), `ResetSchedule_RemovesAllSlotsAndCursors` (Z. 337).

### `ProgramScheduleServiceTests_Interleaving`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs`

- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` (Z. 46), `…_EpisodeOrderPerShow` (Z. 79), `…_SecondRun_ContinuesEachShowCursor` (Z. 102), `StrandOrderKey_IsDeterministic_AndNotAlphabetic` (Z. 134), `…_LeadStrand_FollowsHashOrder_NotAlphabetical` (Z. 154), **`Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled`** (Z. 214), **`Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled`** (Z. 229).

### `ProgramScheduleServiceTests_RingOrder`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_RingOrder.cs`

- `ItemOrderKey_IsDeterministic_AndRuleDependent` (Z. 64), Ring-Reihenfolge-Tests je Materialart (Z. 95–361), **`Reconcile_MixedRing_StaysReproducible`** (Z. 392), `EnsureCoverage_TrailerRing_KeepsAiringsOrder` (Z. 416).

### `ProgramScheduleServiceTests_AgeWindow`
Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_AgeWindow.cs`

- Fenster-Effekte in Erzeugung und Auswahl (Z. 29–193, 335–400, 516–553); Reconcile-Fälle: **`Reconcile_SameWindow_KeepsAiring_TighterWindow_Displaces`** (Z. 215), **`Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces`** (Z. 402), **`Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow`** (Z. 460).

### `ProgramScheduleServiceTests_ClipGroups` / `_ClipFillPeriodBounds`
Dateien: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs`, `_ClipFillPeriodBounds.cs`

- Clip-Füll-Verhalten der Erzeugung (Z. 58–400 der ClipGroups-Datei; Perioden-Bounds Z. 68–114 der zweiten Datei); Reconcile-Fälle: **`Reconcile_ClipFillSlots_StayScheduled`** (Z. 408), `Reconcile_RemovedClipGroupMember_DisplacesAndReplans` (Z. 427), **`Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation`** (Z. 446, zweite Worker-Loop-Wiederholung).

### `ProgramScheduleServiceTests_ClipSource` / `_Trailer` / `_ContentScope`
Dateien: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipSource.cs`, `_Trailer.cs`, `_ContentScope.cs`

- Clip-Quelle: Expansion/Freigabe, **`Reconcile_ClipSourceFillerSlots_StayScheduled`** (Z. 180 ClipSource), `GetRuleDiagnoses_ClipSource_CountsProbePending` (Z. 202).
- Trailer: `EnsureCoverage_TrailerFiller_*` (Z. 25–116), **`Reconcile_KeptTrailerSlot_StaysScheduled`** (Z. 141).
- ContentScope: `EnsureCoverage_AllContent_*`, `…_ScopeSwitch_TakesEffectNextRun` (Z. 167), **`Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots`** (Z. 194), Diagnose (Z. 232).

### `ProgramScheduleServiceTests_Generation` / `_Filler` / `_Conflicts` / `_Access` / `_Diagnosis` / `_CursorSelection` / `_Genres`
Dateien: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_{Generation,Filler,Conflicts,Access,Diagnosis,CursorSelection,Genres}.cs`

- `EnsureCoverage_*`-Grundverhalten: Recurrence-Varianten, Cursor-/Ring-Fortschritt, `…_SecondRun_ExtendsNotDuplicates` (Generation Z. 300), Füller-Logik, Prioritätskonflikte, Freigabe-Prüfung, Diagnose-Zähler (`Diagnosis_*`), `SelectStrandCursor`-Duplikatlogik, Genre-Expansion (inkl. saisonaler/versteckter Fälle `EnsureCoverage_OutOfSeasonGenre_ProducesNothing`, `…_InSeasonGenre_IsScheduled`).

### `ProgramScheduleSignalTests`
Datei: `VideoWebPlayer.Tests/ProgramScheduleSignalTests.cs`

- `Notify_WakesWaiter` (Z. 13), `Notify_IsCoalesced` (Z. 22, Doppelfeuer weckt nur einmal), `Notify_DuringScan_IsSuppressed_UntilScanEnds` (Z. 33).

### `ProgramScheduleWorkerTests`
Datei: `VideoWebPlayer.Tests/ProgramScheduleWorkerTests.cs`

- `Worker_RunsEnsureCoverage_OnStartup` (Z. 16): echter Worker gegen SQLite-ServiceCollection erzeugt Slots.
- `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased` (Z. 23): Feature-Flag aus → keine Slots.
- Helfer `AssertWorkerCreatesSlotsAsync` (Z. 31–117): echter DI-Container, `initialDelay: 0`, Poll bis 30 s; der Worker läuft **ohne** `IProgramScheduleSignal` (nur Intervallpfad).

### `ProgramScheduleAgeWindowTests` / `ProgramSlotTests_DateTimeKind`
Dateien: `VideoWebPlayer.Tests/ProgramScheduleAgeWindowTests.cs`, `ProgramSlotTests_DateTimeKind.cs`

- `IsAllowedAt_*`/`IsValid_*`/`CountBlockedAt_*` (Fensterlogik isoliert, Z. 25–104).
- `ReadSlot_*` (Z. 20–61): `DateTimeKind`-Robustheit der `ProgramSlot`-Zeiten.

## Lücken im Testbestand (bezogen auf Issue #290)

- Kein Test ruft `ReconcileAsync` **zweimal mit identischem `utcNow`** auf — alle Idempotenz-Fälle variieren die Zeit (+10/+15/+25 min), wodurch `coverageStart` wandert und Fensterrand-Effekte zulässig sind.
- `AssertNoDisplacedAsync` prüft nur `SlotsDisplaced == 0`; ein Test auf `SlotsCreated == 0` im Zweitlauf existiert nicht.
- Kein Test simuliert die Probe-Signal-Kaskade (Duration nachträglich geschrieben → `ReconcileAsync` mit verändertem Repräsentanten); `ProgramScheduleTestDb.CreateService` verdrahtet ohnehin kein Signal.
- `ProgramScheduleWorkerTests` nutzt kein `IProgramScheduleSignal` — der Signal-Pfad des Workers (Aufwachen → Reconcile) ist ungetestet.
