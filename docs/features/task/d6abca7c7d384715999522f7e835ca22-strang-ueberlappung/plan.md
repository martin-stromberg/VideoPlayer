# Umsetzungsplan: Überlappungsfreie Strang-Rotation (Issue #343)

## Übersicht

Der Rotationszeiger der Interleaved-Auswahl (`SelectAiringItemsInterleaved`) wird von der starren Tagesrotation (+1 Strang/Tag) auf einen **fortlaufenden Round-Robin-Zeiger** umgestellt: Jede Ausstrahlung setzt direkt hinter dem zuletzt tatsächlich bedienten Strang der Vorgänger-Ausstrahlung an. Der Zeiger wird nicht persistiert, sondern aus den geplanten Slots abgeleitet — der letzte auflösbare Titel-Slot der Regel vor dem aktuellen `airingStartUtc` bestimmt `strandIndex + 1 mod m`. Damit gilt die Fortsetzung innerhalb eines Planlaufs, über Lauf- und Prozessgrenzen und bleibt bei Displacements/Manuelleingriffen automatisch konsistent (Ground Truth = geplanter Bestand).

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Geplante Ausstrahlung (Occurrence) einer interleaved Genre-Regel — ihre bediente Strangmenge im Verhältnis zum Vorgänger |
|| Auslösende Ereignisse | `EnsureCoverageAsync`/`ReconcileAsync` (Materialisierung je Occurrence), keine Nutzeraktion |
|| Erwartete Wirkung | Bedient-Mengen aufeinanderfolgender Ausstrahlungen disjunkt, solange `strandCount > picksPerAiring`; abwechselnde Sammlungen/Serien über die Tage |
|| Umgang mit fehlenden Objekten | Erstplanung ohne Vorgänger-Slots → Zeiger 0; nicht auflösbarer Vorgänger-Slot → überspringen (älterer auflösbarer gewinnt); `ProgramRuleCursors` unverändert wiederverwendet; kein neues Persistenzobjekt |

### Scope-Ausschlüsse

Keine.

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Rotationszeiger | **Ableitung aus geplanten Slots** (letzter Titel-Slot der Regel mit `Start < airingStartUtc` → `strandIndex + 1 mod m`) statt persistierter Zeiger oder Occurrence-Index-Formel | Deterministisch und prozessübergreifend korrekt, ohne EF-Migration/Backup-Registrierung (AGENTS §7 entfällt); selbstheilend bei Displacement, Cursor-Rewind und manuellen Planänderungen — der Zeiger folgt immer dem tatsächlich geplanten Stand. Eine Formel `occurrenceIndex × bedienteStrangzahl` scheidet aus, weil die Bedientzahl je Ausstrahlung variiert (2–3) und nicht aus dem Occurrence-Index ableitbar ist; ein persistierter Zeiger müsste bei Displacement/Rewind mitverwaltet werden. |
|| Slot→Strang-Auflösung | `FindItemIndex`-Semantik wiederverwenden (MediaItemId → MovieId → TVShowEpisodeId), Clip-Slots via `IsClipFill`/`ClipMediaItemIds` ausgenommen | Exakt die Auflösung, die `SlotStillValid` bereits für die titelweise Validierung nutzt — konsistente Klassifikation Titel vs. Clip. |
|| Probelauf-Mechanismus | Beibehalten: Probelauf Offset 0; Rotationslauf nur wenn bediente Stränge < nicht-leere Stränge | All-served-Fenster behalten ihre stabile Tagesstruktur (Anforderung „Stabilität bei all-served-Fenstern"); blockierte Stränge lösen Rotation weiter aus. |
|| `EpisodeCount`-Pfad | Unverändert `Run(StrandStartOffset(m, airingStartUtc))` bei `wanted < m` | Explizite Anforderung („`EpisodeCount`-Rotation unverändert"). |

## Programmabläufe

### Rotationszeiger-Ableitung je Ausstrahlung

1. `MaterializeOccurrence` bzw. `MaterializeOccurrenceEvicting` berechnet nach `airingStart` (nach `ShiftPastInvaders`/`WindowLostToTakeover`): bei `state.IsInterleaved` die Kandidatenmenge = Slots dieser Regel (`persistedOccupancy`/`keptSlots` ∪ `newSlots`).
2. `StrandRotationOffset(state, airingStart, kandidaten)`: Kandidaten mit `Start < airingStart` absteigend nach `Start`; erster Slot, der als Titel-Slot klassifiziert wird (`!IsClipFill`, MediaItemId nicht in `ClipMediaItemIds(state)`) und per `FindItemIndex` einem Strang zugeordnet werden kann → Ergebnis `(strangIndex + 1) % m`; kein Treffer → `0`.
3. `SelectItems` erhält den Offset als Parameter und reicht ihn an `SelectAiringItemsInterleaved` weiter.
4. Dort ersetzt der Zeiger den bisherigen `StrandStartOffset`-Aufruf im Rotationspfad: `probe = Run(0)`; wenn `offset != 0` und bediente Stränge < nicht-leere → `Run(offset)`, sonst `probe`. `wanted < m` (EpisodeCount) bleibt bei `Run(StrandStartOffset(...))`.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`MaterializeOccurrence`, `MaterializeOccurrenceEvicting`, `SelectItems`, `SelectAiringItemsInterleaved`, `StrandRotationOffset` neu, `FindItemIndex`, `ClipMediaItemIds`), `RuleState`, `StrandState`.

### Fortsetzung über Planlauf-Grenzen

Ein zweiter `EnsureCoverage`-Lauf lädt die persistierten Slots erneut in `persistedOccupancy`; die Ableitung liefert denselben Zeiger, den die Vorgänger-Ausstrahlung hinterlassen hat — ohne gespeicherten Zeiger-Zustand.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `OccupiedSlot` | private record (in `ProgramScheduleService`) | Persistenz-Belegungseintrag: `Interval`, `RuleId`, `MediaItemId`, `MovieId`, `EpisodeId`, `IsClipFill` — ersetzt das bisherige `(Interval Slot, long? RuleId)`-Tupel von `persistedOccupancy` und dient als normalisierter Kandidatentyp der Zeiger-Ableitung (Mapping auch aus `ProgramSlot` für `keptSlots`/`newSlots`) |

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service)

- **Neue Methoden:**
  - `StrandRotationOffset(RuleState, DateTime airingStartUtc, IEnumerable<OccupiedSlot>)` → `int` — Zeiger-Ableitung wie im Ablauf beschrieben.
  - `FindItemIndex(List<SchedulableItem>, long mediaItemId, long? movieId, long? episodeId)` → `int` — id-basierte Überladung; bestehende `FindItemIndex(List<SchedulableItem>, ProgramSlot)` delegiert darauf.
  - `TitleStrandIndex(RuleState, OccupiedSlot, HashSet<long> clipIds)` → `int` — Strang-Index eines Slot-Inhalts (−1 für Clips/Unauflösbares); Strang-Schleife wie in `SlotStillValid`.
- **Geänderte Methoden:**
  - `SelectAiringItemsInterleaved` — neuer Parameter `int strandRotationOffset`; ersetzt den internen `StrandStartOffset`-Aufruf im `wanted >= m`-Pfad (Kommentar aktualisieren).
  - `SelectItems` — neuer Parameter `int strandRotationOffset`, Weitergabe an `SelectAiringItemsInterleaved`.
  - `MaterializeOccurrence` — Zeiger-Berechnung aus `persistedOccupancy` (Regel-Filter) ∪ `newSlots`; Übergabe an `SelectItems`.
  - `MaterializeOccurrenceEvicting` — Zeiger-Berechnung aus `keptSlots` ∪ `newSlots`; Übergabe an `SelectItems`.
  - `EnsureCoverageAsync` — `persistedOccupancy`-Projektion um `MediaItemId`, `MovieId`, `TVShowEpisodeId`, `IsClipFill` erweitern und auf `OccupiedSlot` umstellen (Verwendungsstellen `p.Slot`, `p.RuleId` bleiben).

## Datenbankmigrationen

Keine — keine Schemaänderung, kein neuer persistierter Datensatz.

## Validierungsregeln

Keine neuen Eingaben — das Verhalten ist eine interne Planungsinvariante.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Re-Materialisierung/Lookback-Replay:** Bereits belegte Occurrences werden im Lookback-Fenster erneut materialisiert; ihr `airingStart` verschiebt sich hinter den eigenen Tail, der Zeiger steht hinter dem eigenen letzten Pick — die Replay-Picks landen hinter dem Tail und kollidieren wie bisher mit der Folge-Ausstrahlung (→ `DroppedConflicts`, idempotent). Verhalten analog zum bisherigen Tages-Offset.
- **All-served-Wechsel:** Fenster, die alle Stränge bedienen, lassen den Probelauf gewinnen; danach zeigt der Zeiger wieder hinter den letzten Pick — konsistent.
- **Blockierte Stränge:** Stränge ohne Pick (Altersfenster/Bound) werden vom Zeiger übersprungen (Zeiger steht hinter dem letzten Pick) — wie bisher führt das zu selteneren Bedienungen permanenter Blocker; akzeptabel und im Sinne der Disjunktheit.
- **`FindItemIndex`-Überladung:** Reine Extraktion — bestehende Aufrufe (`SlotStillValid`, `RewindRing`, `SlotMinAge`) unverändert.
- **`persistedOccupancy`-Projektion:** Vier zusätzliche Spalten im selben Query — kein zusätzlicher DB-Roundtrip.
- **Erster geplanter Tag nach Deployment auf Altbestand:** Der Zeiger setzt hinter dem letzten tatsächlich geplanten Pick an — die Fortsetzung ist fachlich korrekt; vorhandene Tagesbelegungen bleiben unverändert (kein Plan-Reset nötig, Verbesserung greift bei neu geplanten/verdrängten Ausstrahlungen).

## Umsetzungsreihenfolge

1. **Pflichttest schreiben (rot):** `EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands` in `ProgramScheduleServiceTests_StrandRotation.cs` — bediente Strangmengen aufeinanderfolgender Ausstrahlungsabende sind disjunkt (`strandCount > picksPerDay`). Verifizieren, dass er gegen den Ist-Stand rot läuft.
   - Voraussetzungen: `SeedCollectionRotationScenarioAsync`, `PrimetimeSeed.AiringOf` (vorhanden).
2. **Regressionstest Lauf-Grenze schreiben (rot):** `EnsureCoverage_SecondRun_ContinuesDisjoint` — zweiter `EnsureCoverage`-Lauf nach Horizon-Fortschreibung setzt den Zeiger aus persistierten Slots fort (disjunkte Bedient-Menge am Übergang).
   - Voraussetzungen: dieselben Seed-Helpers.
3. **`OccupiedSlot`-Record + `persistedOccupancy`-Erweiterung** in `EnsureCoverageAsync`; `MaterializeOccurrence`-Signatur anpassen.
   - Voraussetzungen: keine.
4. **`FindItemIndex`-Überladung + `TitleStrandIndex` + `StrandRotationOffset`** implementieren.
   - Voraussetzungen: Schritt 3.
5. **`SelectItems`/`SelectAiringItemsInterleaved`/beide Materialize-Pfade** auf den Zeiger umstellen.
   - Voraussetzungen: Schritte 3–4.
6. **Build (Debug+Release), volle Testsuite** — Tests grün machen, keine Regressionen.
   - Voraussetzungen: Schritte 1–5.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands` | `ProgramScheduleServiceTests_StrandRotation` | **Pflichttest:** Pro Abendpaar (i, i+1) ist `served(i) ∩ served(i+1) = ∅`, solange `strandCount > picksPerDay` (6 `collection:{id}`-Stränge, 2 Picks/Abend); rot auf Ist-Stand (Tag N bedient {k,k+1}, Tag N+1 {k+1,k+2}). |
| `EnsureCoverage_SecondRun_ContinuesDisjoint` | `ProgramScheduleServiceTests_StrandRotation` | Zweiter `EnsureCoverage`-Lauf zu späterem Zeitpunkt: die am Lauf-Übergang neu geplante Ausstrahlung ist zur letzten persistierten disjunkt — Zeiger gilt über Planläufe hinweg; rot auf Ist-Stand. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_StrandRotation.EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays` | Bleibt gültig (Leads ändern sich weiter täglich); Erwartung ohne Anpassung erfüllt — Verifikation im Lauf. |
| `ProgramScheduleServiceTests_StrandRotation.EnsureCoverage_AllStrandsServed_NoRotation_StableStructure` | Probelauf unverändert → unverändert erwartet. |
| `ProgramScheduleServiceTests_StrandRotation.EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` | EpisodeCount-Pfad unverändert → unverändert erwartet. |
| `ProgramScheduleServiceTests_StrandRotation.*` (übrige), `ProgramScheduleServiceTests_*` (Primetime*, Interleaving, AgeWindow, CursorSelection, Reconcile …) | Volle Suite als Regressionsschirm; Erwartungsbilder einzelner Szenarien könnten sich durch die geänderte Strangwahl verschieben — begründete Anpassung nur wenn nachweislich neue (korrekte) Strangfolge. |

### E2E-Tests (primärer Funktionsnachweis)

Keine geplanten E2E-Tests — Begründung: Die Anforderung berührt keinen Benutzerfluss und keine UI; die Planungsinvariante ist ein Service-Verhalten, das durch die SQLite-Integrationstests (echter `EnsureCoverage`-/`Reconcile`-Pfad über `ProgramScheduleTestDb`) auf fachlicher Ebene nachgewiesen wird. Ein Playwright-Test könnte dieselbe Bedingung nur indirekt über den Sendeplan prüfen und wäre kein zusätzlicher Funktionsnachweis gegenüber den zeigerweisen Assertions. Die reale E2E-Abnahme auf der Referenzinstanz ist in dieser Umgebung nicht erreichbar und wird in `test-results.md` als dokumentierter Verzicht geführt (wie im Vorgängerlauf).

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path: faire Fortsetzung | `EnsureCoverageAsync` des Workers | Genre-Regel (SlotDuration, Clip-Gruppe) mit vielen `collection:{id}`-Stränge, nur ~2 Picks/Ausstrahlung | Materialisierung je Occurrence, Zeiger aus Slots | Aufeinanderfolgende Ausstrahlungen bedienen disjunkte Stränge; über den Zyklus jeder Strang einmal | `EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands` |
|| 2 | Lauf-Grenze (Horizon-Fortschreibung) | Zweiter `EnsureCoverage`-Lauf zu späterem `utcNow` | Tag N bereits persistiert geplant, Tage N+1.. neu | Re-Materialisierung neuer Occurrences | Neue Ausstrahlung setzt hinter letztem persistierten Pick an — disjunkt zum persistierten Vortag | `EnsureCoverage_SecondRun_ContinuesDisjoint` |
|| 3 | All-served-Fenster stabil | `EnsureCoverageAsync` | Wenige Stränge, kurze Titel — Probelauf bedient alle | Probelauf gewinnt | Identische Tagesstruktur wie bisher | `EnsureCoverage_AllStrandsServed_NoRotation_StableStructure` (Bestand) |
|| 4 | Ordnung/Cursor bewahrt | `EnsureCoverageAsync` + `ReconcileAsync` | Geordnete Sammlungsstränge | Rotation aktiv | Kein Teil übersprungen/vorgezogen; `ProgramRuleCursors` je Strang korrekt; keine `StrandSequenceBroken`-Verwerfung | `..._ReleaseOrderAndCursorsPreserved`, `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` (Bestand) |
|| 5 | Reale End-to-End-Abnahme | Worker-Pass auf Referenzinstanz `http://192.168.10.230:83` | Produktivdaten mit ~570 Strängen | Deployment + nächste Re-Bewertung | Sendeplan zeigt abwechselnde Sammlungen, keine 2–3-Tage-Wiederholung | Nicht in dieser Umgebung durchführbar → dokumentierter Verzicht (Anwenderwille wie Vorgängerlauf) |

## Offene Punkte (unbeantwortet)

Kein User-Feedback verfügbar; folgende Punkte wurden mit der plausibelsten Annahme entschieden:

| # | Offener Punkt | Getroffene Annahme / Begründung |
|---|---------------|----------------------------------|
| 1 | Persistierter Zeiger vs. Ableitung? | Ableitung aus geplanten Slots (siehe Designentscheidungen) — erfordert keine Migration/Backup-Registrierung, bleibt bei Displacement konsistent und setzt die Issue-Lösungsidee „fortlaufender Zeiger hinter letztem bedientem Strang" exakt um. |
| 2 | Verhalten bei nicht auflösbarem Vorgänger (z. B. letzte Ausstrahlung komplett aus Material gelöscht)? | Älterer auflösbarer Slot gewinnt; ganz ohne Auflösung Zeiger 0 — deterministisch, harmlos (Selbstkorrektur im nächsten Zyklus). |
| 3 | Gilt die Disjunktheit auch bei blockierten/leeren Strängen dazwischen? | Zeiger steht hinter dem letzten Pick — vom Walk übersprungene, unbediente Stränge werden am Folgetag ebenfalls übersprungen (sie drehen ohnehin nicht); Invariante gilt für bediente Mengen, nicht für besuchte Positionen. |
| 4 | `OccupiedSlot`-Record statt Tupel-Erweiterung? | Ja — 6 Felder als Tupel wären unleserlich; der Record hält die `MaterializeOccurrence`-Signatur lesbar und normalisiert die drei Quellen (`persistedOccupancy`, `keptSlots`, `newSlots`). |
