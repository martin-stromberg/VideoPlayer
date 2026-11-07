# Befund-Mapping B1–B15 (Ist-Zeilen auf diesem Branch)

Die Zeilenangaben aus `issue.md` wurden gegen den Stand dieses Branches geprüft; Abweichungen
sind notiert. Dateien liegen unter `VideoWebPlayer.Tests/` bzw. `VideoWebPlayer/Services/`.

## Priorität 1 — zentraler Helper

| Befund | Ist-Fundstelle | Ist-Stand |
|--------|----------------|-----------|
| B1 `AssertGapless` prüft nur `slot.Start <= cursor` → Überlappungen passieren | `Helpers/ProgramScheduleTestDb.cs:414-424` | bestätigt: `Assert.True(slot.Start <= cursor)` — ein Slot, der vor `cursor` beginnt (Overlap) oder komplett darin liegt (cursor läuft sogar rückwärts), schlägt nicht fehl. Aufrufer: Reconcile.cs:55/89/197/497, ContentScope.cs:228, Filler.cs:34/77, Trailer.cs:57, Generation.cs:393/497 |

## Priorität 2 — Mechanismus statt Invariante

| Befund | Ist-Fundstelle | Ist-Stand |
|--------|----------------|-----------|
| B2 `ClipRing_RotatesAcrossRuns` nur „erster Clip anders" | `ProgramScheduleServiceTests_ClipGroups.cs:267-295` | bestätigt: `Assert.NotEqual(firstDayClips[0], secondDayClips[0])` + Cursor-Typ-Check; die erwartete Ringfortsetzung (Tag 1: 12 Entnahmen aus 5er-Ring → Tag 2 startet bei Ringindex 2) wird nicht verglichen |
| B3 `CursorContinuesRingAcrossRuns` 2-Elemente-Ring | `ProgramScheduleServiceTests_ClipSource.cs:134-161` | bestätigt: nur 2 Clips gesät (`clipA`, `clipB`); Naht-Assertion `lastBefore != firstNew` — bei 2 Elementen ist jede Fortsetzung „anders", ein Cursor-Rückfall auf Position 0 bleibt bei ungeradem Slot-Stand unsichtbar |
| B4 `EpisodeCountBelowStrandCount_StillRotates` `Distinct >= 2` | `ProgramScheduleServiceTests_StrandRotation.cs:246-261` | bestätigt: `leads.Distinct().Count() >= 2` bei 3 Strängen/4 Abenden — zwei rotierende Stränge genügen, ein nie bedienter dritter fällt nicht auf |

## Priorität 3 — Invariante im Testnamen, Assertion fehlt

| Befund | Ist-Fundstelle | Ist-Stand |
|--------|----------------|-----------|
| B5 „StartedItem runs past night end": `End` nie geprüft | `ProgramScheduleServiceTests_AgeWindow.cs:106-124` und `:537-557` | bestätigt: beide Tests assertieren nur `LocalMinutes(s.Start) == 5:30`; `End - Start == Laufzeit (90 min)` und `End > 06:00` fehlen |
| B6 `AirsTrailerBeforeScheduledMovie` nur Existenz | `ProgramScheduleServiceTests_ClipGroups.cs:297-321` | bestätigt: `Assert.NotNull(trailerSlot)` + `MovieId == movieX.Id` — kein Lead-Fenster-/Reihenfolge-Bezug (`movie.Start ∈ (trailer.Start, trailer.Start+lead]`) |
| B12 `Generation.cs` `Contains(E5)` statt Sequenz | `ProgramScheduleServiceTests_Generation.cs:422-430` | bestätigt: `Assert.True(last.Count > 4)` + `Assert.Contains(episodes[4].Id, ...)` — die Fortsetzungssequenz (E5, dann Wrap → E1 …) wird nicht verglichen |
| B13 `ClipFillDistributed` nur `Contains` | `ProgramScheduleServiceTests_ClipGroups.cs:383-405` | bestätigt: `Assert.Contains(slots, s => s.MediaItemId == clip.Id)` — keine Verteilungs-/Lückenkette-Prüfung der Segmente |

## Priorität 4 — Asymmetrien und Nebeneffekte

| Befund | Ist-Fundstelle | Ist-Stand |
|--------|----------------|-----------|
| B7 `GetSchedule` ohne Ordnungs-/Limit-Prüfung | `ProgramScheduleServiceTests_Reconcile.cs:500-523` (`GetSchedule_ReturnsUpcomingSlotsWithStatus`) | bestätigt: `NotEmpty` + `Contains Displaced` + `Start >= fromUtc` — kein `OrderBy(Start)`-Sequenzvergleich, kein `maxCount`-Take |
| B8 „StaysScheduled" ohne `SlotsCreated == 0` | `RingOrder.cs:396-418` (`Reconcile_MixedRing_StaysReproducible`), `CollectionOrder.cs:158-181` (`Reconcile_OrderedCollectionRing_StaysReproducible`), `Interleaving.cs:211-221` (`Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled`), `Trailer.cs:140-163` (`Reconcile_KeptTrailerSlot_StaysScheduled`), `Reconcile.cs:429-459` (`Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable`, `before ⊆ after`) | bestätigt: nur `AssertNoDisplacedAsync` bzw. Teilmengen-Vergleich — ein Reconcile, der nichts verdrängt, aber Inhalte/Zeiten umschreibt oder Slots erzeugt, bleibt unsichtbar. Zu klären: ob `ReconcileAsync` am Horizont-Ende legitimerweise `SlotsCreated > 0` meldet (dann Mengen-Gleichheit statt NoOp) |
| B9 `if (served.Count == 0) break` lautlos | `ProgramScheduleServiceTests_StrandRotation.cs` | **Nicht anwendbar auf diesem Branch:** die Funktion `EnsureCoverage_SecondRun_ContinuesDisjointAcrossRunBoundary` existiert nur auf `task/d6abca7c7d384715999522f7e835ca22-strang-ueberlappung` (Issue #343, noch nicht in staging). Dort: `:301-307` — passt zu den issue.md-Zeilen. Die Zeilenangaben zu B4 (`:318-332`) und B9 stammen aus jenem Branch-Stand |
| B10 `FillsUntilTarget` ohne Abbruch-Invariante | `ProgramScheduleServiceTests_Generation.cs:453-477` (`EnsureCoverage_SlotDuration_FillsUntilTarget`) | bestätigt: `slots.Count >= 3` + Dauer der ersten 3 — nicht geprüft: kein weiterer Titel nach Erreichen des Targets (pro Ausstrahlungstag), kein Nachweis, dass die Titelkette genau bis Overshoot läuft |
| B11 MPAA-Theory ohne Startzeit-Check im erlaubten Fall | `ProgramScheduleServiceTests_AgeWindow.cs:380-406` | bestätigt: erlaubter Fall assertiert nur `Contains(rule, movie)` — `LocalMinutes(s.Start) == TimeOfDay` fehlt |
| B14 Worker-Nebeneffekte ungetestet | `ProgramScheduleWorker.cs:62` (Intervall je Iteration), `:66-69` (Gate-Lease), `:77-79` (`NotifyLiveScheduleChanged` bei Displaced/Created>0) | bestätigt: `ProgramScheduleWorkerTests` deckt keinen der drei Punkte ab; Fakes machbar (siehe worker.md) |
| B15 `DeletedMediaItem` `Any(SequenceEqual)` statt Folge über alle Tage | `ProgramScheduleServiceTests_Reconcile.cs:238-283` (`Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`) | bestätigt: prüft nur, dass **irgendein** Tag `[E4,E5]` zeigt — die komplette Folge (E1,E2 | E4,E5 | E6,E7 …) und dass E3 nirgends mehr geplant ist, wird nicht verglichen. Bonus-Fund: `var scheduled = await ScheduledAsync(...)` in :282 ungenutzt (toter Code) |

## Nicht-Ziel-Bewertung (spröde Gegen-Assertions)

| Stelle | Bewertung |
|--------|-----------|
| `ClipGroups.cs:232-237` — strikte A/B-Alternation über 2 Clip-Gruppen | Round-Robin-Design; ein legitimes Überspringen leerer Gruppen ist im Szenario nicht gegeben (8 Clips je Gruppe) → Assertion ist korrekt scharf, bleibt |
| `PrimetimeGrace.cs:139-144` — strikte Gruppen-Alternation der Clip-Slots | Gleiches Muster, beide Gruppen haben 4 Clips → bleibt |
