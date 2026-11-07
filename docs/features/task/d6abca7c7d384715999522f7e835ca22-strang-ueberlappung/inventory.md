# Bestandsaufnahme: Überlappungsfreie Strang-Rotation bei Interleaved-Ausstrahlungen

Analysiert wurde der Strang-Belegungsmechanismus des Sendeplan-Service (`ProgramScheduleService`) bezogen auf die Folge-Korrekturanforderung aus `issue.md` (Issue #343): Die Bedient-Mengen aufeinanderfolgender Ausstrahlungen sollen disjunkt sein, solange `strandCount > picksPerAiring`.

## Zusammenfassung

- `SelectAiringItemsInterleaved` (`ProgramScheduleService.cs:1810`) wählt pro Ausstrahlung Round-Robin über die Stränge. Bei `wanted >= strandCount` (typischer `SlotDuration`-Fall: `wanted = int.MaxValue`) läuft zunächst ein Probelauf mit Offset 0; bedient er weniger Stränge als nicht-leere existieren, gewinnt ein zweiter Lauf mit Rotations-Offset.
- Der Rotations-Offset kommt aus `StrandStartOffset(strandCount, airingStartUtc)` (`ProgramScheduleService.cs:1422`): `(Tage seit UnixEpoch) % strandCount` — also **+1 Strang pro Tag**, unabhängig davon, wie viele Stränge eine Ausstrahlung tatsächlich bedient (~2–3 bei 115-min-Filmen im 225-min-Fenster). Genau das erzeugt die Überlappung: Tag N bedient {k, k+1}, Tag N+1 {k+1, k+2}.
- Der gesuchte „fortlaufende Zeiger hinter dem zuletzt bedienten Strang" ist **nicht persistiert** — er lässt sich aber aus den geplanten Slots rekonstruieren: Picks werden zeitlich sequenziell belegt, der letzte Titel-Slot einer Ausstrahlung entspricht dem letzten Pick. `FindItemIndex` (bereits für die titelweise Slot-Validierung `SlotStillValid` genutzt) löst einen Slot zum Strang-Index auf.
- Beide Materialisierungspfade rufen dieselbe Auswahl: `MaterializeOccurrence` (EnsureCoverage, Zeile 1673) und `MaterializeOccurrenceEvicting` (Reconcile, Zeile 1102). Occurrences einer Regel werden in beiden Pfaden chronologisch abgearbeitet; neu erzeugte Slots liegen in `newSlots`, persistierte Belegung in `persistedOccupancy` (nur `Start`/`End`/`ProgramRuleId` — Inhalts-Ids fehlen bisher) bzw. `keptSlots` (volle `ProgramSlot`-Entities).
- `StrandStartOffset` muss erhalten bleiben: `EpisodeCount`-Regeln (`wanted < m`) rotieren weiterhin tagbasiert — Anforderung bestätigt „`EpisodeCount`-Rotation unverändert".
- Clip-Fill-Slots (`IsClipFill`, Clip-Gruppen-MediaItems) sind keine Titel-Picks und müssen bei der Zeiger-Ableitung ausgeschlossen werden — dieselbe Klassifikation nutzt bereits `SlotStillValid`/`RewindCursor` (`ClipMediaItemIds`).
- Test-Ausgangszustand: siehe [Tests](inventory/tests.md) — Baseline-Lauf der vollen Suite dokumentiert.

## Details

- [Logik](inventory/logic.md) — `ProgramScheduleService`: Auswahl, Rotation, Cursor-Fortschreibung, Materialisierungspfade
- [Datenmodell](inventory/models.md) — `ProgramSlot`, `ProgramRuleCursor`, `ProgramCursorContentType`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Baseline-Lauf), bestehende Testklassen und Hilfsmethoden
