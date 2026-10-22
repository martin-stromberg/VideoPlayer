# Bestandsaufnahme — N16 Planer-Eviction

## Relevante Komponenten

| Komponente | Rolle |
|---|---|
| `VideoWebPlayer/Services/ProgramScheduleService.cs` | `EnsureCoverageAsync` (extend-only, ~Z. 63), `ReconcileAsync` (~Z. 129), `BuildOccurrences` (~Z. 730), `MaterializeOccurrence` (~Z. 765), `MaterializeOccurrenceEvicting` (~Z. 512), `GroupAirings`/`AiringReproducible`/`FindItemIndex`/`RewindCursor`, `FillGaps` |
| `VideoWebPlayer/Services/ProgramScheduleWorker.cs` | Intervall-Loop: `ReconcileAsync` dann `EnsureCoverageAsync` |
| `VideoWebPlayer/Data/ProgramSlot.cs` / `ProgramSlotStatus.cs` | Scheduled=0 / Displaced=1 (Historie, nie gelöscht) |
| `VideoWebPlayer/Data/ProgramRuleCursor.cs` | Ring-Position je Regel (LastContentType+LastContentId) |
| `RuleState`/`PeekAt`/`Commit` (Service-intern) | Ringfortschritt während der Materialisierung |

## Befund (Ursachenanalyse)

**Bug A — partiell materialisierte Occurrence wird als invalid eingestuft.**
`MaterializeOccurrence`/`MaterializeOccurrenceEvicting` schreiben für eine bereits laufende
Occurrence (`OccurrenceLookback` = 24 h) nur die Slots, deren Ende nach `coverageStart` liegt;
das erste geschriebene Slot beginnt mitten in der Occurrence. `ReconcileAsync` verlangte aber
`occurrenceStarts.Contains(airing[0].Start)` — ein solches Tail scheitert immer → `firstInvalid`
→ **gesamte Regel-Zukunft** (`Displace`/Suffix) verdrängt → Re-Materialisierung schreibt dieselben
Positionen erneut → nächster Lauf verdrängt wieder. Oszillation im Worker-Takt (15 min) erzeugt
tausende überlappende Displaced-Zeilen (Live: 4520 vs. 38) und leert laufende Fenster („heute
16:00 leer").

**Bug B — Airings am Horizont-Rand werden abgeschnitten geladen.**
`ReconcileAsync` lud geplante Slots mit `s.Start < horizonEnd` — ragt ein Airing über den Horizont
hinaus, fehlt der Tail in `scheduled`/`airings`; die verkürzte Sequenz scheitert am Count-Vergleich
in `AiringReproducible` → verdrängt → Re-Materialisierung schreibt alle Slots inkl. der bereits
existierenden jenseitigen Slots erneut → **überlappende Scheduled-Duplikate** (Live-Cluster
„8 Items starten ~14:43–14:45" = wiederholte Neu-Materialisierung derselben Positionen).

## Fix

1. `AiringReproducible(airing, state, occurrenceStarts)`: Rückwärtslauf über den Item-Ring vom
   Kopf-Item — jede Iteration zählt ein weiteres „bereits gelaufenes" Kopf-Item; akzeptiert, wenn
   `airing[0].Start - Σskipped` exakt einem Occurrence-Start entspricht und der Rest der Sequenz
   (EpisodeCount/SlotDuration-Logik) reproduziert. `k=0` = bisheriges Verhalten.
2. `ReconcileAsync`: `scheduled`-Query ohne `s.Start < horizonEnd` — Airings werden vollständig
   geladen und korrekt validiert.

## Nicht geändert

- Eviction (`Evicts`: Priorität, dann kleinere Regel-Id) — korrekt; bleibt.
- Platzierung kumulativ pro Occurrence — war bereits korrekt; Cluster kam aus Neu-Materialisierung.
- Keine Migration/Massenbereinigung vorhandener Displaced-Zeilen (Historie; nicht ohne Not).
