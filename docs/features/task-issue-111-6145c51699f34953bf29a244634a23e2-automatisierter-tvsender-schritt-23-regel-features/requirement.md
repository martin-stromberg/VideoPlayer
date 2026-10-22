# Übersetzte Anforderung: Schritt 23 — Regel-Features N19–N21

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N19–§N21.

## Fachliche Zusammenfassung

Drei Nachbesserungen am automatischen TV-Sender:

1. **N19 — Serien-Interleaving im Genre-Zeitfenster:** Eine Regel der Art `GenreRotation`
   (Material `ProgramRuleMaterialType.Genre`) soll ihr Zeitfenster nicht mehr mit Episoden
   EINER Serie hintereinander füllen (Stripping), sondern echtes TV-Verhalten abbilden:
   mehrere Serien pro Tag, jede eine Folge zur stabilen Uhrzeit. Umsetzung laut Vorgabe:
   je Serie ein eigener Fortschritts-Cursor, Fenster-Füllung round-robin über die Serien,
   Position über Tage stabil, Episodenreihenfolge pro Serie fortlaufend.
2. **N20 — Wiederholungsarten „Werktag" und „Wochenende":** `ProgramRuleRecurrence` wird um
   `Weekday` (Mo–Fr) und `Weekend` (Sa+So) erweitert — Occurrence-Berechnung
   (`BuildOccurrences`), Auswahl im Regel-Editor (`TVSenderAdmin`), serverseitige
   Validierung (`ProgramRuleService`), Beschriftungen, Tests.
3. **N21 — Regel-Editor als Overlay-Panel:** Der Regel-Editor in `TVSenderAdmin.razor` wird
   von der einklappbaren Card (Schritt-16-Umsetzung, `#rule-editor`/`rule-editor-card`)
   in ein Overlay-Panel über der Regel-Liste umgewandelt — Focus-/Background-Verhalten nach
   App-Konvention (`.admin-dialog-overlay`/`.admin-dialog` wie die Bestätigungsdialoge).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs` — `RuleState`, `SelectAiringItems`,
  `MaterializeOccurrence`/`MaterializeOccurrenceEvicting`, `NextIndexFor`, `PersistCursors`,
  `RewindCursor`, `AiringReproducible`, `BuildOccurrences`, `ExpandGenreAsync`,
  `SchedulableItem` (Serien-Interleaving + neue Recurrence-Arten).
- `VideoWebPlayer/Data/ProgramRuleRecurrence.cs` — neue Werte `Weekday`, `Weekend`.
- `VideoWebPlayer/Data/ProgramRuleCursor.cs` +
  `Data/Configurations/ProgramRuleCursorConfiguration.cs` — Strang-Schlüssel für
  Serien-Cursor (neue Spalte → EF-Migration).
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` —
  `OptionalRestoreColumns`-Eintrag für die neue Spalte + Alt-Backup-Regressionstest.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Recurrence-Auswahl,
  `RecurrenceLabel`, Regel-Editor als Overlay.
- `VideoWebPlayer/wwwroot/app.css` — Editor-Overlay-Styling (Breite für großes Panel).
- Tests: `ProgramScheduleServiceTests_*` (Interleaving, Werktag/Wochenende),
  `ProgramRuleServiceTests_Validation`, `TVSenderAdminTests` (Overlay), ggf.
  `TVSenderAdminE2ETests`, `VideoWebPlayerBackupDataTests_*`.
- Dokumentation: `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, `changes.log`.

## Implementierungsansatz

- N19: `SchedulableItem` erhält eine Strang-Zugehörigkeit (Serien-Id für Episoden, gemeinsamer
  Schlüssel für sonstige Inhalte). `RuleState` hält statt eines Rings mehrere Stränge mit
  eigenem `NextIndex`; die Auswahl rotiert pro Ausstrahlung über die Stränge. Persistenz je
  Strang über `ProgramRuleCursor` + neue Spalte `StrandKey` (NULL = bisheriger Einzelcursor —
  abwärtskompatibel). Nicht-Genre-Material und Füller-Logik bleiben unverändert.
- N20: Enum-Werte anhängen (keine bestehenden Werte ändern — persistiert als int),
  `BuildOccurrences`-Switch erweitern, Editor-Select + `RecurrenceLabel` ergänzen.
- N21: `#rule-editor`-Block in `.admin-dialog-overlay`/`.admin-dialog`-Struktur verpacken,
  Overlay-Klick schließt (Abbrechen-Verhalten wie Bestätigungsdialoge), bestehende
  bUnit-/E2E-Tests anpassen.

## Konfiguration

Keine neuen Konfigurationseinträge.

## Offene Fragen

Keine — Umsetzungsweg ist in der Anforderung selbst vorgegeben (N19: Strang-Cursor +
round-robin; N20: enum-Erweiterung; N21: App-Dialogkonvention).
