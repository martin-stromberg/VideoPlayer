# Tasks: Sender-Testhärtung — nicht sichergestellte Erwartungen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Test-Helper | `AssertGapless` auf Lücken- + Überlappungsprüfung verschärfen (B1) | Erledigt | `ProgramScheduleTestDbAssertGaplessTests` (6 Fälle) + 63 Aufrufer-Tests |
| 2 | Tests | Ringfortsetzung ClipRing (B2) | Erledigt | `EnsureCoverage_ClipRing_RotatesAcrossRuns` |
| 3 | Tests | 3er-Ring + Naht ClipSource (B3) | Erledigt | `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` |
| 4 | Tests | `Distinct == 3` StrandRotation (B4) | Erledigt | `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` |
| 5 | Tests | Laufzeit + Nachtende AgeWindow ×2 (B5) | Erledigt | `EnsureCoverage_StartedItem_RunsPastNightEnd`, `…_WeeklyRule_StartedItemRunsPastNightEnd` |
| 6 | Tests | Lead-Fenster-Bezug Trailer (B6) | Erledigt | `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` |
| 7 | Tests | GetSchedule Ordnung + maxCount (B7) | Erledigt | `GetSchedule_ReturnsUpcomingSlotsWithStatus` |
| 8 | Tests | 5× StaysScheduled → NoOp/Mengen-Gleichheit (B8) | Erledigt | `AssertReconcileNoOpAsync`-Aufrufe in RingOrder/CollectionOrder/Interleaving/Trailer + `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable` |
| 9 | Dokumentation | B9 Nicht-Befund (#343-Branch) | Erledigt | Kein direkter Test — dokumentiert in plan.md/test-results.md |
| 10 | Tests | Abbruch-Invariante FillsUntilTarget (B10) | Erledigt | `EnsureCoverage_SlotDuration_FillsUntilTarget` |
| 11 | Tests | MPAA Start == TimeOfDay (B11) | Erledigt | `EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow` (3 Cases) |
| 12 | Tests | Sequenz ohne Ring-Neustart (B12) | Erledigt | `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` |
| 13 | Tests | Segment-Verteilung + Gapless (B13) | Erledigt | `EnsureCoverage_InterleavedRule_ClipFillDistributed` |
| 14 | Test-Infrastruktur | `ScheduleTestTimeProvider` (B14) | Erledigt | `Worker_RereadsIntervalEachIteration` |
| 15 | Test-Infrastruktur | 3 Fakes (B14) | Erledigt | Alle `ProgramScheduleWorkerTests_SideEffects`-Tests |
| 16 | Tests | 4 Worker-Nebeneffekt-Tests (B14) | Erledigt | `Worker_PassWithChanges_NotifiesLiveScheduleChanged`, `…_DoesNotNotify…`, `…_WrapsPassInGateLease`, `…_RereadsIntervalEachIteration` |
| 17 | Tests | Volle Folge + toter Code (B15) | Erledigt | `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` |
| 18 | Verifikation | Gegenbeweis AssertGapless | Erledigt | `ProgramScheduleTestDbAssertGaplessTests` |
| 19 | Verifikation | Volle Suite + Debug/Release-Build | Erledigt | Volllauf 2929/2933 (2 E2E-Flakes, im Retry grün); Debug+Release-Build 0 Fehler |
