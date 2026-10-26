# Logik — `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | public | Extend-only Lauf: materialisiert Occurrences, `InsertClipFill`, `FillGaps` |
| `ReconcileAsync` | public | Re-Bewertung: verdrängt invalide Ausstrahlungen, rematerialisiert, `InsertClipFill`, `FillGaps` |
| `GroupAirings` | private static | Gruppiert sortierte Slots zu Ausstrahlungen (gleiche `ProgramRuleId`, lückenlos) |
| `MaterializeOccurrence` / `MaterializeOccurrenceEvicting` | private | Wählt Titel (`SelectItems`) und schreibt Slots; nur `End > coverageStart` wird geschrieben |
| `SelectAiringItems` / `SelectAiringItemsInterleaved` | private static | Titel-Auswahl mit Bound-, `minStartRemain`- und `clipGrace`-Regeln |
| `BoundStarts` / `BoundaryAfter` | private static | Bound-Startliste aller item-tragenden Occurrences; erste Grenze nach einem Zeitpunkt |
| `TailLimit` | private static | Nächster belegter Start bzw. höherpriorer Bound-Start nach dem Ausstrahlungsende |
| `InsertClipFill` | private | Post-Pass: verteilt Clip-Slots in die Segmente jeder Ausstrahlung |
| `FillAiringSegments` | private | Verlegt eine Ausstrahlung neu: `periodEnd = airing[0].Start + SlotDuration` **(Defekt A)**; Segmente mit `share`-/`carry`-Budget; Titel ohne Grenzprüfung **(Defekt B)**; Tail-Clip mit `periodEnd`- statt `tailLimit`-Kappe **(Defekt C)** |
| `NextClipItem` | private | Round-Robin-Zug über die Clip-Gruppen-Ringe (`StrandState`) |
| `ClipFillContext` | private record | Kontext des Füll-Passes: Access, AiringsByMovie, CoverageStart, BoundStarts, HorizonEnd, Summary |
| `ReconcileWindow` | private class | Abgeleiteter Zustand des Reconcile-Laufs: `Occurrences`, `BoundStarts`, `Airings`, `FillerSlots`, `InvalidGroups` |
| `AiringReproducible` (clip-aware) | private static | Reproduzierbarkeitsprüfung; Clip-Slots werden per `ClipMediaItemIds` projiziert |
| `FillGaps` | private | Füllt freie Lücken mit Füller-Regelmaterial |

Defekte im Detail:

- **A:** `FillAiringSegments` Zeile ~2544: `var periodEnd = airing[0].Start + rule.SlotDuration!.Value;` — `airing[0].Start` ist der reale Start (nach `ShiftPastInvaders` verschoben bzw. bei Teil-Ausstrahlung mitten in der Periode). Das nominale Periodenende `Occurrence.StartUtc + SlotDuration` ist daraus nicht ableitbar — der Occurrence-Start fehlt im `ClipFillContext`.
- **B:** Titel werden in der Schleife `title.Start = pos; title.End = pos + duration` neu gesetzt — ohne Prüfung gegen `blocked` oder Bound-Starts. Übersteigt die verschobene Kette den freien Raum, entstehen Überlappungen.
- **C:** Tail-Prädikat `pos < periodEnd && pos + item.Duration <= (placed > TimeSpan.Zero ? tailLimit : periodEnd)` — der erste Tail-Clip darf bis `periodEnd` laufen, auch wenn `tailLimit` (belegter/reservierter Raum) früher liegt.
