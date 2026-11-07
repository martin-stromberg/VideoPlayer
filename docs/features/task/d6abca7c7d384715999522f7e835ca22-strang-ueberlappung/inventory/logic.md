# Bestandsaufnahme — Logik: `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (~3160 Zeilen). Es werden nur die für die Anforderung relevanten Methoden dokumentiert.

## `SelectAiringItemsInterleaved` (private static, ~Zeile 1810)

| Element | Stand |
|---------|-------|
| `wanted` | `EpisodeCount` falls > 0; `int.MaxValue` bei `SlotDuration`; sonst 1 |
| `target` | `SlotDuration` bei Nicht-`EpisodeCount`-Regeln (Zeit-Ziel des Fensters) |
| `Run(startOffset)` | Lokale Funktion → `RunInterleavedLaps(...)` — ein Auswahllauf ab Startstrang |
| `wanted < m` (EpisodeCount) | Direkt `Run(StrandStartOffset(m, airingStartUtc))` — Tagesrotation, kein Probelauf |
| `wanted >= m` (SlotDuration) | Probelauf `Run(0)`; wenn `rotated != 0` und bediente Stränge < nicht-leere Stränge → `Run(rotated)`, sonst Probelauf |
| `rotated` | **Bisher:** `StrandStartOffset(m, airingStartUtc)` = Tageszähler mod m (+1/Tag) — die Fehlerquelle |

Wird aufgerufen von `SelectItems` (Zeile 1772). Reine Funktion — Stränge werden nicht mutiert; `CommitTaken` übernimmt nur das Ergebnis des gewinnenden Laufs.

## `RunInterleavedLaps` (private static, ~Zeile 1851)

Ein Auswahllauf: Runden über `(startOffset + i) % m`, je Strang max. 1 Pick pro Runde. Abbruch bei `wanted` erreicht, Zeitziel erreicht, Clip-Grace (`clipGrace`), Bound-Verletzung oder `noProgressLaps > maxLap`. Ergebnis `InterleavedSelection(Picks, Scanned, BlockedByAgeWindow)` — `Picks` in zeitlicher Reihenfolge (letzter Pick = letzter belegter Slot).

## `StrandStartOffset` (private static, ~Zeile 1422)

`(int)(((startUtc.Date - UnixEpoch).Days % strandCount) + strandCount) % strandCount` — Tagesrotation +1/Tag. Bleibt für den `EpisodeCount`-Pfad vorgesehen.

## `SelectItems` (private, ~Zeile 1760)

| Parameter | Stand |
|-----------|-------|
| `state` | `RuleState` (Items, Strands, IsInterleaved, ClipGrace) |
| `airingStartUtc` | realer (ggf. verschobener) Ausstrahlungsbeginn |
| `bound` | nächste Fenstergrenze (optional hart) |

Flat-Pfad: `SelectAiringItems`. Interleaved-Pfad: `SelectAiringItemsInterleaved` → `SelectionResult(Taken, Picks, Scanned, BlockedByAgeWindow)`.

## `CommitTaken` (private static, ~Zeile 1781)

Schreibt `Scanned`/`LastPlaced` je Strang fort (`CommitScan`); Flat-Pfad analog. Wird erst nach bestandener Konfliktprüfung aufgerufen.

## `MaterializeOccurrence` (private, ~Zeile 1644)

1. `state = states[rule.Id]`; leere Items → Abbruch.
2. `airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked, outranked)` — hinter überschießende Vorgänger-Enden/Take-over-Ketten.
3. `WindowLostToTakeover` → Abbruch ohne Slots/Cursor.
4. `SelectItems` → bei 0 Takes Abbruch.
5. Ranges nur für Takes mit `End > coverageStart`; `ranges.Count == 0` (voll vergangen) → Abbruch.
6. Atomare Konfliktprüfung gegen `blocked`/`newSlots` → `DroppedConflicts++`, Abbruch.
7. Slots an `newSlots`, `blocked` erweitern, `CommitTaken`, `SlotsCreated`.

Belegungsquellen: `persistedOccupancy` — geladene `ProgramSlots` (`Scheduled`, `End > coverageStart - OccurrenceLookback`, `Start < horizonEnd`) als `(Interval Slot, long? RuleId)`-Tupel — **Inhalts-Ids (`MovieId`, `TVShowEpisodeId`, `MediaItemId`, `IsClipFill`) werden derzeit nicht projiziert**; `newSlots` — volle `ProgramSlot`-Entities dieses Laufs. Aufruf aus `EnsureCoverageAsync` (Zeile 158), Occurrences sortiert nach Priorität ↓, RuleId, StartUtc (je Regel chronologisch).

## `MaterializeOccurrenceEvicting` (private, ~Zeile 1071)

Gleiche Struktur wie `MaterializeOccurrence`, liefert Konflikt-Kept-Slots als `losers` zurück statt abzubrechen. Belegungsquellen: `keptSlots` (`List<ProgramSlot>`, Zeile 355: `Scheduled`, `End > coverageStart`) und `newSlots`. Aufruf aus `ReconcileAsync` (Zeile 404), gleiche Occurrence-Sortierung. `CommitTaken` identisch.

## `FindItemIndex` (private static, ~Zeile 853)

Löst `ProgramSlot` → Index in einer `SchedulableItem`-Liste auf: `MediaItemId` (eindeutig) → `MovieId` → `TVShowEpisodeId`. Wird von `SlotStillValid` (Zeile 712: Schleife über alle Stränge) und `RewindCursor`/`RewindRing` genutzt — etablierte Slot→Strang-Auflösung.

## `SlotStillValid` (private static, ~Zeile 681)

Clip-Klassifikation zuerst (`IsClipFill` oder `clipIds.Contains(MediaItemId)` → Clip-Pfad), dann Strang-Suche per `FindItemIndex`, dann Laufzeit- und Altersfenster-Check; `StrandSequenceBroken` (Ring-Kontiguität geordneter Stränge) unverändert relevant.

## `ClipMediaItemIds` (private static, ~Zeile 650)

`HashSet<long>` aller MediaItem-Ids der Clip-Gruppen-Ringe des `RuleState` — Clip-First-Klassifikation.

## `StrandState` / `RuleState` (private, ~Zeile 1190 / 1258)

- `StrandState(Key, Items, NextIndex)`: `CommitScan`, `RewindTo`, `PreservesOrder` (geordnete Stränge: alle `Sequenced` oder `TVShowEpisode`/`PlaylistEntry`).
- `RuleState(Rule, Items, Strands, ClipGroups, IsInterleaved, ClipGrace)`; `SetItems` bildet Stränge in `StrandOrderKey`-Hashreihenfolge (`StrandOrderKey(ruleId, key)` = FNV-1a über `"{ruleId}:{key}"`) — deterministisch, prozessstabil.

## `PersistCursors` (~Zeile 2184)

Persistiert `LastPlaced` je Strang in `ProgramRuleCursors` (eine Zeile je `StrandKey`, Flat: `null`). Kein Rotations-Zeiger vorhanden.

## `EnsureCoverageAsync` (~Zeile 89) / `ReconcileAsync` (~Zeile 180)

`EnsureCoverageAsync`: `coverageStart = max(lastEnd, now)` — persistierte `Scheduled`-Slots enden alle ≤ `coverageStart`; `persistedOccupancy` geladen; Occurrences bis `horizonEnd` materialisiert; `InsertClipFill` (Zeile 165) legt danach `IsClipFill`-Slots zwischen/um die Titel-Picks (kann Titel-Slots zeitlich nach hinten legen, ändert nicht deren Reihenfolge).
`ReconcileAsync`: `keptSlots` über den Horizont verteilt; Displacement/Re-Materialisierung; `InsertClipFill` analog (Zeile 437).

## `RewindCursor`/`RewindRing` (~Zeile 868)

Zurückspulen der Strang-Cursor bei Displacement — ein aus Slots abgeleiteter Rotationszeiger bleibt damit automatisch konsistent (kein gesonderter Zeiger-Zustand nötig).
