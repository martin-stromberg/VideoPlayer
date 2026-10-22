# Review: Code (Schritt 23 — N19–N21)

## Ergebnis

**Status:** Erfüllt

## Geprüfte Aspekte

### N19 — Serien-Interleaving

- **Strang-Modell:** `StrandState` kapselt Ring + Cursor je Strang; `RuleState` bildet
  Stränge nur bei Genre-Material ≠ Füller und >1 Gruppe — alle anderen Pfade bleiben
  wörtlich Flachmodus (Delegatoren `PeekAt`/`Commit`/`RewindTo` auf `Strands[0]`).
- **Atomarität bewahrt:** Konsum-Picks (oversized heads) commit sofort (Cursor-Fortschritt
  unabhängig vom Ausgang — identisch zur Flach-Semantik); Take-Commits erst nach dem
  Konflikt-Check (`CommitTaken`). Bei gedropptem Airing bleiben Strang-Cursor unberührt.
- **Round-Robin-Terminierung:** `progressed`-Flag + `consumed[s] >= Items.Count`-Begrenzung
  + `done[s]` beim Tail-Bound — terminiert garantiert; `s--`-Retry bleibt auf demselben
  Strang-Positionsplatz (oversized Kopf wird ersetzt, nicht die Position).
- **Stabile Position:** Strang-Reihenfolge = Erstauftretens-Reihenfolge in der Expansion
  (deterministisch: Filme/"other" zuerst, dann Serien alphabetisch); jede Occurrence
  beginnt bei Strang 0.
- **Persistenz:** `ProgramRuleCursors` je `(ProgramRuleId, StrandKey)` — Unique-Index als
  Composite; NULL-StrandKey = Flach-Cursor (Abwärtskompatibel: Bestandszeilen gelten
  weiterhin als Gesamt-Cursor).
- **Rewind:** je Strang der früheste verdrängte Inhalt — identische Semantik wie bisher je
  Regel.
- **`AiringReproducibleInterleaved`:** Hypothesen über Präfix-Pickzahl p (Constraint
  `p % m == strandOf[0]` — der Pick des ersten Airing-Slots fixiert die Phase),
  Kopf-Rekonstruktion je Strang (`firstIndex − prefixCount`, modular), volle Simulation mit
  echter `SelectAiringItemsInterleaved`-Logik + Fenster-Bound; Vergleich Item/Start/End.
  Per-Strang-Konsekutivitäts-Check fängt Episoden-Verwürfelung früh ab.

### N20 — Werktag/Wochenende

- Enum-Anhängung (4/5) statt Einfügen — persistierte int-Werte unverändert.
- `BuildOccurrences`-Fälle Mo–Fr / Sa+So; `Enum.IsDefined`-Validierung deckt neue Werte;
  `DayOfWeek`/`SingleDate` werden nur bei Weekly/Once persistiert — für die neuen Arten
  bewusst `null` (Validierungstests belegen).
- UI-Optionen + `RecurrenceLabel` deutsch konsistent.

### N21 — Overlay-Editor

- Editor-Struktur unverändert, nur äußere Hülle ersetzt (Card → Overlay+Dialog); IDs,
  Handler (`SaveRuleAsync`, `CancelEdit`, `OnRecurrenceChanged`, …) unverändert.
- `.rule-editor-card`-Klasse entfernt, `.rule-editor-dialog` ergänzt.

### Daten/Backup/Migration

- `StrandKey` nullable → Alt-Backups ohne Spalte brauchen keinen Default; in
  `OptionalRestoreColumns` registriert; Regressionstest `..._StrandKey` liest den
  wiederhergestellten Stand via `ChangeTracker.Clear()` (getrackte Seed-Entitäten würden
  sonst den Seed-Wert vortäuschen).
- Index-Wechsel in der Migration verlustfrei (Drop/Create Unique).

### Verbleibende Risiken / bewusste Grenzen

- Bestands-Genre-Regeln mit einem NULL-Gesamt-Cursor starten die Stränge bei 0 (einmaliger
  Drehsprung beim ersten Lauf nach dem Update) — dokumentiert, akzeptiert.
- `AiringReproducibleInterleaved` ist konservativ (bei Zweifel → Verdrängen statt
  Beibehalten): erhöht im schlimmsten Fall kurzfristig die Verdrängungsrate, nie das
  Fehlverhalten.
- `p % m`-Constraint setzt voraus, dass Präfix-Picks ohne Strang-Ausfall rotieren
  (Dropout mitten im Präfix → Hypothese abgelehnt) — konservativ, deckt den Regelfall.
- SQLite toleriert mehrere NULL in Unique-Indizes — der Composite schützt echte
  Strang-Doppelzeilen; NULL-Duplikate wären technisch möglich, werden aber nicht geschrieben
  (PersistCursors findet/aktualisiert die NULL-Zeile deterministisch).
