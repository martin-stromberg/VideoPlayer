# Umsetzungsplan: Senderplanung fehlerhaft

## Übersicht

Zwei belegte Logikfehler in `ProgramScheduleService` werden korrigiert: (1) Eine im
24-h-Rückblick (`OccurrenceLookback`) komplett beendete Occurrence verbraucht in
`MaterializeOccurrence` Ringpositionen über `CommitTaken`, obwohl sie keinen einzigen
Slot erzeugt. (2) Die positionsbasierte Reproduzierbarkeitsprüfung
(`AiringReproducible` / `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible`)
verdrängt geplante Slots, sobald die Materialmenge wächst. Nach Anwender-Entscheidung
wird die Reproduktions-Prüfung **vollständig durch eine titelweise Validierung ersetzt**:
`ReconcileAsync` prüft jeden geplanten Slot einzeln (Material, Regel-/Filterzugehörigkeit,
Altersfenster zum geplanten Start, Laufzeit, Occurrence-Anker); was passt, bleibt — bei
Mengenwachstum ändert sich an keinem Slot etwas, also gibt es keine Verdrängung.
Betroffen ist ausschließlich `VideoWebPlayer/Services/ProgramScheduleService.cs` — keine
Schema-, Interface-Signatur- oder Konfigurationsänderung. Die 16 Primetime-Tests
(3 rot, 13 grün) sind die verbindliche Soll-Spezifikation.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Reconcile-Validierung | **Titelweise Validierung ersetzt die Re-Simulation komplett.** Neue interne Prüfung je Slot (`SlotStillValid`) plus Kopf-Anker je Ausstrahlung (`AnchorProvable`); die Re-Simulations-Machinerie (`ReproContext`, beide `AiringReproducible`-Überladungen, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `PrefixTakeAssignments`, `DeferralAssignments`, `ProvableDeferral`, `MaxDeferredStrands`, `SlotMatchesItem`) wird **entfernt** | Anwender-Modell: Positionsbasierte Beweise sind bei Mengenwachstum prinzipiell falsch — `MixUnorderedRing` sortiert die Ringordnung mit jedem neuen Item um und die Strangmenge selbst kann wachsen. Die titelweise Prüfung ist von Ringordnung und Strangzahl unabhängig: ein geplanter Slot bleibt gültig, solange sein Inhalt existiert, zur Regel passt, altersmäßig starten darf und seine Ausstrahlung einen Occurrence-Anker hat. Damit ist Wachstum definitionsgemäß verdrängungsfrei — kein Toleranz-Budget nötig |
| Granularität der Verdrängung | **Unverändert: ausstrahlungsweise + Regel-Suffix** (`InvalidGroups` → `Displace`/`DisplaceRuleFuture`) | Sequenzintegrität geordneter Stränge: ein invalider Titel korrumpiert die Pick-Reihenfolge aller Folge-Slots (Beleg: `Reconcile_NewEpisodeInserted_ReplansInAirOrder` erwartet, dass die ganze Ausstrahlung neu materialisiert wird — ein behaltener Kopf würde die Occurrence-Materialisierung per Atomar-Konfliktcheck verwerfen und den Cursor doppelt konsumieren). Nur die *Erkennung* wird titelweise; gültige Titel werden ausschließlich nachgelagert einer echten Änderung verdrängt — nie bei Mengenwachstum, weil Wachstum keinen Slot invalid macht |
| Kopf-Anker je Ausstrahlung | `AnchorProvable(head)`: ∃ live-Occurrence `o` der Regel mit `o ≤ head.Start` und (`o == head.Start` ∨ `ShiftProvable(o, head.Start)` ∨ `head.Start − o ≤ UnwrittenHeadroom`) | Drei Beweisformen decken alle legitimen Kopf-Lagen ohne Ring-Rekonstruktion ab: exakter Start, Invader-Shift (bestehender `ShiftProvable`-Beleg über Scheduled ∪ Displaced) und ungeschriebenes Präfix (teil-ausgestrahlte Occurrence — `Reconcile_PartiallyAiredOccurrence_*`, Issue-315/320-Tests). `UnwrittenHeadroom = rule.SlotDuration ?? (wanted − 1) · maxItemDuration` ist eine reine Zeit-/Inhaltsschranke — wachstumsstabil, weil sie keine Ringpositionen kennt. Kein Kandidat → `NoOccurrenceAnchor`; Kandidaten da, aber keiner beweisbar → `AnchorWithoutInvaderEvidence` |
| Sequenzprüfung | **Nur geordnete Stränge** (`PreservesOrder`): aufeinanderfolgende Picks desselben Strangs innerhalb einer Gruppe müssen ring-kontigus sein (`idx == (lastIdx + 1) % count`); erster Pick des Strangs in der Gruppe ist Baseline. Ungeordnete Ringe: keine Positionsprüfung | Erkennt Umnumerierung und eingeschobene Episoden inhaltsverankert (`Reconcile_NewEpisodeInserted`) ohne Positionsannahmen. Für ungeordnete Ringe sind Skip-Nachweise bei gewachsenem Ring bedeutungslos — bewusste Abschwächung, dokumentiert unter Seiteneffekte. Ein neuer Strang (neue Serie im Genre) verändert keinen bestehenden Slot und kann daher nichts invalidieren — die Strang-Struktur bestehender Ausstrahlungen wird nicht re-verifiziert |
| Cursor-/Ring-Stand bei Wachstum | **Unverändert** — `NextIndexFor` ist inhaltsverankert | `ProgramRuleCursor` speichert Inhalt (`LastContentType`/`LastContentId`), nie Positionen; `NextIndexFor` lokalisiert den Inhalt im neu gemischten Ring. Neue Items treten an ihrer Hash-Position automatisch in kommende Ausstrahlungen ein. Da Wachstum keine Verdrängung auslöst, ist auch kein `RewindCursor` nötig. `MixUnorderedRing`/`ItemOrderKey`/`StrandOrderKey`/`Fnv1aHash` bleiben unangetastet (deterministische, prozessinvariante Ordnung ist Randbedingung) |
| Clip-Slots | Mitgliedschaft im aktuellen Clip-Ring (`ClipMediaItemIds`) + `IsAllowedAt` am Slot-Start | Entspricht dem Einfüge-Prädikat von `InsertClipFill`/`FillAiringSegments`. Der bisherige Clip-Vorfilter (`ClipChainBroken`, `TitlelessAiring`) löst sich auf: `ClipChainBroken` ist in lückenlosen `GroupAirings`-Gruppen unerreichbar; `TitlelessAiring` bleibt als Gruppen-Check erhalten |
| Filler-Slots | Unveränderte Einzelprüfung | Filler haben keine Sequenz — die bestehende Prüfung (Regel vorhanden/aktiv, MediaItem in Expansion) bleibt |
| Lookback-Occurrences im Reconcile | **Unverändert** — reine Anker-Kandidaten und Re-Materialisierungs-Basis | Klärt die offene Frage der Anforderung: „beendete Occurrences verbrauchen keine Materialposition" betrifft nur Materialisierung/Cursor-Verbrauch in `EnsureCoverageAsync`; lesende Anker ohne Cursor-Wirkung sind davon nicht erfasst. Occurrences `≥ coverageStart − OccurrenceLookback` werden weiter re-materialisiert (`MaterializeOccurrenceEvicting`) |
| `MaterializeOccurrence` (Lookback-Konsum) | Early-Return `ranges.Count == 0` **nach** `SelectItems`/Ranges-Aufbau, vor Konfliktcheck und `CommitTaken` | Spiegelt exakt den vorhandenen Guard in `MaterializeOccurrenceEvicting` (Z. 1570–1573). Eine Vorab-Prüfung vor `SelectItems` ist unsicher: `ShiftPastInvaders` und Overshoot machen das tatsächliche Ende erst nach der Auswahl bekannt. `SelectItems` ist rein lesend — erst `CommitTaken` verbraucht Positionen |

## Programmabläufe

### Materialisierung im Extend-only-Lauf (`EnsureCoverageAsync`) — korrigiert

1. `BuildOccurrences` baut Occurrences mit `OccurrenceLookback` (24 h) hinter
   `coverageStart` — **unverändert** (laufende Ausstrahlungen müssen weiter erfasst
   werden, u. a. für Szenario 1.f „Regel mitten in der Primetime angelegt").
2. `MaterializeOccurrence` je Occurrence: `ShiftPastInvaders` → `SelectItems`
   (rein lesend) → `taken.Count == 0` → `return` (heute schon).
3. Ranges-Aufbau: nur Intervalle mit `End > coverageStart` werden aufgenommen.
4. **NEU:** `ranges.Count == 0` → `return`. Die Ausstrahlung ist zum
   Planungszeitpunkt vollständig gelaufen — keine Slot-Zeilen, kein `CommitTaken`,
   kein Cursor-/Ringverbrauch (Take- und Skip-Positionen bleiben unangetastet).
   Kommentar analog zur Evicting-Variante.
5. Unverändert danach: Atomar-Konfliktcheck → Slots schreiben → `blocked.AddRange`
   → `CommitTaken` → `summary.SlotsCreated`.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`MaterializeOccurrence`,
`SelectItems`, `CommitTaken`).

### Titelweise Reconcile-Validierung — neu

Ersetzt `ReconcileWindow.Validate` inhaltlich; `Rebuild` (Evidence, Occurrences,
BoundStarts, `GroupAirings`, Filler-Split) bleibt unverändert.

1. Je Regel werden die Ausstrahlungsgruppen (`window.Airings`, von `GroupAirings`
   gebildete lückenlose Ketten gleicher Regel, an der Coverage-Grenze geteilt) in
   Startreihenfolge geprüft. Ausstrahlungen, die vollständig vor `coverageStart`
   enden, werden übersprungen (Historie).
2. Gruppen-Vorprüfung wie heute: Regel fehlt → `RuleMissing`; `!rule.IsActive` →
   `RuleInactive`; keine Occurrences der Regel → `NoOccurrenceAnchor`. Außerdem:
   Gruppe ohne einzigen Titel-Slot → `TitlelessAiring`.
3. **Kopf-Anker:** `AnchorProvable(head)` — Kandidaten = alle Occurrence-Starts der
   Regel `≤ head.Start` (aus `_occurrenceStarts`, die über `reconcileLookback`
   bis `_maxOccurrenceLookback` reichen). Beweis über exakte Lage, `ShiftProvable`
   (Invader-Chain aus Scheduled ∪ Displaced-Evidence) oder `UnwrittenHeadroom`.
4. **Slotweise Prüfung** (`SlotStillValid`), jeder Slot der Gruppe:
   - **Clip-Slot** (`MediaItemId ∈ ClipMediaItemIds(state)`, bzw. `IsClipFill`):
     gültig, wenn das MediaItem noch im Clip-Ring expandiert — sonst
     `ItemNotInExpansion` (deckt entfernte Clip-Zuordnung, geänderte Clip-Gruppe,
     entzogene Freigabe des Clip-Materials).
   - **Titel-Slot:** `FindItemIndex(state.Items, slot)` lokalisiert den Inhalt in der
     aktuellen Expansion (MediaItemId, dann `MovieId`/`TVShowEpisodeId` — deckt
     gelöschtes Material, entfernte Genre-/Quell-Zuordnung, entzogene Freigabe via
     `AccessContext`-Filter ab) — `-1` → `ItemNotInExpansion`. Dann:
     - **Laufzeit:** `slot.End − slot.Start` muss der Laufzeit des gefundenen Items
       entsprechen — des Items selbst oder, bei Representative-Wechsel, der zum
       `slot.MediaItemId` passenden `MediaItemAlternates`-Variante (gleiche
       Toleranz wie `SlotMatchesItem`, ohne dessen Startvergleich). Abweichung →
       `SlotSequenceMismatch` (Duration-Änderung/Re-Link ist eine echte Änderung).
     - **Altersfenster:** `ProgramScheduleAgeWindow.IsAllowedAt(item.AgeRatingMinAge,
       slot.Start, ageRule)` — gescheitert → `SlotSequenceMismatch` (deckt
       verschärfte Fenster/Modus-Wechsel: `Reconcile_SameWindow_KeepsAiring_
       TighterWindow_Displaces`, `Reconcile_RunningSlot_AgeWindowBlocksAtOwnStart_
       IsDisplaced`).
     - **Sequenz (nur geordnete Stränge):** über `FindItemIndex` je Strang
       zugeordnet; folgt der Slot auf einen früheren Pick desselben Strangs in
       derselben Gruppe, muss sein Ring-Index der direkte Nachfolger sein —
       sonst `StrandSequenceBroken`. Erster Pick des Strangs in der Gruppe =
       Baseline (sein Vorgänger kann ungeschrieben oder in einer früheren Gruppe
       liegen). Ungeordnete Ringe und Clip-Positionen: keine Sequenzprüfung.
5. Erste invalide Ausstrahlung je Regel → `InvalidGroups` (identische Struktur wie
   heute: `(ordered, firstInvalid)` — Ausstrahlungs-Index, ausstrahlungsatomar);
   `HasRescuableInvalid` weiter nur bei aktiver Regel → validierungsgetriebenes
   Fenster-Widening bleibt.
6. **Unverändert danach** — die Verdrängungs- und Neuplanungs-Machinerie ist der
   vom Anwender-Modell geforderte „neue Titel wird gewählt"-Schritt: Suffix-
   Verdrängung je Regel, Filler-Einzelprüfung, `RewindCursor`/`RewindRing`
   (verdrängte Folgen werden ohne Episodensprung an der nächsten freien Occurrence
   wieder eingeplant), Re-Materialisierung über `MaterializeOccurrenceEvicting`
   auf dem Basisfenster (Selektion mit Sequenz/Cursor, Altersfenster, Karenzzeit,
   Bound — der Betrachtungspunkt wandert dort intern bereits „auf das Ende des
   aktuellen Titels"), `Evicts`-Konfliktauflösung, `InsertClipFill`, `FillGaps`,
   `PersistCursors`, SaveChanges.

Beteiligte Klassen/Komponenten: `ReconcileWindow` (`Rebuild`, `Validate`),
`ProgramScheduleService` (`SlotStillValid`, `AnchorProvable`, `UnwrittenHeadroom`,
`MatchingItemDuration` — neue private Hilfsmethoden; `FindItemIndex`,
`ClipMediaItemIds`, `ShiftProvable`, `GroupAirings`, `BoundStarts`, `BoundaryAfter`
— unverändert weiterverwendet).

## Neue Klassen

Keine neuen Klassen — nur neue private Hilfsmethoden in `ProgramScheduleService`:

| Methode | Typ | Zweck |
|---------|-----|-------|
| `SlotStillValid` | private static | Titelweise Gültigkeitsprüfung eines Slots (Clip-Mitgliedschaft bzw. Item-Mitgliedschaft, Laufzeit inkl. Alternates, Altersfenster am Start, Strang-Kontiguität geordneter Stränge) |
| `AnchorProvable` | private static | Kopf-Anker-Beweis: exakte Lage ∨ `ShiftProvable` ∨ innerhalb `UnwrittenHeadroom` |
| `UnwrittenHeadroom` | private static | Zeitbudget ungeschriebener Präfix-Takes: `rule.SlotDuration ?? (wanted − 1) · maxItemDuration` der Expansion (`wanted = EpisodeCount ?? 1`; `SlotDuration` deckt Interleaved- und Flat-Perioden gleichermaßen ab) |
| `MatchingItemDuration` | private static | Dauer-Auflösung des zu einem Slot passenden Items (Item-Duration oder passende `MediaItemAlternates`-Variante) — übernimmt den Laufzeit-/Alternate-Teil von `SlotMatchesItem` |

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service, `internal sealed`)

- **Geänderte Methoden:**
  - `MaterializeOccurrence` — **NEU:** Early-Return `ranges.Count == 0` vor dem
    Konfliktcheck (Z. 2106), damit komplett gelaufene Lookback-Occurrences weder
    Slots noch Cursor-Verbrauch erzeugen (spiegelt `MaterializeOccurrenceEvicting`
    Z. 1570–1573).
  - `ReconcileWindow.Validate` — ersetzt den `AiringReproducible`-Aufruf durch den
    titelweisen Ablauf (s. Programmabläufe); der `laterSlots`-Parameter
    (`LaterGroupSlots`-Plumbing für die Strang-Rekonstruktion) entfällt.
- **Neue Methoden:** `SlotStillValid`, `AnchorProvable`, `UnwrittenHeadroom`,
  `MatchingItemDuration` (alle `private static`; `SlotStillValid` liefert neben dem
  bool den `AiringInvalidReason` für die Diagnose-Zeile).
- **Entfernte Methoden/Typen:** `ReproContext` (inkl. `NoteFailure`/Memo),
  `AiringReproducible` (beide Überladungen, Z. 678 und 739),
  `FlatAiringTailReproducible` (Z. 753), `InterleavedAiringTailReproducible`
  (Z. 932), `PrefixTakeAssignments` (Z. 1240), `DeferralAssignments` (Z. 1256),
  `ProvableDeferral` (Z. 1288), `MaxDeferredStrands` (Z. 1231), `SlotMatchesItem`
  (Z. 3532 — Laufzeitteil zieht in `MatchingItemDuration` um).
- **Unverändert bleiben:** `GroupAirings`, `ClipMediaItemIds`, `ShiftProvable`,
  `ShiftPastInvaders`, `BoundStarts`, `BoundaryAfter`, `FindItemIndex`,
  `MixUnorderedRing`, `ItemOrderKey`, `StrandOrderKey`, `Fnv1aHash`,
  `SelectStrandCursor`, `NextIndexFor`, `RuleState`/`StrandState`/`ClipGroupState`
  (`SetItems`, `Commit`, `CommitScan`, `RewindTo`), `SelectItems`,
  `SelectAiringItems`, `SelectAiringItemsInterleaved`, `CommitTaken`,
  `SelectionResult`/`StrandPick`/`FlatSelection`/`InterleavedSelection`,
  `StrandStartOffset`, `MaterializeOccurrenceEvicting`, `RewindCursor`/`RewindRing`,
  `PersistCursors`, `InsertClipFill`, `FillAiringSegments`, `NextClipItem`,
  `FillGaps`, `ExpandMaterialAsync` und alle `Expand*`-Methoden,
  `LoadAccessContextAsync`, `LoadAgeRuleAsync`, `SlotMinAge`, `Protected`,
  `Displace`, `DisplaceRuleFuture`, `Evicts`, `LoadWindowAsync` samt
  Widening-Heuristik, `ProgramScheduleWorker`-Aufrufreihenfolge.

### `AiringInvalidReason` (Enum)

- **Unverändert** — `ProgramScheduleServiceTests_InvalidReason.StageOrdering_
  IsStrictlyIncreasing` pinnt die exakte Werteliste; alle Member bleiben bestehen.
  `HeadPickInconsistent` und `RemainderNotReproducible` werden von der neuen
  Prüfung nicht mehr erzeugt (bleiben als Enum-Werte erhalten); die übrigen Werte
  werden weiter gemappt (s. Programmabläufe).

### `IProgramScheduleService` (Interface)

- **Geänderte Methoden:** keine Signaturänderung. XML-Doku von
  `EnsureCoverageAsync` (Lookback materialisiert nur noch laufende Ausstrahlungen)
  und `ReconcileAsync` (titelweise Validierung statt Reproduzierbarkeitsprüfung)
  präzisieren.

## Datenbankmigrationen

Keine. Reine Logik-Korrektur ohne Schemaänderung; alle Tabellen/Spalten sind
bereits in `VideoWebPlayerBackupData` registriert.

## Validierungsregeln

Keine. Es gibt keine neuen oder geänderten Eingaben — die Korrektur betrifft
interne Plan- und Prüflogik.

## Konfigurationsänderungen

Keine. Es entstehen auch keine neuen internen Konstanten (`UnwrittenHeadroom` ist
eine abgeleitete Größe, kein Budget wie das verworfene `MaxGrowthBypasses`).

## Seiteneffekte und Risiken

- **Gewollte Verdrängungsfreiheit bei Wachstum:** Ein neuer Film verändert
  `MixUnorderedRing`-Ordnung und ein neues Genre die Strangmenge — beides berührt
  die titelweise Prüfung nicht: Inhalt, Laufzeit, Altersfenster und Anker jedes
  Slots bleiben unverändert wahr. Die 18-fache Suffix-Verdrängung der Szenarien
  1.d/1.e entfällt vollständig.
- **Bewusste Abschwächungen gegenüber der Re-Simulation (dokumentiert, kein
  bekannter Test bricht):**
  - *Invader-Beleg innerhalb des Headrooms:* Ein Kopf, der innerhalb
    `UnwrittenHeadroom` hinter dem Occurrence-Start liegt, gilt ohne
    Belegkette als verankert (die Lage ist von einem ungeschriebenen Präfix
    nicht unterscheidbar). Folge: eine Sendezeit-Änderung, die eine neue
    Occurrence näher als `UnwrittenHeadroom` vor einen alten Kopf legt,
    verdrängt den Slot nicht — der Slot bleibt auf seiner leicht versetzten
    Position (stabil, keine Doppelbelegung; die neue Occurrence wird über den
    Atomar-Konfliktcheck/`Evicts` gelöst). Für die getesteten Fälle
    (`Reconcile_ChangedTimeOfDay_ReplansAtNewTime`: 2 h Versatz ≫ Headroom;
    `Reconcile_RuleLeftWindow`: 24 h Versatz) bleibt die Verdrängung erhalten.
  - *Zwischen-Anker in verschmolzenen Ketten:* Innerhalb einer lückenlosen
    `GroupAirings`-Kette hat nur der Gruppenkopf einen Anker-Beweis; ein mitten
    in der Kette entfallener Occurrence-Anker (z. B. gelöschte Einzel-Occurrence
    einer dichten Regel) wird toleriert statt erkannt.
  - *Skip-Nachweise ungeordneter Ringe:* Dass zwischen zwei Picks nur
    altersgesperrte Positionen lagen, wird nicht mehr verifiziert — bei
    umsortiertem Ring nicht verifizierbar.
  - *Bound-Verletzung einzelner Slots:* Ein Slot, der in ein neuerdings
    höherpriores Fenster ragt, wird nicht mehr von der Validierung verdrängt,
    sondern über die unveränderte Materialisierungs-Konfliktlogik
    (`MaterializeOccurrenceEvicting` + `Evicts`/`DisplaceRuleFuture`) gelöst —
    gleiches Endergebnis.
- **Lookback-Guard ändert Ring-Startpositionen:** Erste `EnsureCoverage`-Läufe
  mit beendeter Occurrence im 24-h-Fenster starten die Ringe jetzt am Anfang
  statt an Position k — fachlich das neue Soll, aber mehrere Bestandstests
  hatten den Konsum in ihre Erwartungen eingepreist (Liste unter „Betroffene
  bestehende Tests"). Laufende (teil-ausgestrahlte) Occurrences konsumieren
  weiterhin korrekt Cursor-Positionen — `Reconcile_PartiallyAiredOccurrence_*`
  bleibt unverändert gültig.
- **Echte Verfallsfälle bleiben erkannt:** gelöschte/entzogene Inhalte
  (`ItemNotInExpansion`), geänderte Sendezeiten/Gültigkeit (`NoOccurrenceAnchor`,
  `AnchorWithoutInvaderEvidence`), Regel-Inaktivität (`RuleInactive`),
  Dauer-Änderungen (`SlotSequenceMismatch`), Altersfenster-Verschärfungen,
  Episoden-Umnumerierung (`StrandSequenceBroken`), Clip-Entfernung — jeweils mit
  den bestehenden Wächter-Tests abgesichert (s. Tests).
- **Kein Leistungsrisiko:** Die entfernte Hypothesen-Machinerie war der teuerste
  Teil der Validierung; die titelweise Prüfung ist linear in Slotzahl ×
  Strangzahl. `Reload`/Widening-Mechanik bleibt unverändert.
- **Deployment:** Kein Einmal-Effekt beim Update — keine Datenmigration; der
  erste Reconcile-Lauf nach dem Update sieht unveränderte Pläne als gültig an.

## Umsetzungsreihenfolge

1. **Lookback-Guard in `MaterializeOccurrence`**
   - Voraussetzungen: Keine.
   - Beschreibung: Early-Return `ranges.Count == 0` zwischen Ranges-Aufbau
     (Z. 2093–2101) und Konfliktcheck (Z. 2106) einfügen, inkl. Kommentar im Stil
     der Evicting-Variante. Anschließend
     `EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning` laufen
     lassen → erwartet `[E1..E5]`.

2. **Titelweise Validierung implementieren**
   - Voraussetzungen: Keine (unabhängig von Schritt 1, aber zweite rot→grün-
     Quelle; empfohlen nach Schritt 1).
   - Beschreibung: `MatchingItemDuration`, `UnwrittenHeadroom`, `AnchorProvable`,
     `SlotStillValid` implementieren; `ReconcileWindow.Validate` auf den
     titelweisen Ablauf umstellen; Re-Simulations-Machinerie und
     `LaterGroupSlots`-Plumbing entfernen; `AiringInvalidReason`-Mapping der
     neuen Prüfpfade festlegen. Anschließend die beiden
     `Simulation_*_NoDisplacement`-Tests laufen lassen → 0 verdrängte Slots.

3. **Lookback-/Validierungsabhängige Bestandstests anpassen**
   - Voraussetzungen: Schritte 1–2 (nur mit dem neuen Verhalten sind die neuen
     Erwartungen beweisbar).
   - Beschreibung: Die unter „Betroffene bestehende Tests" gelisteten Tests an
     die neue Soll-Semantik anpassen (kein Lookback-Konsum mehr; titelweise statt
     vollreproduktiver Erhaltung); anschließend kompletter Suite-Lauf, um
     etwaige weitere implizit vom Lookback-Konsum abhängige Assertions zu
     finden — Kriterium: `EnsureCoverageAsync`-Aufruf mit einer zum
     Planungszeitpunkt beendeten Occurrence im 24-h-Fenster plus Assertions auf
     konkrete Ringpositionen/Episoden-Ids.

4. **Neuen Flachring-Regressionstest hinzufügen**
   - Voraussetzungen: Schritt 2 (für sinnvollen Grün-Nachweis; der Test deckt
     den Flachpfad ab, den die beiden Ziel-Tests nicht berühren — beide sind
     interleaved).
   - Beschreibung: In `ProgramScheduleServiceTests_PrimetimeChanges` einen Test
     im Stil von `Simulation_MovieAddedToGenre_NoDisplacement` ergänzen: Regel
     mit **nur einem Film-Genre** (kein Serien-Genre → `SetItems` geht in den
     Flachpfad, ein einziger `other`-Ring), Film am Tag 2 hinzufügen,
     `AssertNoDisplacedAsync` + `AssertPreserved`.

5. **Verifikation und Definition of Done**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: (a) Alle 16 Primetime-Tests grün — insbesondere die drei
     bisher roten. (b) Prüfpflicht nach AGENTS.md §4: jede Korrektur kurz
     zurückbauen und verifizieren, dass der zugehörige Test wieder fehlschlägt
     (Guard entfernt → `[E6,E7,E8,E1,E2]`; titelweise Prüfung durch die alte
     positionsbasierte Re-Simulation ersetzt bzw. Slot-Prädikat sabotiert →
     18 verdrängte Slots in 1.d/1.e). (c) Volle Suite
     `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün —
     insbesondere die unter „Tests" genannten Wächter. (d)
     `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei.
     Einmalig flackernde Playwright-Tests werden einzeln wiederholt und im
     Bericht genannt (AGENTS.md §7).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Simulation_MovieAddedToFlatGenreRule_NoDisplacement` | `ProgramScheduleServiceTests_PrimetimeChanges` | Regel mit nur einem Film-Genre (Flachpfad, ein `other`-Ring): Film-Wachstum am Tag 2 verdrängt nichts (`AssertNoDisplacedAsync`, `AssertPreserved`). Deckt den Flachpfad von Korrektur 2 ab, den die beiden vorhandenen Wachstums-Tests (interleaved) nicht erreichen |

Keine weiteren neuen Tests: die 16 vorhandenen Primetime-Tests sind die
verbindliche Soll-Spezifikation; bei Abweichung wird die Programmlogik korrigiert,
nicht der Test.

### Betroffene bestehende Tests

Diese Tests haben den Lookback-Konsum bzw. die vollreproduktive Erhaltung in ihre
Erwartungen eingepreist und müssen an das neue Soll angepasst werden (die
Programmlogik ist korrekt — die Tests spiegeln das alte Fehlverhalten):

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_Generation.EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` | Erwartung `episodes.Skip(2).Concat(E1)` und Cursor `episodes[0]` beruht auf Lookback-Konsum E1+E2 — neu: `episodes.Take(4)`, Cursor `episodes[3]` |
| `ProgramScheduleServiceTests_Generation.EnsureCoverage_SeriesExhausted_WrapsAround` | Erwartung `Take(3)=[E2,E1,E2]` beruht auf Lookback-Konsum `[E1,E2,E1]` — neu: `[E1,E2,E1]` |
| `ProgramScheduleServiceTests_Reconcile.Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` | `episodes.Skip(2)` an zwei Stellen (Z. 218/235) beruht auf Lookback-Konsum — neu `Skip(0)`; Verdrängungs-/Wiedereinplanungs-Mechanik unverändert |
| `ProgramScheduleServiceTests_Reconcile.Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` | Löscht `episodes[4]`s MediaItem — mit Guard ist E5 nicht mehr verplant (nur E1–E4 im Horizont); Szenario umstellen: eine tatsächlich verplante Episode löschen (z. B. `episodes[3]`) und erwartete Wiederholungssequenz anpassen (verdrängte Ausstrahlung wird ohne Sprung neu materialisiert) |
| `ProgramScheduleServiceTests_Reconcile.Reconcile_NewEpisodeInserted_ReplansInAirOrder` | Erwartet `[E3,newE,E4,E5]` — mit Guard startet die Rotation bei E1 und die heutige Ausstrahlung `[E1,E2]` bleibt titelweise gültig; neu erwartet `[E1,E2,E3,newE]` (nur die betroffene Ausstrahlung wird umgeplant — exakt die neue Soll-Semantik) |
| `ProgramScheduleServiceTests_Interleaving.EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` | Kommentar beschreibt den Lookback-Konsum („single index jump expected"); Assertions (`aired[i] > aired[i−1]`, `Count ≥ 3`) bleiben vermutlich gültig — bei Lauf verifizieren und Kommentar anpassen |

Außerdem Auditschritt (Umsetzungsschritt 3): Suite-Lauf identifiziert ggf.
weitere Tests, die implizit beendete-Lookback-Occurrence-Konsums voraussetzen.

### Unverändert grün zu haltende Wächter-Tests

Diese bestehenden Tests sichern das beizubehaltende Verhalten explizit ab und
dürfen nicht brechen:

- **Suffix-Verdrängung bei echter Änderung + sprungsfreie Wiedereinplanung:**
  `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`,
  `Reconcile_NewEpisodeInserted_ReplansInAirOrder`,
  `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip`
  (nach Anpassung), `Reconcile_PriorityRaised_EvictsLowerPriorityAiring`.
- **Occurrence-Anker / Gültigkeit:** `Reconcile_ChangedTimeOfDay_ReplansAtNewTime`,
  `Reconcile_RuleLeftWindow_DisplacesFutureAirings`,
  `Reconcile_LongAiring_StaysScheduled`, `Reconcile_LongAiring_BeyondTwoDays_
  StaysScheduled`, `Reconcile_MultiPartLongAiring_StaysScheduled`,
  `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled`.
- **Teil-ausgestrahlte Occurrences / laufende Slots:** `Reconcile_PartiallyAiredOccurrence_
  KeepsTailScheduled`, `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable`,
  `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`, alle vier
  `ProgramScheduleServiceTests_Issue315`-Tests, beide
  `ProgramScheduleServiceTests_Issue320`-Tests.
- **Altersfenster:** `Reconcile_SameWindow_KeepsAiring_TighterWindow_Displaces`,
  `Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces`,
  `Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow`,
  `Reconcile_RunningSlot_AgeWindowBlocksAtOwnStart_IsDisplaced` (Issue315).
- **Stabilität/Idempotenz:** alle `ProgramScheduleServiceTests_Stability`-Tests,
  `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp`,
  `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled`,
  `EnsureCoverage_IgnoresDisplacedSlots`.
- **Ring/Interleaving/Clip/Trailer:** `ProgramScheduleServiceTests_RingOrder`
  (inkl. `Reconcile_MixedRing_StaysReproducible`), `_Interleaving`, `_Genres`,
  `_ClipFillPeriodBounds`, `_ClipGroups`, `_ClipSource`, `_Trailer`,
  `_PriorityTakeover`, `_Conflicts`, `_CursorSelection`, `_Access`,
  `_ContentScope`, `_Diagnosis`, `_Cleanup`, `_InvalidReason`, `_Boundary`,
  `_Filler`, `_Purge`, `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`.
- **Die 13 grünen Primetime-Tests** (1.a–1.c, 1.f–1.l) — vollständige
  Regressionssuite der unveränderten Soll-Aspekte.

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung: Die Anforderung berührt keinen
Benutzerfluss — sie korrigiert interne Planlogik von `EnsureCoverageAsync`/
`ReconcileAsync`, ohne UI-, API- oder Signaturänderung. Die Soll-Spezifikation ist
explizit als Service-Tests gegen echtes SQLite und die produktiven Services ohne
Testdoublen festgeschrieben (requirement.md, Prüfgrundsätze). Der
End-to-End-Pfad „Plan generieren" ist bereits durch die bestehenden
`TVSenderAdminE2ETests` abgedeckt (u. a.
`TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`, im Bestandslauf grün) —
die Korrektur ändert an diesem Fluss nichts Sichtbares.

## Offene Punkte

Keine.
