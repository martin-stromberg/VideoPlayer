# Bestandsaufnahme — N22: Videos niemals kürzen

## Kürzungs-Pfad (Diagnose)

Der beobachtete Mittendrin-Schnitt entsteht **nicht** durch verkürzte `ProgramSlot.End`-Werte —
kein Pfad schreibt einen Slot mit `End < Start + MediaItem.Duration`. Der Ablauf:

1. `ProgramScheduleService.EnsureCoverageAsync` (Z. 110–113) materialisiert Occurrences mit
   `BoundaryAfter(boundStarts, …)` als `softEndUtc`.
2. `SelectAiringItems` (Z. 1239–1245) lässt ein grenzüberschreitendes Item **ganz** weg
   (kein Slot). Wenn die Grenze zum Planungszeitpunkt nicht existierte (Folgeregel noch ohne
   Material, Trailer-Regel mit leerer Expansion, später neu aktivierte Regel), wird das
   überziehende Item legitim eingeplant.
3. `ReconcileAsync` → `MaterializeOccurrenceEvicting` + Caller (Z. 329–347): Ein Bestandsslot,
   der vor dem Occurrence-Start begann und darüber hinausragt („boundary invader"), wird via
   `DisplaceAiringSuffix` (Z. 303–317) komplett `Displaced` — **mitten in seiner Ausstrahlung**.
   `ProgramGuideService.GetNowAsync` (Z. 44–49) filtert `Status == Scheduled`; ab der
   Verdrängung liefert der Guide den Folgeslot → der Zuschauer sieht das Video nach (z. B.)
   3 min abbrechen. `LiveStreamCoordinator`/`FfmpegLiveStreamSegmenter` klemmen ffmpeg per
   `-t` auf `slot.End` — sie schneiden nur, was der Plan vorgibt.

Zweiter, verwandter Defekt gleicher Wurzel: In `EnsureCoverageAsync` führt der legitime Überstand
auf Kollision mit der Folge-Occurrence → deren **gesamte** Ausstrahlung wird atomar verworfen
(Z. 1067) statt hinter dem Überstand zu beginnen.

Detaildokumente:
- `inventory/boundary-semantik.md` — Schritt-22-Fenstergrenze, Prioritätslogik, Call-Sites
- `inventory/betroffene-tests.md` — Bestandstests, die alte Semantik einkodieren

## Ist-Zustand der Selektion

- `SelectAiringItems(items, headIndex, rule, occurrenceStartUtc, softEndUtc)` — statisch;
  `softEndUtc` = nächster Occurrence-Start einer gleich-/niedrigerprioren materialtragenden
  Regel (`BoundaryAfter`, Z. 1271–1277; Filter `p <= priority` in Z. 1274). Grenzfall:
  Head-Item passt nicht → `consumed++` (permanent übersprungen, Cursor rückt vor); Tail-Item
  überschreitet → `break` (Airings endet früher).
- `SelectAiringItemsInterleaved` — analog pro Strang (Z. 1187–1198).
- `BoundStarts` (Z. 1259–1265): Occurrence-Starts mit `Items.Count > 0` — Trailer-Regeln sind
  zur Laufzeit der Occurrence-Materialisierung leer → ihre Fenster begrenzen nichts.
- `MaterializeOccurrence` (Z. 1041–1092): atomare Konfliktverwerfung über `blocked` + `newSlots`.
- `MaterializeOccurrenceEvicting` (Z. 731–798): wie oben, aber liefert verlierende Bestandsslots
  zurück; Caller displaced Suffix (Invasor) bzw. Regel-Zukunft (Kompetitor).
- `AiringReproducible` (Z. 501–560) / `...Interleaved` (Z. 571–676): reproduzieren die Auswahl
  an einem Occurrence-Start; Positionsanker `occurrenceStarts.Contains(airing[0].Start - skipped)`
  verlangt **exakten** Start — ein verschobenes (shifted) Airing wäre unter der neuen Semantik
  nicht reproduzierbar → Oszillationsgefahr.
- `FillGaps` (Z. 1279–1370): Füller müssen komplett in die Lücke passen; nur am Horizont-Ende
  darf der letzte Füller überziehen — unverändert passend.
- `ProgramScheduleOptions` (`ProgramSchedule:TrailerLeadDays`, `EffectiveTrailerLead`) —
  Vorbild für die neue Mindest-Restlücke; gebunden in `ServiceCollectionExtensions` Z. 235.

## Live-Pfad (Nachweis, kein Eingriff)

`LiveStreamCoordinator.SynchronizeStreamAsync` (Z. 113–116) übergibt `now.RemainingSeconds`
(= `slot.End − now`, `ProgramGuideService` Z. 186) als `LiveStreamJob.RemainingSeconds`;
`FfmpegLiveStreamSegmenter` (Z. 297, 375) stoppt ffmpeg dort. Sobald Slots stets die volle
Item-Laufzeit tragen, spielt der Stream jedes Video vollständig.
