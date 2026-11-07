# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `AssertGapless` (Helper) — strikte Lücken- + Überlappungsprüfung, Fensterkanten-Ausnahme (ProgramScheduleTestDb.cs:502-521)
- [x] `EnsureCoverage_ClipRing_RotatesAcrossRuns` (B2) — Ring aus Tag-1-Zyklus + Fortsetzung `ring[(count+i)%5]` (ClipGroups.cs:287-305)
- [x] `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` (B3) — 3 Clips, Naht = `ring[(idx+1)%3]` (ClipSource.cs:134-174)
- [x] `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` (B4) — `Distinct == 3` (StrandRotation.cs:259-261)
- [x] `EnsureCoverage_StartedItem_RunsPastNightEnd` + `WeeklyRule_StartedItemRunsPastNightEnd` (B5) — `End-Start == 90min` + Ende jenseits 06:00 (AgeWindow.cs:123-131, :564-571)
- [x] `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` (B6) — Lead-Fenster `movie.Start ∈ (trailer.Start, trailer.Start+lead]` (ClipGroups.cs:331-339)
- [x] `GetSchedule_ReturnsUpcomingSlotsWithStatus` (B7) — `OrderBy(Start)`-Sequenz + `maxCount`-Take (Reconcile.cs:532-537)
- [x] 5× B8 — `AssertReconcileNoOpAsync` in RingOrder.cs:419, CollectionOrder.cs:181, Interleaving.cs:221, Trailer.cs:159; Reconcile.cs:460-462 (`AssertNoDisplacedAsync` + Mengen-Gleichheit der ersten `before.Count` Slots)
- [x] `EnsureCoverage_SlotDuration_FillsUntilTarget` (B10) — Abbruch-Invariante je Ausstrahlungstag (Generation.cs:482-497)
- [x] `EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow` (B11) — `LocalMinutes(Start) == TimeOfDay` (AgeWindow.cs:413-416)
- [x] `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` (B12) — zyklische Gesamtsequenz (Generation.cs:427-433)
- [x] `EnsureCoverage_InterleavedRule_ClipFillDistributed` (B13) — ≥2 Segmente + Gapless-Kette (ClipGroups.cs:423-436)
- [x] `ScheduleTestTimeProvider`, `FakeProgramScheduleService`, `FakeScheduleSignal`, `FakeBackgroundProcessingGate` (B14) — angelegt in Helpers/
- [x] `ProgramScheduleWorkerTests_SideEffects` (B14) — 4 Tests: Live-Signal feuert/nicht feuert, Gate-Lease-Reihenfolge, Intervall-Neulesung
- [x] `Reconcile_DeletedMediaItem_…` (B15) — vollständige Folge über alle Tage + `DoesNotContain(E3)` + toter `var scheduled` entfernt (Reconcile.cs:272-287)
- [x] B9 — dokumentierter Nicht-Befund (Scope-Ausschluss 2; Zieltest nur auf #343-Branch)
- [x] Gegenbeweis B1 — `ProgramScheduleTestDbAssertGaplessTests` (6 Fälle: Gapless, Overlap, Contained, Gap, Straddle, Unterdeckung)

## Offene Aufgaben

–

## Hinweise

- Alle verstärkten Assertions sind gegen den Ist-Code **grün** — kein Produktiv-Bug
  aufgedeckt, kein Produktiv-Fix nötig.
- B8-Offener-Punkt 1 aufgelöst: `ReconcileAsync` meldet an allen vier Stellen `SlotsCreated == 0`
  → die starke `AssertReconcileNoOpAsync`-Variante hält; bei `TomorrowAiringsStayStable`
  zusätzlich Mengen-Gleichheit.
