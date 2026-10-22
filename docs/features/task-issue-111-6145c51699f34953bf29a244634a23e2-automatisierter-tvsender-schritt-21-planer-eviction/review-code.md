# Code-Review — N16 Planer-Eviction

**Status:** Keine Befunde

## Geprüfte Änderungen

1. `ReconcileAsync`: `scheduled`-Query ohne `s.Start < horizonEnd` — Airings/Füller jenseits des
   Horizonts werden vollständig geladen (Korrektheit der Sequenzprüfung; verhindert zugleich
   Duplikat-Materialisierung über noch-geplante Slots, da sie in `keptSlots`/`blocked` enthalten
   sind). Untere Schranke (`End > loadFrom`) unverändert.
2. `AiringReproducible(airing, state, occurrenceStarts)`: Ring-Rückwärtslauf vom Kopf-Item —
   akzeptiert Occurrence-Suffixe (k übersprungene Kopf-Items), deren kumulierte Dauer exakt auf
   einen Occurrence-Start der Regel trifft; Tail muss Count/MediaItemId/Grenzen reproduzieren.
   `k=0` = bisherige Semantik. Abbruch jenseits des ältesten Occurrence-Starts;
   0-Dauer-Guard gegen unbegrenzten Lauf.

## Selbstkritik / geprüfte Randfälle

- Falsch-Akzeptanz: `starts` ist pro Regel gruppiert; Positionsabgleich exakt (Ticks) — ein Tail,
   das nicht an einer Item-Grenze der Occurrence beginnt, matcht nicht.
- `EpisodeCount`/`SlotDuration`-Bounds identisch zur Materialisierung repliziert.
- `wanted=int.MaxValue` (SlotDuration): Schleifenabbruch nur über `earliestStart` — abgesichert
   durch striktes Wachsen von `skipped` (Dauer-Guard).
- Rückwärts-Wrap (`idx-k < 0`) via Modulo-Normalisierung korrekt.
- Füller-Slots jenseits des Horizonts bleiben per MediaItemId-Check gültig; `FillGaps`/`GapSeconds`
   behandeln jenseitige `blocked`-Intervalle korrekt (Kappung an `horizonEnd`).
- `DisplaceRuleFuture`/`RewindCursor` unverändert — Suffix-Semantik für echte Konflikte bleibt.
