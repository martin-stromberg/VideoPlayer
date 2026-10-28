# `ReconcileAsync`-Abgleich und Planerzeugung — Bestandsaufnahme (Issue #290)

Alle Angaben beziehen sich auf `VideoWebPlayer/Services/ProgramScheduleService.cs` (3016 Zeilen, Klasse `ProgramScheduleService`, `sealed`).

## Öffentliche Einstiegspunkte

| Methode | Sichtbarkeit | Zeilen | Kurzbeschreibung |
|---------|-------------|--------|------------------|
| `EnsureCoverageAsync` | public | 75–155 | Extend-only: `coverageStart = max(now, letztes Scheduled-End)`; füllt `[coverageStart, now + Horizon]`; bestehende Slots werden nie geändert |
| `ReconcileAsync` | public | 158–397 | Neubewertung `[now, now + Horizon]`: validiert alle geladenen `Scheduled`-Slots gegen die aktuelle Regel-/Medienlage, markiert Ungültige `Displaced` und materialisiert Ersatz |
| `GetScheduleAsync` | public | 400–406 | Slots ab `fromUtc` aufsteigend — `Scheduled` **und** `Displaced` gemeinsam |
| `GetRuleDiagnosesAsync` | public | 409–466 | Pro-Regel-Planbarkeitsdiagnose (Admin), expandiert wie ein Planlauf, schreibt nichts |
| `DeleteDisplacedSlotsAsync` | public | 469–477 | Löscht alle `Displaced`-Zeilen des Senders (`ExecuteDeleteAsync`, Admin) |
| `ResetScheduleAsync` | public | 480–495 | Löscht alle Slots + `ProgramRuleCursors` des Senders (Admin) |
| `RequireAdminAsync` | private | 497–504 | Admin-Erzwingung für die drei Verwaltungsmethoden |

Konstruktor-Parameter (Z. 55–72): `ApplicationDbContext`, `IProgramRuleService`, `ProgramSettingsService`, `ILogger`, optional `IOptions<MediaProbeOptions>` (`EffectiveMaxAttempts` für die Diagnose) und `IOptions<ProgramScheduleOptions>` (`EffectiveTrailerLead`, `EffectiveMinStartRemain`, `EffectiveMaxOccurrenceLookback`).

## Ablauf `ReconcileAsync` (Z. 158–397)

### 1. Eingaben laden (Z. 160–201)

- `GetOrCreateChannelAsync`; **alle** Regeln des Senders inkl. `Genres`, `MediaSourceFilters`, `ClipGroups.ClipGroup` geladen — auch inaktive (deren verdrängte Slots brauchen den Cursor-Rücklauf, Z. 179–181). `activeRules` = `IsActive`-Teilmenge.
- `horizonEnd = utcNow + GetProgramScheduleHorizonAsync`; `coverageStart = utcNow`.
- `LoadAccessContextAsync` (Z. 1511–1564): `AllContent`-Scope oder Schnitt über alle Nicht-Admin-Benutzer (Quellfreigaben + Einzelfreigaben).
- `LoadAgeRuleAsync` (Z. 1571–1574): `ProgramScheduleAgeWindowRule` aus Fensterschema + Unbekannte-Freigabe-Modus.
- Cursor aller Regeln (Z. 183–187), `airingsByMovie` aus persistierten `Scheduled`-Film-Slots > `coverageStart` via `LoadMovieAiringsAsync` (Z. 189; Trailer-Items zählen nicht, `BuildMovieAiringsMap` Z. 2544–2557).
- Je Regel ein `RuleState`: `ExpandMaterialAsync(rule, access, summary, airingsByMovie, coverageStart, ct)` (Z. 194) — mit `coverageStart`, nicht dem `DateTime.UtcNow`-Fallback — plus `BuildClipGroupStates` (Trailer-Gruppe expandiert hier sofort, da `airingsByMovie` bekannt).

### 2. Slot-Fenster laden und validieren: `ReconcileWindow` (Z. 203–245, Klasse Z. 1024–1108)

- `LoadScheduledAsync(from)` (Z. 211–215): `Scheduled`-Slots mit `End > from`, nach `Start` geordnet — **kein oberer `Start`-Bound**: eine über den Horizont reichende Ausstrahlung muss vollständig geladen werden, sonst scheitert ihr Tail an der Sequenzprüfung und wird verdrängt (Kommentar Z. 203–210 — dort bereits als Verdrängungs-Schleifenquelle benannt).
- Basis-Fenster `coverageStart - OccurrenceLookback` (24 h, Z. 26/227). Heuristik: beginnt ein Slot `<= loadFrom`, wird einmalig auf `coverageStart - _maxOccurrenceLookback` verbreitert und neu geladen (Z. 228–234); andernfalls greift bei `window.HasRescuableInvalid` die validierungsgetriebene Verbreiterung (Z. 241–244).
- `ReconcileWindow` (primary-ctor-Klasse, Z. 1024–1108):
  - `Reload(loadFrom, scheduled)` (Z. 1049–1054): setzt `Scheduled` und ruft atomar `Rebuild` + `Validate` — kein Zustand eines engeren Fensters wiederverwendbar.
  - `Rebuild` (Z. 1056–1077): `_invaderStarts` (End → frühestes Start je belegtem Slot — Verschiebungs-Evidenz), `Occurrences` über `BuildOccurrences` mit Lookback `coverageStart - loadFrom` aus **aktiven** Nicht-Füller-Regeln, `_occurrenceStarts` je Regel, `BoundStarts`, `Airings` = `GroupAirings` der Nicht-Füller-Slots (maximale lückenlose Ketten gleicher `ProgramRuleId`, Z. 510–526), `FillerSlots` separat.
  - `Validate` (Z. 1079–1107): je Regel-Gruppe die geordneten Ausstrahlungen bis zur ersten Invaliden; `firstInvalid` = erste Ausstrahlung, die (a) noch Sendezeit hat (`End > coverageStart`) und (b) fehlschlägt: Regel fehlt/inaktiv, keine Occurrence-Starts oder `AiringReproducible` `false`. Ergebnis `InvalidGroups` (Suffix-Verdrängung ab `firstInvalid`), `HasRescuableInvalid` = invalid bei **aktiver** Regel (steuert die Fensterverbreiterung).

### 3. Verdrängung (Z. 246–283)

- `Displace(slot)` (Z. 249–255): setzt `Status = Displaced`, nur wenn `End > coverageStart` — ausgestrahlte Slots sind Historie.
- Suffix-Verdrängung je `InvalidGroups`-Eintrag (Z. 260–269): ab `firstInvalid` alle Ausstrahlungen der Regel (Sequenzkontext ist cursorbasiert) + `displacedRuleIds`.
- Füller-Slots einzeln (Z. 271–283): invalid, wenn Regel fehlt/inaktiv oder `slot.MediaItemId` nicht mehr in `state.Items` (exakte MediaItem-Mitgliedschaft, kein Content-Fallback).

### 4. Cursor-Rücklauf: `RewindCursor`/`RewindRing` (Z. 285–292, 967–1016)

- `RewindCursor` trennt Clip-Slots (`ClipMediaItemIds`) von Titel-Slots; `RewindRing` geht die verdrängten Slots aufsteigend durch und setzt bei `ring.NextIndex > idx` den persistierten Cursor auf den Ring-Vorgänger von `idx` und `ring.RewindTo(idx)` — nur der früheste auflösbare verdrängte Inhalt entscheidet (`break` nach erstem Treffer).

### 5. Re-Materialisierung mit Verdrängungswettbewerb (Z. 294–371)

- `keptSlots` = verbliebene `Scheduled` mit `End > coverageStart`; `blocked` = deren Intervalle.
- `DisplaceRuleFuture(ruleId, fromStartUtc)` (Z. 300–325): verdrängt alle noch-`Scheduled`-Slots aller Ausstrahlungen der Regel ab `fromStartUtc` inkl. Füller-Slots + `RewindCursor`.
- `Evicts(winner, kept)` (Z. 327–336): Füller verliert immer; sonst `Priority` höher, Gleichstand kleinere `Id`.
- Materialisierungsschleife (Z. 341–371): nur Occurrences `>= coverageStart - OccurrenceLookback` (Basisfenster — das verbreiterte Fenster dient nur als Beweisanker; Kommentar Z. 338–340 nennt das Oszillationsrisiko), geordnet nach `Priority` desc, `Rule.Id`, `StartUtc`. Je Occurrence `MaterializeOccurrenceEvicting`; verlierende `keptSlots` → deren Ausstrahlung wird per `DisplaceRuleFuture` suffixweise entfernt, ungruppierte Verlierer (Füller) einzeln.
- `MaterializeOccurrenceEvicting` (Z. 1116–1189): `ShiftPastInvaders(occurrence.StartUtc, blocked)` → `SelectItems`; nur Ranges mit `End > coverageStart`; Konfliktprüfung — `keptSlots` mit `Start < occurrence.StartUtc` sind legitime überschießende Tails (verlieren immer, werden als `conflicts` gemeldet), Kopf-an-Kopf-Konkurrenten gehen durch `evicts`; bei nicht verdrängbarem Konflikt `DroppedConflicts++` und keine Materialisierung (kein Cursor-Verbrauch). Schreibt `ProgramSlot`s mit `Status = Scheduled` (Default) in `newSlots` und ruft `CommitTaken`.

### 6. Clip-Füllung, Lücken, Persistenz (Z. 373–397)

- `BuildMovieAiringsFromSlotsAsync(keptSlots + newSlots)` → `leadAirings` (Z. 373).
- `InsertClipFill` (Z. 374–382) auf den **neuen** Slots; `FillGaps` über `activeRules` (Z. 383).
- `PersistCursors` (Z. 385, Impl. Z. 2074–2106) + `AddRange(newSlots)` + `SaveChangesAsync` (Z. 386–387) — ein Commit am Ende.
- Rückgabe `ProgramScheduleReconcileSummary` (Z. 389–392) + Log `[ProgramSchedule] Re-Bewertung: {Displaced} Sendeplaetze verdrängt, {Created} erzeugt ({Filler} Fueller).` (Z. 393–395).

## Reproduzierbarkeitsprüfung (`AiringReproducible`)

### Clip-bewusster Einstieg (Z. 566–607)

`ClipMediaItemIds` (Z. 557–564) sammelt die MediaItem-Ids aller Clip-Gruppen-Ringe der Regel. Mit Clips: Kettengliederung muss lückenlos sein (`slot.Start == prev.End`, sonst `false`); Clip-Slots werden auf Mitgliedschaft in der aktuellen Clip-Expansion geprüft und aus der Zeitachse herausgerechnet (projizierte `ProgramSlot`-Kopien ohne `Id`); bleiben keine Titel-Slots, `false`. Danach Dispatch `AiringReproducible(airing, 0, DateTime.MinValue, ReproContext)` (Z. 615–627) auf `FlatAiringTailReproducible` oder `InterleavedAiringTailReproducible`, Memo über `(Offset, AfterStart)`.

### `FlatAiringTailReproducible` (Z. 629–742)

- `FindItemIndex` (Z. 952–962) verortet den Kopf-Slot: zuerst `MediaItemId`, dann Content-Fallback `MovieId`/`TVShowEpisodeId` (mildert Repräsentantenwechsel ab — Positions- und Dauerprüfung laufen aber gegen das **neue** Item).
- Rückwärts-Hypothesen `k` = „genommene, aber nie geschriebene Kopf-Items" (teilweise ausgestrahlte Occurrence): laufen den Ring zurück, überspringen bei ungeordneten Ringen fenster-gesperrte Vorgängerpositionen (`ProgramScheduleAgeWindow.IsAllowedAt` am rückgerechneten Zeitpunkt) und rekonstruieren `airingStart = head.Start - ΣVorgängerdauern` (Z. 653–697).
- Je Kandidaten-Occurrence `occurrenceStart ≤ airingStart` (strikt `> afterStart`): Verschiebung `occurrenceStart < airingStart` braucht Invader-Evidenz `invaderStarts[airingStart] < occurrenceStart` (Z. 700–707); `SelectAiringItems`-Neusimulation ab `airingStart` mit `BoundaryAfter(boundStarts, occurrenceStart, …)`; Kopf-Pick muss auf Ringposition `idx` liegen; alle `consumed` Slots müssen `MediaItemId`, `Start`, `End` exakt treffen (Z. 724–736); Rest rekursiv.

### `InterleavedAiringTailReproducible` (Z. 754–948)

- Lokalisiert jeden Slot in seinem Strang; geordnete Stränge verlangen strikte Ring-Contiguity, ungeordnete verlangen, dass alle übersprungenen Zwischenpositionen am Start des Folgepicks gesperrt waren (Z. 769–819).
- Hypothesen: Präfixlänge `p` der Picks vor dem Kopf; `startOffset` aus `StrandStartOffset(wanted, m, Tag)` — **beide** Tage `realDay` und `realDay−1` werden probiert, weil eine Invader-Verschiebung den realen Start über die UTC-Tagesgrenze schieben kann (Z. 824–833). Stränge ohne Beitrag können keine Präfix-Picks geliefert haben.
- Strang-Köpfe werden rückwärts rekonstruiert (genommene Picks treiben `simStart`, gesperrte Positionen nur den Scan-Kopf, Z. 859–891); Konsistenzbedingung `StrandStartOffset(wanted, m, simStart) == startOffset` (Z. 905–908); Invader-Evidenz analog (Z. 910–913); `SelectAiringItemsInterleaved`-Simulation und Slot-für-Slot-Abgleich mit Rekursion (Z. 915–943).

### `StrandStartOffset` / Zeit-/Belegungsfunktionen

| Funktion | Zeilen | Kurzbeschreibung |
|----------|--------|------------------|
| `StrandStartOffset` | 1439–1442 | Start-Strang interleaver Ausstrahlungen: tagesbasierte Rotation `(startUtc.Date - UnixEpoch).Days mod m` — nur wenn `wanted < strandCount`, sonst 0 |
| `ShiftPastInvaders` | 1444–1460 | Schiebt einen Nominalstart iterativ hinter jedes ihn belegende Intervall (überschießende Tails laufen zu Ende) |
| `BoundaryAfter` | 1934–1940 | Erster Bound-Start `> afterUtc` mit `Hard = priority > eigene` |
| `BoundStarts` | 1920–1926 | Sortierte Occurrence-Starts aller Regeln mit nichtleerer Expansion |
| `SelectItems` | 1713–1730 | Dispatch flach/interleaved (`SelectAiringItems` Z. 1858–1915, `SelectAiringItemsInterleaved` Z. 1758–1848): Fenster-Eignung je Slot-Startzeit (`IsAllowedAt`), Bound-Prüfung (`minStartRemain`, harte Grenzen), `clipGrace`, EpisodeCount/SlotDuration-Ziel |
| `CommitTaken` | 1734–1748 | Commit nach Konfliktfreiheit: `CommitScan` je Strang (`NextIndex` hinter letzter Scanposition, `LastPlaced` = letzter genommener Pick) |
| `NextIndexFor` | 1578–1585 | Cursor → Ring-Startindex; ohne Treffer 0 |
| `MixUnorderedRing` / `ItemOrderKey` / `StrandOrderKey` / `Fnv1aHash` | 1382–1423 | Deterministische FNV-1a-Mischung ungeordneter Ringe (Movie/MediaItem) — reine Funktion der Expansion, laufstabil |
| `GroupAirings` | 510–526 | Maximale lückenlose Ketten gleicher `ProgramRuleId` |

## Materialexpansion (Eingaben, gegen die `Validate` prüft)

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `ExpandMaterialAsync` (2 Overloads) | 2110–2137 | Dispatch je `MaterialType`; der Overload **ohne** `coverageStart` fällt auf `DateTime.UtcNow` (Z. 2110–2111) — genutzt von `EnsureCoverageAsync` (Z. 114) und `GetRuleDiagnosesAsync` (Z. 443); `ReconcileAsync` nutzt Z. 194 den Overload mit `coverageStart`. Leere Expansion → `OrphanedRules++` + Log |
| `MovieMediaItemsAsync` | 2144–2155 | Repräsentant je Film: `OrderBy(Duration is null ? 1 : 0).ThenBy(MediaItemId)` — bevorzugt gemessene Items |
| `EpisodeMediaItemsAsync` | 2157–2168 | Dasselbe je Episode |
| `TryMovieItem` / `ToItem` | 2190–2205 / 2238–2253 | `Duration == null` → `CountNoDuration`; `access.Allows` → `SkippedNoAccess` |
| `ExpandShowAsync` | 2207–2233 | Episoden nach Staffel/Nummer |
| `ExpandMovieAsync` / `ExpandMovieCollectionAsync` | 2255–2267 / 2269–2289 | Filme nach `ReleaseDate ?? PremieredAt`, `Id` |
| `ExpandGenreAsync` | 2300–2401 | `GenreCanonicalResolver.ResolveCanonicalNames(allRows, DateTime.UtcNow)` (Z. 2327) — saisonale/versteckte Zeilen tagesabhängig; Quell-Filter; Filme → Strang `other`, Episoden → `show:{id}` |
| `ExpandTrailersAsync` | 2428–2471 | Trailer-Links der im Plan ausgestrahlten Filme (Start > `coverageStart`), geordnet nach nächstem Filmstart |
| `ExpandMediaSourceClipsAsync` | 2503–2519 | Unklassifizierte Video-Items einer Quelle |
| `ExpandPlaylistAsync` | 2559–2633 | Playlist-Einträge (manuell/release-sortiert); `ContentType` → `PlaylistEntry` |
| `ExpandClipGroupItemsAsync` / `FilterClipCandidatesAsync` | 2719–2735 / 2740–2779 | Clip-Ring je Gruppe: Video, unklassifiziert, `Duration != null`, freigegeben |
| `LoadMovieAiringsAsync` / `BuildMovieAiringsFromSlotsAsync` / `BuildMovieAiringsMap` | 2521–2557 | Filmstarts > `afterUtc` aus `Scheduled`-Slots (Trailer-Items ausgeschlossen) |
| `BuildClipGroupStates` / `ExpandClipGroupStatesAsync` / `ExpandTrailerStatesAsync` | 2675–2697 / 2702–2713 / 2405–2422 | Clip-Gruppen-Zustände; Trailer-Gruppe verzögert |

## `EnsureCoverageAsync` (Z. 75–155) — Referenzablauf

`coverageStart = max(now, letztes Scheduled-End)` (Z. 87–91); identische Zustandsbildung (Regeln nur `IsActive`); `MaterializeOccurrence` ohne Eviction (atomarer Konfliktverwurf, Z. 1621–1678); Trailer/Clip-Gruppen nachexpandiert; `InsertClipFill`, `FillGaps` (Z. 1942–2054), `PersistCursors`, ein `SaveChangesAsync` (Z. 148). Rückgabe `ProgramScheduleRunSummary`; Log `[ProgramSchedule] {Created} Sendeplaetze erzeugt …`.

## Interne Typen (Auswahl)

- `SchedulableItem` (Z. 1193–1201): `(ContentType, ContentId, MediaItemId, MovieId, TVShowEpisodeId, Duration, StrandKey, AgeRatingMinAge)` — die Expansionseinheit.
- `StrandState` (Z. 1211–1248): Ringzustand je Strang (`NextIndex`, `LastPlaced`, `PreservesOrder` = nur `TVShowEpisode`/`PlaylistEntry`, `PeekAt`, `Commit`, `CommitScan`, `RewindTo`).
- `ClipGroupState` (Z. 1253–1276): Gruppen-Ring mit `StrandKey = "clipgroup:{id}"`, `IsTrailer` für `TrailerGroupId`.
- `RuleState` (Z. 1278–1365): `SetItems` baut Stränge — Genre-Regeln mehrsträngig (`show:{id}`/`other`, Hash-Mix via `StrandOrderKey`, `MixUnorderedRing` je Strang), sonst Flachpfad ein Strang (`Key == null`); `ClipGrace` nur bei Gruppen + `SlotDuration`.
- `ReproContext` (Z. 531–538): invarianter Kontext + Memo der Reproduzierbarkeitsprüfung.
- `Occurrence` (Z. 1367), `Interval` (Z. 1369–1372), `SummaryCounters` (Z. 1462–1483), `AccessContext` (Z. 1488–1509), `ClipFillContext` (Z. 2642–2659, inkl. `RoundRobin`-Laufzustand), `ClipCandidate` (Z. 2664–2666), `MediaItemRef` (Z. 2142), `SelectionResult`/`StrandPick`/`FlatSelection`/`InterleavedSelection` (Z. 1693–1711).
