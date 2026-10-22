# Plan-Review — N16 Planer-Eviction

**Status:** Vollständig umgesetzt

| Anforderung | Umsetzung |
|---|---|
| Reproduktion als Test (Idempotenz) | `Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_WorkerLoop_*` — Mutation-Check: schlagen ohne Fix fehl |
| Mechanismus der Overlap-Cluster | Gefunden: Horizont-Kappung beim Laden + Mid-Occurrence-Head → Verdrängung + Neu-Materialisierung gleicher Positionen |
| Eviction nur bei echten Konflikten | `Evicts` unverändert (Priorität/Id); falsche Invalidierung entfernt |
| Platzierung kumulativ | War bereits kumulativ; Cluster entstand durch Neu-Materialisierung, nicht durch Platzierung |
| coverageStart/Horizont-Leerstelle | Erklärt: laufendes Fenster wurde je Lauf verdrängt; `EnsureCoverage` füllt Mid-Plan-Löcher nicht (extend-only ab lastEnd) |
| Keine Migration/Bereinigung | Nicht nötig — Displaced bleiben Historie |
| Tests | +3 Tests (Reproduktion, Stabilität, Worker-Loop inkl. Overlap-Verbot) |
