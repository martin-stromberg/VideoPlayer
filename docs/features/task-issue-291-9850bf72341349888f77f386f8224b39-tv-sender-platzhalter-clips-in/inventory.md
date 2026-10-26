# Bestandsaufnahme: Platzhalter-Clips innerhalb der Sendeperiode verteilen und Zeitüberlappungen vermeiden (#291)

Analysiert wurde der Clip-Füll-Pass der Sendeplan-Generierung (`ProgramScheduleService`), der mit #277 eingeführte Verteilung von Platzhalter-Clips auf die Segmente einer `SlotDuration`-Ausstrahlung.

## Zusammenfassung

- `EnsureCoverageAsync` und `ReconcileAsync` materialisieren Occurrences (Titel) und rufen danach `InsertClipFill` → `FillAiringSegments` auf, das die Restzeit der Periode gleichmäßig auf die n+1 Segmente verteilt und die Titel hinter die Clips neu verlegt.
- **Defekt A (Periodenende):** `FillAiringSegments` berechnet `periodEnd` aus `airing[0].Start` — dem realen/geschriebenen Kopf der Ausstrahlung, nicht dem nominellen Occurrence-Start. Bei Teil-Ausstrahlungen (Lauf mitten in der Periode, Lookback-Materialisierung) und invader-verschobenen Starts liegt das Periodenende rechnerisch zu spät → Clips werden Stunden nach dem echten Periodenende eingeplant. Reproduziert: Lauf um 22:30 → 33+ Clips mit Start nach 00:15.
- **Defekt B (Überlappung):** Neu verlegte Titel werden ohne jede Belegt-/Reserviert-Prüfung positioniert; `tailLimit` begrenzt nur Clip-Slots. Reproduziert: Titel 21:19–23:05 überlappt belegten Slot 23:00–23:30 einer höherprioren Regel.
- **Defekt C (Tail-Clip):** Der erste Tail-Clip prüft `pos + Duration <= periodEnd` statt `tailLimit` → kann belegten Raum überdecken.
- `GroupAirings` gruppiert lückenlos verkettete Same-Rule-Slots; Occurrence-Starts sind in `EnsureCoverageAsync` (`occurrences`) und `ReconcileWindow` (`window.Occurrences`) verfügbar, werden aber nicht an den Füll-Pass weitergereicht.
- Test-Ausgangszustand: Baseline grün (2248/2249, 1 übersprungen), siehe [Tests](inventory/tests.md). Die neuen Reproduktionstests schlagen erwartungsgemäß fehl.

## Details

- [Logik](inventory/logic.md)
- [Tests](inventory/tests.md)
