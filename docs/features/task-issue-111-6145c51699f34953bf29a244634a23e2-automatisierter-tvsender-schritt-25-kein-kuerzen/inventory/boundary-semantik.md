# Detail: Fenstergrenze (softEnd) — Ist vs. Soll

## Ist (Schritt 22, Commit 342f9e9)

`BoundStarts` sammelt die Startzeiten aller Occurrences materialtragender Regeln.
`BoundaryAfter(boundStarts, afterUtc, priority)` liefert den ersten Bound-Start `> afterUtc`
mit `p <= priority` — eine höherpriore Folge-Occurrence begrenzt die Auswahl **nicht**;
ihr Fenster darf überzogen werden, die Kollision tötet dann aber die komplette Ausstrahlung
(`MaterializeOccurrence`: atomarer Drop) bzw. im Reconcile die überziehenden Slots
(`DisplaceAiringSuffix`).

Effekte:
- Grenzüberschreitendes Item → nicht platziert (Tail) bzw. konsumiert (Head).
- Invasor (Slot `Start < occ.Start`, `End > occ.Start`) → komplett `Displaced` → sichtbarer
  Mittendrin-Schnitt (N22).
- Legitimer Überstand gegen Folge-Occurrence → deren ganze Ausstrahlung verworfen.

## Soll (N22)

Zwei Grenzklassen, jeweils nur der **Start** eines Items wird gegen die Grenze geprüft:

| Grenze | Semantik |
|--------|----------|
| weich (Folge-Occurrence gleich-/niedrigerer Priorität) | Item darf starten, wenn `bound − itemStart ≥ MinStartRemain`; läuft dann über die Grenze hinaus zu Ende. Folge-Occurrence beginnt hinter dem Überstand. |
| hart (Folge-Occurrence höherer Priorität) | Item muss zusätzlich **vollständig** vor der Grenze enden — es kann das hochpriore Fenster nie gewinnen; statt verwerfen+verlieren wird das Item auf die nächste eigene Occurrence verschoben (kein Konsum). |

Konsequenzen:

- `consumed`-Pfad (permanentes Überspringen nicht passender Head-Items) entfällt vollständig —
  gegen die Mindest-Restlücke/harte Grenze wird das Item *verschoben*, nie still übersprungen.
- Materialisierung beider Pfade: Belegt ein Intervall (kept oder neu erzeugt) den
  Occurrence-Start (`Start < occ.Start`, `End > occ.Start`), beginnt die Auswahl/Slot-Kette
  erst an dessen `End` — begonnene Ausstrahlungen laufen aus. Überzog die Vorgänger-Ausstrahlung
  so weit, dass hinter der Grenze < MinStartRemain bleibt, entsteht keine Ausstrahlung (Füller).
- `DisplaceAiringSuffix`/Invasor-Zweig entfällt: Invasoren sind legitim, werden nicht mehr
  verdrängt; die Neue-Materialisierung shiftet stattdessen.
- `AiringReproducible(-Interleaved)`: Positionsanker `occ.Start <= airing[0].Start − skipped`
  (statt `==`), Simulation am reellen Airing-Start `airing[0].Start − skipped`, Grenze aus dem
  nominalen Occurrence-Start — sonst werden gültige, verschobene Airings endlos verdrängt.

## Mindest-Restlücke

`ProgramScheduleOptions.MinStartRemainSeconds` (Default 30): Rest vor der Grenze < Schwelle →
kein Item mehr; die Restlücke füllt `FillGaps` mit Füllern (unverändert). Verhindert den
Grenzfall „2 s Rest → 90-min-Video", ohne das gewünschte 3-min-Beispiel zu treffen.
