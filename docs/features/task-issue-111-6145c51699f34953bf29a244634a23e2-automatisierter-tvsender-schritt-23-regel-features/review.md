# Review: Planabdeckung (Schritt 23 — N19–N21)

## Ergebnis

**Status:** Alle Planpunkte umgesetzt; zwei Bewusste-Abweichungen siehe unten.

## Abgleich gegen tasks.md

| # | Aufgabe | Status | Beleg |
|---|---------|--------|-------|
| 1–3 | N20: Enum `Weekday`/`Weekend`, `BuildOccurrences`, Select+`RecurrenceLabel` | Erledigt | `ProgramRuleRecurrence.cs`, `ProgramScheduleService.cs` (switch), `TVSenderAdmin.razor` (Optionen + Labels „Werktags (Mo–Fr)"/„Wochenende (Sa+So)") |
| 4–6 | `ProgramRuleCursor.StrandKey`, Composite-Unique-Index, Migration | Erledigt | `ProgramRuleCursor.cs`, `ProgramRuleCursorConfiguration.cs`, `Migrations/20261022190000_AddProgramRuleCursorStrandKey(.Designer).cs` + Snapshot |
| 7 | `OptionalRestoreColumns` += `ProgramRuleCursors.StrandKey` | Erledigt | `VideoWebPlayerBackupData.cs` |
| 8–12 | Stränge, Interleaved-Auswahl, Cursor je Strang, Rewind, Reconcile | Erledigt | `ProgramScheduleService.cs` (`StrandState`, `RuleState.SetItems`, `SelectAiringItemsInterleaved`, `SelectAndCommitConsumed`, `CommitTaken`, `RewindCursor`, `PersistCursors`, `AiringReproducibleInterleaved`) |
| 13–14 | Overlay-Editor + CSS | Erledigt | `TVSenderAdmin.razor` (`.admin-dialog-overlay` + `.admin-dialog.rule-editor-dialog`), `app.css` (`.rule-editor-dialog`) |
| 15–18 | Tests | Erledigt | neue `ProgramScheduleServiceTests_Interleaving` (4), Weekday/Weekend-Generation (2), Validierung (2), `VideoWebPlayerBackupDataTests_StrandKey`, bUnit `RuleEditor_RendersAsOverlayDialog`, E2E-Assertion angepasst |
| 19 | Doku | Erledigt | `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, `changes.log` |

## Bewusste Abweichungen vom Plan

1. **Editor-Block nicht physisch aus dem Tab-Pane verschoben** — das Overlay liegt weiter im
   `tab-regeln`-Abschnitt. Da `.admin-dialog-overlay` `position:fixed` ist, ist das visuell
   und funktional identisch (identisch zur Lage der Bestätigungsdialoge); das physische
   Umstellen entfiel als unnötiger Diff.
2. **Migration manuell geschrieben statt `dotnet ef`** — das EF-Tool baut vorab, und der
   Debug-Build ist durch die laufende Benutzer-App gesperrt (MSB3027). Migration +
   Designer-Datei wurden aus dem aktuellen Model-Snapshot konsistent erzeugt
   (Spalte + Index-Wechsel, `ProductVersion 10.0.12`).

## Selbstkritik / Risiken (aus der Umsetzung)

- **`AiringReproducibleInterleaved`** ist bewusst konservativ (Präfix-Hypothesen über
  zyklische Strang-Zuordnung; Stränge ohne Airing-Anteil dürfen keinen Präfix liefern).
  Unklare Fälle → Verdrängen + Neuplanung — sichere Richtung, aber potenziell mehr
  Verdrängungen als nötig. Reconcile-Idempotenz-Test belegt den Normalfall.
- **Lookback-Konsum** pro Strang entspricht exakt der bisherigen Flach-Semantik
  (ein Lookback-Occurrence konsumiert je Lauf ein weiteres Element je Strang); der
  Fortsetzungstest dokumentiert das. Kein neues Verhalten eingeführt.
- Füller-Regeln bleiben Flachmodus (`RuleType == Filler`), Genre-Stränge greifen nur für
  Occurrence-Regeln — `FillGaps` unverändert.
