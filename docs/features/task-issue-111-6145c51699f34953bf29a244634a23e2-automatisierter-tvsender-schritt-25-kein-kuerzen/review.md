# Plan-Review — N22

**Status: Vollständig umgesetzt**

| Planelement | Umsetzung |
|---|---|
| `MinStartRemainSeconds` + `EffectiveMinStartRemain` (Default 30 s) | `ProgramScheduleOptions` ✅ |
| `BoundaryAfter` → `(StartUtc, Hard)?`, erste Grenze beliebiger Priorität | ✅ |
| Weiche Grenze: Start vor Grenze−minRemain → Item läuft komplett (Überlauf) | `SelectAiringItems`/`...Interleaved` ✅ |
| Harte Grenze: Item muss vollständig vor Grenze enden, sonst defer (kein Konsum) | ✅ `consumed`-Pfad entfernt |
| Shift: Belegt ein Slot den Occurrence-Start, beginnt Materialisierung hinter seinem Ende | `ShiftPastInvaders` in `MaterializeOccurrence` + `MaterializeOccurrenceEvicting` ✅ |
| `DisplaceAiringSuffix`/Invasor-Trim entfernt (kein Mittendrin-Schnitt mehr) | ✅ |
| `AiringReproducible(-Interleaved)`: Anker `o <= realStart`, Simulation am realen Start + Invasor-Evidenz | ✅ |
| Tests: 3-min-Rest-Überlauf, <30 s → Füller, Stabilität, Invasor bleibt, harte Grenze, Options-Normalisierung | ✅ (4 neue + 4 angepasste) |
| Doku `tv-sender.md`, `RELEASE_NOTES.md` | ✅ |
| Loop-Terminierung bei degenerierten Dauern | Fortschritts-Abbruch in beiden Selektoren ✅ |

## Offene Aufgaben

Keine.
