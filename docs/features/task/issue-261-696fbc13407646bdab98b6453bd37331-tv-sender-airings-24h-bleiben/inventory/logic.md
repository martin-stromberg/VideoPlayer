# Logik

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2050 Zeilen)

Implementiert `IProgramScheduleService`: erzeugt und pflegt den rollierenden, lückenlosen
Sendeplan des einen Auslieferungs-Kanals aus den aktiven `ProgramRule`s. Zeilenangaben beziehen
sich auf Commit `cbdb0b8`.

### Zentrale Konstante (Bug-Stelle)

`private static readonly TimeSpan OccurrenceLookback = TimeSpan.FromHours(24);` (Z. 26) —
wird an genau zwei Stellen verwendet:

- `ReconcileAsync`, Z. 175: `loadFrom = coverageStart - OccurrenceLookback` — Ladefenster der
  geplanten Slots (`s.End > loadFrom`, Z. 177).
- `BuildOccurrences`, Z. 1119: `startUtc >= coverageStartUtc - OccurrenceLookback` — filtert die
  nominellen Occurrence-Starts. Gilt für `EnsureCoverageAsync` (Z. 113) und `ReconcileAsync`
  (Z. 188) gleichermaßen.

Zusätzlich begrenzt die Tagesschleife in `BuildOccurrences` (Z. 1096) den Suchraum: sie iteriert
nur Tage von `coverageStart`-Tag (lokal) **minus 1 Tag** bis `horizonEnd`-Tag — Occurrences
älter als ~2 Tage werden unabhängig vom `OccurrenceLookback`-Wert gar nicht erst erzeugt.

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync(DateTimeOffset, CancellationToken)` | public | Extend-only-Ausbau `[coverageStart, now + horizon]`; `coverageStart = max(now, letztes Scheduled-Slot-Ende)` (Z. 66–131). Ruft `BuildOccurrences` mit demselben 24-h-Lookback (Z. 113) — ein vergrößertes Fenster würde hier sehr alte Occurrences neu materialisieren |
| `ReconcileAsync(DateTimeOffset, CancellationToken)` | public | Neubewertung des Plans (Z. 134–356), siehe Ablauf unten |
| `GetScheduleAsync(long, DateTimeOffset, int, CancellationToken)` | public | Slots ab `fromUtc` inkl. Status (Z. 359) |
| `GetRuleDiagnosesAsync(string, long, CancellationToken)` | public | Pro-Regel-Diagnose, Admin-only (Z. 368) |
| `DeleteDisplacedSlotsAsync(string, long, CancellationToken)` | public | Löscht alle `Displaced`-Slots, Admin-only (Z. 404) |
| `ResetScheduleAsync(string, long, CancellationToken)` | public | Löscht alle Slots + Cursor, Admin-only (Z. 415) |

### Ablauf `ReconcileAsync` (Z. 134–356)

1. Lädt **alle** Regeln des Kanals (inkl. inaktiver; `activeRules` separat, Z. 137–143),
   Horizont (48 h Default), `coverageStart = utcNow` (Z. 145–146).
2. Baut `RuleState`s (Expansion + Cursor) für jede Regel — auch inaktive, damit deren verdrängte
   Slots einen Cursor-Rewind bekommen (Z. 152–168).
3. `loadFrom = coverageStart - OccurrenceLookback` (Z. 175); lädt `Scheduled`-Slots mit
   `End > loadFrom` — **kein** oberer Start-Bound, damit überschießende Airing-Tails komplett
   geladen werden (Kommentar Z. 170–174).
4. `invaderStarts`: `scheduled.GroupBy(End) → min(Start)` (Z. 184–186) — Verschiebe-Evidenz:
   ein Airring, das später als sein nominaler Occurrence-Start beginnt, ist nur plausibel, wenn
   ein belegter Slot genau an seinem realen Beginn endete und vor dem nominalen Start begann.
5. `occurrences = BuildOccurrences(activeRules ohne Filler, coverageStart, horizonEnd)` (Z. 188);
   `occurrenceStarts` je Regel (Z. 189–191); `boundStarts` (Z. 192).
6. `GroupAirings` gruppiert Nicht-Filler-Slots zu Airings (Z. 196); Filler-Slots werden einzeln
   geprüft (Z. 242–254).
7. Pro Regel: erstes invalides Airing verdrängt die gesamte Rest-Zukunft der Regel
   (Z. 212–240). „Invalid" = Regel weg/inaktiv, keine Occurrence-Starts, oder
   `AiringReproducible` liefert `false` (Z. 222–225). `Displace` verdrängt nur Slots mit
   `End > coverageStart` — vollständig gelaufene Slots bleiben Historie (Z. 202–208).
8. `RewindCursor` für verdrängte Regeln (Z. 259–263), dann Re-Materialisierung über
   `MaterializeOccurrenceEvicting` mit Evict-Logik (Z. 267–339), `FillGaps`, `PersistCursors`,
   `SaveChangesAsync` (Z. 341–346).

### Reproduzierbarkeitsprüfung (Bug-relevant)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GroupAirings` | private static (Z. 445) | Gruppiert sortierte Slots zu maximalen, lückenlos aneinander anschließenden Läufen derselben `ProgramRuleId` |
| `AiringReproducible` | private static (Z. 478) | Ein-Strang-Pfad. `earliestStart = occurrenceStarts.Min()` (Z. 495); Rückwärtslauf über Kopf-Items; **`break`, sobald `airingStart < earliestStart` (Z. 510–511)** — ein realer Beginn außerhalb des Occurrence-Fensters kann nie einen Treffer finden. Shift-Kandidaten brauchen `invaderStarts`-Evidenz (Z. 519–521) |
| `AiringReproducibleInterleaved` | private static (Z. 557) | Mehrstrang-Pfad; iteriert `occurrenceStarts.Where(o <= airing[0].Start)` (Z. 602) — ohne Treffer im Fenster sofort `false`. Simuliert Präfix-Picks und prüft `invaderStarts` am rekonstruierten `simStart` (Z. 660–663) |
| `FindItemIndex` | private static (Z. 697) | Lokalisiert einen Slot im expandierten Ring: `MediaItemId`, dann `MovieId`/`TVShowEpisodeId`-Fallback |
| `RewindCursor` | private static (Z. 712) | Spult Strang-Cursor auf den Vorgänger des frühesten verdrängten, noch expandierbaren Inhalts zurück |
| `MaterializeOccurrenceEvicting` | private (Z. 749) | Wie `MaterializeOccurrence`, gibt aber verdrängte Kept-Slots als Verlierer zurück; Slots, die über den Occurrence-Start hinausragen, sind legitime Tails (nie geschnitten) |
| `SelectAiringItems` | private static (Z. 1300) | Item-Auswahl des flachen Rings: `EpisodeCount` > `SlotDuration` > 1 Item; Bound-Regeln (Soft/Hard) via `minStartRemain` |
| `SelectAiringItemsInterleaved` | private static (Z. 1234) | Round-robin über Stränge mit `StrandStartOffset`; gleiche Bound-Semantik |
| `BoundStarts` | private static (Z. 1340) | Sortierte Occurrence-Starts aller Regeln mit expandierbarem Material |
| `BoundaryAfter` | private static (Z. 1354) | Erster Bound-Start strikt nach `afterUtc`; `Hard = Bound-Priorität > Regel-Priorität` |
| `ShiftPastInvaders` | private static (Z. 972) | Verschiebt einen Occurrence-Start hinter das Ende jedes ihn überdeckenden belegten Intervalls |
| `StrandStartOffset` | private static (Z. 967) | Start-Strang-Rotation bei `EpisodeCount < Strangzahl` (tagbasiert) |
| `StrandOrderKey` | internal static (Z. 939) | Deterministischer FNV-1a-Hash-Mix je (Regel, Strang) — reproduzierbar, nicht alphabetisch |
| `SelectStrandCursor` | internal static (Z. 957) | Gewinner-Cursor je Strang: höchste `UpdatedAt`, Tiebreak höchste `Id` |
| `NextIndexFor` | private static (Z. 1084) | Ring-Index hinter der Cursor-Position; 0 ohne Cursor oder nicht mehr gefundenem Inhalt |

### Occurrence-Erzeugung und Materialisierung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildOccurrences` | private (Z. 1093) | Erzeugt `Occurrence(Rule, StartUtc)` pro anwendbarem Tag je Regel. Tagesschleife `coverageStart.Tag-1 … horizonEnd.Tag` (lokal, Z. 1096–1102); UTC-Konversion über `TimeZoneInfo.Local` (Z. 1117–1118); Fensterfilter `coverageStart - OccurrenceLookback ≤ startUtc < horizonEnd` (Z. 1119) |
| `MaterializeOccurrence` | private (Z. 1127) | Schreibt Slots einer Occurrence: `ShiftPastInvaders` → `SelectItems` → nur Slots mit `End > coverageStart` werden persistiert (Z. 1149–1151), atomarer Konflikt-Drop (Z. 1157) |
| `SelectItems`/`CommitTaken` | private (Z. 1192/1213) | Auswahl ohne Cursor-Commit; Commit erst nach Konfliktfreiheit |
| `FillGaps`/`GapSeconds` | private (Z. 1362/1455) | Füllt Lücken aus Filler-Ringen (inkl. Trailer-Vorlauf) bis zum Horizont |
| `PersistCursors` | private (Z. 1473) | Schreibt eine Cursor-Zeile je Strang mit `LastPlaced` |
| `ExpandMaterialAsync` + `Expand*Async` | private (Z. 1506 ff.) | Material-Expansionen je `MaterialType` (Show, Movie, Collection, Genre→Stränge, Playlist, Trailer, MediaSourceClips) |
| `LoadMovieAiringsAsync`/`BuildMovieAirings(From|Map)` | private (Z. 1940/1953/1963) | Film-Ausstrahlungs-Starts für den Trailer-Vorlauf |

### Interne Typen

| Typ | Zeile | Zweck |
|-----|-------|-------|
| `SchedulableItem` (record) | 823 | Expandierter Inhalt: ContentType/Id, MediaItemId, MovieId?, TVShowEpisodeId?, Duration, StrandKey |
| `StrandState` | 840 | Ein Ring je Strang (`NextIndex`, `Commit`, `RewindTo`, `PeekAt`) |
| `RuleState` | 860 | Regel + Items + Stränge; `IsInterleaved` bei >1 Strang; Genre-Regeln bilden Stränge `"show:{id}"`/`"other"` (Z. 887–918) |
| `Occurrence` (record) | 924 | `(Rule, StartUtc)` |
| `Interval` (struct) | 926 | `Overlaps`-Prüfung |
| `SummaryCounters` | 990 | Lauf-Zähler → `ProgramScheduleRunSummary` / Diagnose |
| `AccessContext` | 1015 | Publikums-Freigabe-Snapshot |

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | protected override | Endlosschleife: Intervall aus `GetProgramScheduleIntervalAsync` (Default 15 min); bei `TvSenderEnabled` hinter `IBackgroundProcessingGate` **`ReconcileAsync`, dann `EnsureCoverageAsync`** (Z. 71–72); Aufwachen per `IProgramScheduleSignal` möglich |

## `ProgramSettingsService` (relevante Ausschnitte)

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetProgramScheduleHorizonAsync` (Z. 317) | Planungshorizont; **Default 48 h** bei ≤ 0 |
| `GetProgramScheduleIntervalAsync` | Worker-Intervall; Default 15 min |
| `GetHolidayFederalStateAsync` | Bundesland für `ProgramRuleRecurrence.Holiday` |
| `GetTvSenderEnabledAsync` | Feature-Flag des Senders |

## `ProgramScheduleOptions`

Datei: `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`

| Eigenschaft | Typ | Kurzbeschreibung |
|-------------|-----|------------------|
| `TrailerLeadDays` / `EffectiveTrailerLead` | `int` / `TimeSpan` | Trailer-Vorlauf-Fenster; Default 7 Tage, ≤ 0 → Default |
| `MinStartRemainSeconds` / `EffectiveMinStartRemain` | `int` / `TimeSpan` | Mindest-Restzeit vor einer Window-Boundary zum Starten eines Items; Default 30 s |

Wird als `IOptions<ProgramScheduleOptions>?` in den `ProgramScheduleService`-Konstruktor
injiziert (Z. 53, gelesen Z. 60–62); `null` → Defaults. Konfiguriert ist in
`appsettings.json` nur `"ProgramSchedule": { "TrailerLeadDays": 7 }` — für den Lookback
existiert **keine** Option.
