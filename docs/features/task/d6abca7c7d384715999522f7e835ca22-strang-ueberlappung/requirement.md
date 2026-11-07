# Übersetzte Anforderung: Überlappungsfreie Strang-Rotation bei Interleaved-Ausstrahlungen

Quelle: `issue.md` → „Nachtrag: Folge-Korrekturanforderung — Wiederholung an aufeinanderfolgenden Tagen" (GitHub-Issue #343, Folgefehler zu #341/#342).

## Fachliche Zusammenfassung

Die mit Issue #341 eingeführte bedarfsgesteuerte Startstrang-Rotation in `ProgramScheduleService.SelectAiringItemsInterleaved` rotiert den Startstrang nur um +1 Strang pro Ausstrahlungstag (`StrandStartOffset` = UTC-Tageszähler mod Strangzahl). Da eine `SlotDuration`-Ausstrahlung typischerweise zwei bis drei Stränge bedient, überlappen sich die Bedienfenster benachbarter Tage: Ein-Film-Sammlungen laufen an zwei bis drei aufeinanderfolgenden Tagen. Der Rotationszeiger muss die tatsächlich bediente Strangzahl je Ausstrahlung berücksichtigen und hinter dem zuletzt bedienten Strang fortgesetzt werden, sodass die Bedient-Mengen aufeinanderfolgender Ausstrahlungen disjunkt sind, solange noch unbediente Stränge existieren.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine geplante Ausstrahlung (Occurrence) einer Genre-Regel mit mehreren Strängen (`collection:{id}`, `show:{id}`, `other`) im interleaved Belegungsmodus — konkret der Satz der Stränge, die eine Ausstrahlung bedient, im Verhältnis zum Vorgänger-Fenster.
- **Auslösende Ereignisse:** Automatische Plangenerierung/-fortschreibung — `EnsureCoverageAsync` (Horizon-Erweiterung, MaterializeOccurrence) und `ReconcileAsync` (MaterializeOccurrenceEvicting) bei jeder neu eingeplanten Ausstrahlung. Kein Nutzereingriff.
- **Erwartete Wirkung:** Der Sendeplan zeigt über die Tage hinweg abwechselnde Filmsammlungen/Serien: kein Strang/Titel erscheint an zwei aufeinanderfolgenden Ausstrahlungen, solange `strandCount > picksPerAiring`. Bereits falsch belegte künftige Tage werden bei der nächsten Re-Bewertung (Reconcile) korrigiert.
- **Umgang mit fehlenden Objekten:**
  - Keine vorhandenen Slots der Regel (Erstplanung): Zeiger beginnt bei Strang 0 der `StrandOrderKey`-Ordnung (deterministisch) — erzeugen eines Startwerts entfällt.
  - Strang des letzten Vorgänger-Picks nicht mehr auflösbar (Material entfernt/umbenannt): Vorgänger-Slot wird übersprungen, nächster älterer auflösbarer Slot bestimmt den Zeiger; ohne jeden auflösbaren Vorgänger-Slot gilt Start 0.
  - `ProgramRuleCursors`-Zeilen: wiederverwenden — die Fortschreibung je Strang bleibt unverändert; kein neuer persistierter Zeiger (der Zeiger wird aus den tatsächlich geplanten Slots abgeleitet und braucht kein eigenes Persistenzobjekt).
- **Scope-Ausschlüsse:** Keine.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs` — einzige geänderte Datei:
  - `SelectAiringItemsInterleaved` — Startstrang-Offset des Rotationslaufs (Probelauf/Gegenlauf bleibt).
  - `StrandStartOffset` — bleibt für den `EpisodeCount`-Pfad (`wanted < m`) unverändert.
  - `SelectItems`, `MaterializeOccurrence`, `MaterializeOccurrenceEvicting` — Übergabe des Rotations-Startindex.
  - `RuleState`/`StrandState`, `FindItemIndex` — Auflösung Slot → Strang-Index (Vorgänger-Pick).
  - `persistedOccupancy` (EnsureCoverage) — benötigt Inhalts-Referenzen des Slots (MovieId/EpisodeId/MediaItemId, IsClipFill).
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_StrandRotation.cs` — neuer Pflichttest (Bedient-Überschneidung aufeinanderfolgender Tage leer bei `strandCount > picksPerDay`) sowie Regressionstest für die Fortsetzung über einen Planlauf hinweg (zweiter `EnsureCoverage`-Lauf).
- Dokumentation: `docs/help/tv-sender-ablauf-technisch.md` (Abschnitt Strang-Rotation), `docs/RELEASE_NOTES.md`, `changes.log`.

## Implementierungsansatz

- Der Rotationszeiger wird aus dem tatsächlich letzten Titel-Slot der Regel vor dem aktuellen `airingStartUtc` abgeleitet: Der Slot mit maximalem `Start < airingStartUtc`, dessen Inhalt sich über `FindItemIndex` einem Strang zuordnen lässt, bestimmt den Fortsetzungsindex `strandIndex + 1 (mod m)`. Grundlage sind persistierte Slots (`persistedOccupancy` in `EnsureCoverageAsync`, `keptSlots` in `ReconcileAsync`) plus die in diesem Lauf neu erzeugten `newSlots` — damit gilt die Fortsetzung sowohl innerhalb eines Planlaufs als auch über Lauf- und Prozessgrenzen hinweg, ohne neue Persistenz.
- Der Probelauf-Mechanismus bleibt: Bedient der Probelauf (Offset 0) alle nicht-leeren Stränge, bleibt die Tagesstruktur stabil (all-served-Fenster); sonst läuft die Auswahl mit dem fortgeschriebenen Zeiger.
- `EpisodeCount`-Regeln (`wanted < m`) behalten die bestehende Tagesrotation über `StrandStartOffset` — unverändert.
- Ordnung innerhalb geordneter Stränge, `ProgramRuleCursors`-Fortschreibung (`CommitTaken`/`CommitScan`) und die titelweise `StrandSequenceBroken`-Validierung werden nicht angetastet.

## Konfiguration

Keine — das Verhalten ist fest verdrahtet (Invariante, kein Schalter).

## Offene Fragen

Keine — alle Punkte sind aus Anforderung und Codebestand ableitbar (siehe Planung, Abschnitt „Offene Punkte (unbeantwortet)" für die dokumentierten Annahmen).
