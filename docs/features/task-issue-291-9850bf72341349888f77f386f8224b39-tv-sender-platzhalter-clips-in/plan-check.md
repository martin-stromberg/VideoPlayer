# Plan-Check: Platzhalter-Clips innerhalb der Sendeperiode (#291)

**Status: Plan vollständig**

Geprüft gegen `requirement.md`, `inventory/` und den Code in `ProgramScheduleService.cs`.

## Geprüfte Punkte

- **Defekt A (Periodenende):** Der Plan ersetzt `airing[0].Start` durch den letzten Occurrence-Start ≤ Kopf. Herleitung ist für alle drei Startfälle korrekt: nominaler Start, invader-verschobener Start (`ShiftPastInvaders` → realer Start > Occurrence-Start) und Teil-Ausstrahlung (Kopf endete vor `coverageStart`). Lookback-Occurrences sind in beiden Aufrufern vorhanden (`occurrences` bzw. `window.Occurrences` mit Reconcile-Lookback).
- **Defekt B (Ketten-Ende):** `fill = min(rest, chainEnd - lastEnd)` begrenzt die Summe der eingefügten Clip-Zeit; da die Kette lückenlos ist, gilt `letzter Titel-Ende = lastEnd + ΣClips ≤ chainEnd` — kein Titel kann in belegten/reservierten Raum ragen. Korrekt, weil belegte Intervalle innerhalb `[airing[0].Start, lastEnd)` per Materialisierungs-Konfliktprüfung ausgeschlossen sind.
- **Defekt C (Tail-Clip):** Uniform-Prädikat `pos < periodEnd && pos < chainEnd && pos + Duration <= tailLimit` lässt den Überhang eines begonnenen Clips zu (`tailLimit` > `periodEnd` möglich), blockiert aber Starts hinter jeder Grenze und Enden hinter `tailLimit` (belegt/höherpriores Bound/Horizont). Bestehender Test `EnsureCoverage_LastClip_MayOvershootPeriodEnd` bleibt erfüllbar.
- **Existing tests im Blick:** `GraceRemainder_FillsWithDistributedClips` (Einzelregel → `chainEnd` = nächster eigener Occurrence-Start ≥ Periodenende → kein Einfluss), `ClipOvershoot_ReservedBoundWindow_ClipDropped` (höherpriores Bound → beide Grenzen deckeln), `Reconcile_*`-Tests (Pfad `ReconcileAsync` → `InsertClipFill` mit `window.Occurrences`).
- **Atomizität:** `blocked`-Buchführung (RemoveAll vor Re-Lay, AddRange danach) bleibt unverändert; Early-Return-Fälle entfernen nichts.
- **Akzeptanzkriterien:** Verteilung (Test 3), Überlappungsfreiheit (Test 2), Mitternachts-Periodenende (Test 1), Regressionstest mit 20:15/240 min (alle drei) — abgedeckt.
- **Keine Schema-/UI-Änderung** → keine Migration, kein Backup-Eingriff, kein Usability-Review.

## Abweichungen/Anmerkungen

- Die Ketten-Grenze bezieht Bound-Starts **jeder** Priorität ein (konservativ); weiche Bounds dürfen danach nicht mehr von verlegten Titeln gekreuzt werden — bewusste Verschärfung gegenüber dem reinen Overshoot der Materialisierung, im Plan dokumentiert.
- Merged Airings über Occurrence-Grenzen erhalten bei `rest <= 0` weiterhin kein Fill — bewusst unverändert.

Ergebnis: **Plan vollständig** — Umsetzung kann beginnen.
