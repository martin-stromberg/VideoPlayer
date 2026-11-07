# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| B1 `AssertGapless` auf Lücken- + Überlappungsprüfung | Programmablauf B1 + Änderung `ProgramScheduleTestDb` | Alle 9 Aufrufer-Tests + Gegenbeweis-Szenario 3 | Abgedeckt |
| B2 Ringfortsetzung ClipGroups | Änderung `EnsureCoverage_ClipRing_RotatesAcrossRuns` | Selbsttest + Ist-Lauf | Abgedeckt |
| B3 ≥3 Clips + Naht-Erwartung ClipSource | Änderung `CursorContinuesRingAcrossRuns` | Selbsttest | Abgedeckt |
| B4 `Distinct == 3` StrandRotation | Änderung `EpisodeCountBelowStrandCount_StillRotates` | Selbsttest | Abgedeckt |
| B5 `End - Start == Laufzeit` AgeWindow ×2 | Beide Tests geändert | Selbsttest | Abgedeckt |
| B6 Lead-Fenster-Bezug Trailer | Änderung `AirsTrailerBeforeScheduledMovie` | Selbsttest | Abgedeckt |
| B7 `OrderBy(Start)` + `maxCount`-Take GetSchedule | Änderung `GetSchedule_ReturnsUpcomingSlotsWithStatus` | Selbsttest | Abgedeckt |
| B8 5× StaysScheduled → NoOp/Mengen-Gleichheit | Änderungen Reconcile/RingOrder/CollectionOrder/Interleaving/Trailer + Offener Punkt 1 mit Fallback | Selbsttest | Abgedeckt |
| B9 Mindest-Tageszahl | Scope-Ausschluss 2: Zieltest existiert nur auf #343-Branch — dokumentierter Nicht-Befund | n/a (begründet) | Abgedeckt (dokumentiert) |
| B10 Abbruch-Invariante FillsUntilTarget | Änderung `SlotDuration_FillsUntilTarget` | Selbsttest | Abgedeckt |
| B11 `LocalMinutes(Start) == TimeOfDay` MPAA-Theory | Änderung MPAA-Theory | Selbsttest | Abgedeckt |
| B12 Sequenzvergleich Generation | Änderung `AdvancesEpisodesInAirOrder` | Selbsttest | Abgedeckt |
| B13 Verteilung + Gapless-Kette ClipFillDistributed | Änderung `ClipFillDistributed` | Selbsttest | Abgedeckt |
| B14 Worker-Nebeneffekte (Signal/Gate/Intervall) | Neue Helpers/Fakes + `ProgramScheduleWorkerTests_SideEffects` mit 4 Tests | 4 neue Tests | Abgedeckt |
| B15 volle Folge über alle Tage DeletedMediaItem | Änderung `DeletedMediaItem…` + toten `var scheduled` entfernen | Selbsttest | Abgedeckt |
| Jede Verstärkung verifiziert (grün/Rot→Fix) | Umsetzungsreihenfolge: Einzeltest je Befund | Gefilterte `dotnet test`-Läufe | Abgedeckt |
| Volle Suite grün, Debug+Release fehlerfrei | Umsetzungsreihenfolge Schritt 5 | Gesamtlauf | Abgedeckt |
| Helper-Assertions kompatibel zu allen Aufrufern | Signatur `AssertGapless` unverändert, Fensterkanten-Ausnahme | Aufrufer-Tests | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

–

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Keine Benutzerflüsse betroffen (reine Testhärtung auf Service-Ebene) | – | Nicht erforderlich — Plan begründet: kein UI-/CLI-/API-Pfad geändert; reale E2E-Abnahme dokumentiert verzichtet (Anwender, 2026-11-09) |

## Fehlende oder unvollständige Planbestandteile

–

## Hinweise

- B9 ist bewusst als dokumentierter Nicht-Befund geführt: die Zielstelle existiert auf
  diesem Branch nicht (nur auf dem ungemergten #343-Branch); der Umstand ist dem
  Anwender im Abschlussbericht zu melden, damit die Härtung beim #343-Merge nachgezogen
  werden kann.
- Offene Punkte 1–3 in `plan.md` sind Laufzeit-abhängig und werden mit der dokumentierten
  Annahme bzw. dem dokumentierten Fallback (Offener Punkt 1) umgesetzt.
