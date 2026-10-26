# Umsetzungsplan: Platzhalter-Clips innerhalb der Sendeperiode verteilen (#291)

## Ziel

`FillAiringSegments` im `ProgramScheduleService` verteilt Platzhalter-Clips nur in den freien Raum innerhalb der nominalen Periode (Occurrence-Start + `SlotDuration`), hält die Kette überlappungsfrei gegenüber belegtem und reserviertem Raum und plant keine Clip-Blöcke nach dem Periodenende. Der #277-Überhang eines begonnenen Clips bleibt.

## Änderungen — `VideoWebPlayer/Services/ProgramScheduleService.cs`

### 1. `ClipFillContext`: Occurrence-Starts je Regel ergänzen

Neues Feld `IReadOnlyDictionary<long, List<DateTime>> OccurrenceStarts` (Regel-Id → sortierte nominale Occurrence-Starts inkl. Lookback).

- `EnsureCoverageAsync`: aus `occurrences` aufbauen (`GroupBy(o => o.Rule.Id)`), beim `with`-Clone an der bestehenden Stelle (Zeile ~130) setzen. Da `clipContext` vor `occurrences` erzeugt wird (Zeile 99), bleibt die Reihenfolge: leer initialisieren, später per `with` setzen.
- `ReconcileAsync`: aus `window.Occurrences` (Zeile ~362, `clipContext with { … }`).

### 2. `FillAiringSegments`: nominales Periodenende

```
var periodStart = letzter Occurrence-Start der Regel <= airing[0].Start
var periodEnd   = periodStart + rule.SlotDuration
```

- Kein Occurrence-Start ≤ Kopf gefunden (inkonsistent) → Ausstrahlung unverändert übernehmen (kein Fill) — defensiv.
- `rest = periodEnd - airing[^1].End` wie bisher; `rest <= 0` → unverändert.

### 3. Ketten-Grenze (freier Raum)

Neue private Hilfsmethode `ChainLimit(lastEnd, blocked, boundStarts, horizonEnd)`:

```
limit = horizonEnd
foreach o in blocked:        o.Start >= lastEnd && o.Start < limit → limit = o.Start
foreach b in boundStarts:    b.StartUtc >= lastEnd && b.StartUtc < limit → limit = b.StartUtc
return limit
```

— wie `TailLimit`, aber **ohne** Prioritätsfilter (jeder Bound-Start gehört einer anderen Occurrence; die Kette darf nicht hineinreichen).

Verteiltes Budget: `fill = min(rest, chainEnd - lastEnd)`; `fill <= 0` → unverändert zurückgeben. `share = fill / (n+1)`, `carry` wie bisher. Damit endet die neu verlegte Kette (Titel + Clips) nie hinter `chainEnd` → **keine Überlappung** und kein Überschreiten reservierter Fenster.

### 4. Clip-Prädikate

- Non-tail: `placed + item.Duration <= budget && pos + item.Duration <= tailLimit` (unverändert; `pos < chainEnd` ist durch `fill`-Kappe impliziert).
- Tail: `pos < periodEnd && pos < chainEnd && pos + item.Duration <= tailLimit` — uniform: ein Clip, der vor Periodenende und vor der Ketten-Grenze beginnt, darf bis `tailLimit` überhängen (begonnener-Clip-Überhang), kann aber nie in Belegtes ragen. Behebt Defekt C (erster Tail-Clip ignorierte `tailLimit`).

`tailLimit` (mit Prioritätsfilter, nur höherpriore Bounds + Belegtes + Horizont) bleibt die End-Kappe; `chainEnd` (alle Bounds + Belegtes) die Start-Grenze.

### 5. Kommentare anpassen

Methodenkommentare von `InsertClipFill`/`FillAiringSegments`/`TailLimit` auf das korrigierte Periodenende- und Grenzmodell aktualisieren (knapper Stil wie bestehend).

## Tests — `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipFillPeriodBounds.cs` (neu)

Bereits als rote Reproduktion angelegt:

1. `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd` — Lauf mitten in der Periode; kein Clip-Start ≥ Periodenende, keine Überlappung.
2. `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap` — höherpriore Regel mitten in der Periode (00:00–00:30); überlappungsfrei, Clips < Periodenende.
3. `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod` — 20:15/240 min, 4 Filme, 220 × 14 s Clips: Verteilung über Segmente, lückenlose Kette, Grenzen.

## Verifikation

- `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei
- `dotnet test VideoWebPlayer.Tests` volle Suite grün
- Keine Schema-/Datenmodelländerung → keine EF-Migration, kein Backup-Schema-Eingriff
- Keine UI-Änderung → Usability-Review nicht anwendbar

## Offene Punkte

Keine.

## Risiken / Entscheidungen

- Merge-Ausstrahlungen über mehrere Occurrences (lückenlos verkettet) erhalten weiterhin kein Fill, wenn `rest <= 0` — unverändertes Verhalten.
- Leere reservierte Bound-Fenster werden vom Füllen der Kette ausgenommen (konservativ); `FillGaps` kann sie weiter mit Füller-Material belegen.
