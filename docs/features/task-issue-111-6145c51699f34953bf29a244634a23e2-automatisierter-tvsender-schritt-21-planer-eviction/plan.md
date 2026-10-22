# Umsetzungsplan — N16 Planer-Eviction

| # | Schritt | Status |
|---|---------|--------|
| 1 | Reproduktionstest: partiell gelaufene Occurrence → Reconcile darf nicht verdrängen | erledigt |
| 2 | Fix A: `AiringReproducible` akzeptiert Occurrence-Suffixe (Ring-Rückwärtslauf) | erledigt |
| 3 | Fix B: `scheduled`-Laden ohne `Start < horizonEnd`-Kappung | erledigt |
| 4 | Tests: Idempotenz (Worker-Loop), Overlap-Verbot, Mutation-Check | erledigt |
| 5 | Volle Suite + Debug/Release-Build | offen |
| 6 | Doku `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md` | offen |

## Offene Punkte

Keine.
