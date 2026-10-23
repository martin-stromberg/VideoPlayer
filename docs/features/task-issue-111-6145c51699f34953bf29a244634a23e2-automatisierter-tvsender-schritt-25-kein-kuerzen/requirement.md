# Übersetzte Anforderung — N22: Videos dürfen niemals gekürzt werden

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N22 (Priorität: hoch, BUG).

## Fachliche Zusammenfassung

Ein einmal eingeplantes Video muss im Sendeplan **immer vollständig** ausgestrahlt werden —
niemals gekürzt oder mittendrin durch die nächste Sendung ersetzt. Beobachteter Fehler: Gegen
Ende eines Regelfensters (Restzeit ~3 min) wird ein langes Video eingeplant und dann mittendrin
abgeschnitten. Künftig gilt explizit: **Überlauf ist erlaubt** — das letzte Item einer
Ausstrahlung darf über das Fenster-Ende (die nächste Occurrence-Grenze) hinaus auslaufen; die
Folge-Occurrence verschiebt sich entsprechend hinter das tatsächliche Item-Ende. Ergänzend gilt
eine Mindest-Restlücke: Ist vor der nächsten Fenstergrenze weniger als eine konfigurierbare
Schwelle frei (Default ~30 s), wird gar nicht erst eingeplant — die Lücke füllen Füller.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `SelectAiringItems` (~Z. 1220) und `SelectAiringItemsInterleaved` (~Z. 1153): Grenzbehandlung
    des `softEndUtc`-Parameters — statt „grenzüberschreitendes Item fällt weg" die neue Semantik
    „Item, das vor der Grenze beginnt, läuft zu Ende".
  - `BoundaryAfter`/`BoundStarts` (~Z. 1259–1277): ggf. um harte Grenze (höherpriore Folge-
    Occurrence, muss das Item vollständig passen) vs. weiche Grenze (gleich-/niedrigerprior,
    Überlauf erlaubt) unterscheiden.
  - `MaterializeOccurrence` (~Z. 1041) und `MaterializeOccurrenceEvicting` (~Z. 731): Behandlung
    eines in den Occurrence-Start hineinragenden Belegers (begonnenes Video läuft aus) —
    Materialisierung beginnt verschoben hinter dem Überstand statt die Ausstrahlung zu verwerfen
    bzw. den Invasor zu trimmen.
  - `DisplaceAiringSuffix` im Reconcile (~Z. 303): Der Invasor-Trim darf ein legitimiert
    überziehendes Video nicht mehr mitten in der Ausstrahlung verdrängen.
  - `AiringReproducible`/`AiringReproducibleInterleaved` (~Z. 501/571): muss verschobene
    Airings (reeller Start > Occurrence-Start) und überziehende letzte Items reproduzierbar
    halten, sonst Oszillation.
- `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`: neue konfigurierbare
  Mindest-Restlücke (analog `TrailerLeadDays`/`EffectiveTrailerLead`).
- Tests: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs` (Bestandstests prüfen
  bisheriges Anti-Überzugs-Verhalten → an neue Semantik anpassen) + neue Tests.
- `FfmpegLiveStreamSegmenter`/`LiveStreamCoordinator`: nur als Nachweis-Pfad relevant — der
  ffmpeg-`-t`-Clamp folgt `slot.End`; mit vollständigen Slots entfällt der Mittendrin-Stopp,
  kein Code-Eingriff nötig.

## Implementierungsansatz

1. **Diagnose (abgeschlossen, siehe inventory.md):** Kürzungs-Pfad = `ReconcileAsync`:
   `DisplaceAiringSuffix` verdrängt einen legitim überziehenden Bestandsslot mitten in der
   Ausstrahlung; in `EnsureCoverageAsync` verhindert `BoundaryAfter` den Überzug, lässt aber
   unerwünschte Fälle (ganze Ausstrahlung fällt weg bzw. Nachfolger komplett blockiert) zu.
   Ein Slot wird nirgends mit verkürztem `End` geschrieben — der „Schnitt" entsteht durch
   Verdrängen des laufenden Slots (`ProgramGuideService.GetNowAsync` liefert ihn nicht mehr).
2. Neue Auswahl-Semantik: Ein Item wird platziert, solange sein Start vor der Grenze minus
   Mindest-Restlücke liegt; es läuft immer zu Ende (Überlauf). Gegen eine harte Grenze
   (höherpriore Folge-Occurrence) muss das Item hingegen vollständig passen — sonst wird es auf
   die nächste Occurrence verschoben (kein Konsum mehr, keine stillschweigende Überspringung).
3. Shift-Semantik in beiden Materialisierungspfaden: Ragt ein bestehender Slot über den
   Occurrence-Start hinaus, beginnt die Materialisierung erst hinter dessen Ende.
4. Reproduce-Checks an die verschobenen Starts anpassen.

## Konfiguration

Neuer Schlüssel `ProgramSchedule:MinStartRemainSeconds` (o. ä.) in `ProgramScheduleOptions`,
Default 30 s, `<= 0` fällt auf Default zurück — gleiche Ebene wie `TrailerLeadDays`
(anwendungsweite Operator-Konfiguration, nicht pro Regel).

## Offene Fragen

- Keine — die Anforderung nennt Regel, Beispiel (15 min Rest + 44-min-Video läuft aus) und
  Schwellen-Empfehlung explizit.
