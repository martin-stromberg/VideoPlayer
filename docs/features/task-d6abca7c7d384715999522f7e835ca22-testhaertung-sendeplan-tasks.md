# Tasks: Sender-Testhärtung — nicht sichergestellte Erwartungen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Test-Helper | `AssertGapless` in `ProgramScheduleTestDb` auf Lücken- + Überlappungsprüfung verschärfen (B1) | Offen | — |
| 2 | Tests | `EnsureCoverage_ClipRing_RotatesAcrossRuns`: erwartete Ringfortsetzung assertieren (B2) | Offen | — |
| 3 | Tests | `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns`: 3. Clip säen, Naht gegen erwartetes Ringelement (B3) | Offen | — |
| 4 | Tests | `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates`: `Distinct == 3` (B4) | Offen | — |
| 5 | Tests | `EnsureCoverage_StartedItem_RunsPastNightEnd` + Weekly-Variante: `End - Start == Laufzeit`, `End` jenseits Nachtende (B5) | Offen | — |
| 6 | Tests | `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie`: Lead-Fenster-Bezug (B6) | Offen | — |
| 7 | Tests | `GetSchedule_ReturnsUpcomingSlotsWithStatus`: `OrderBy(Start)`-Sequenz + `maxCount`-Take (B7) | Offen | — |
| 8 | Tests | 5 „StaysScheduled"-Reconcile-Tests: `AssertReconcileNoOpAsync` bzw. Mengen-Gleichheit (B8) | Offen | — |
| 9 | Dokumentation | B9 als dokumentierten Nicht-Befund führen (Zieltest nur auf #343-Branch) | Offen | — |
| 10 | Tests | `EnsureCoverage_SlotDuration_FillsUntilTarget`: Abbruch-Invariante pro Ausstrahlungstag (B10) | Offen | — |
| 11 | Tests | `EnsureCoverage_MpaaRatedMovie_RespectsMappedWindow`: `LocalMinutes(Start) == TimeOfDay` (B11) | Offen | — |
| 12 | Tests | `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder`: Sequenzvergleich ohne Ring-Neustart (B12) | Offen | — |
| 13 | Tests | `EnsureCoverage_InterleavedRule_ClipFillDistributed`: Segment-Verteilung + Gapless-Kette (B13) | Offen | — |
| 14 | Test-Infrastruktur | `ScheduleTestTimeProvider` Helper anlegen (B14) | Offen | — |
| 15 | Test-Infrastruktur | Fakes `FakeProgramScheduleService`, `FakeScheduleSignal`, `FakeBackgroundProcessingGate` anlegen (B14) | Offen | — |
| 16 | Tests | `ProgramScheduleWorkerTests_SideEffects`: 4 Worker-Nebeneffekt-Tests (B14) | Offen | — |
| 17 | Tests | `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`: volle Folge über alle Tage + toten `var scheduled` entfernen (B15) | Offen | — |
| 18 | Verifikation | Gegenbeweis B1: künstlich überlappter Slot lässt `AssertGapless` fehlschlagen | Offen | — |
| 19 | Verifikation | Volle Suite + Debug/Release-Build grün | Offen | — |
