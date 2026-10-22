# Tasks: Schritt 23 — Regel-Features N19–N21

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Enum | `ProgramRuleRecurrence` um `Weekday`/`Weekend` erweitern | Offen | — |
| 2 | Logik | `BuildOccurrences`: `Weekday` (Mo–Fr) + `Weekend` (Sa+So) | Offen | — |
| 3 | UI | Recurrence-Select + `RecurrenceLabel` in `TVSenderAdmin` ergänzen | Offen | — |
| 4 | Datenmodell | `ProgramRuleCursor.StrandKey` (string?) + Kommentar | Offen | — |
| 5 | EF | `ProgramRuleCursorConfiguration`: Composite-Unique-Index `(ProgramRuleId, StrandKey)` | Offen | — |
| 6 | EF | Migration `AddProgramRuleCursorStrandKey` + Model-Snapshot | Offen | — |
| 7 | Backup | `OptionalRestoreColumns` += `ProgramRuleCursors.StrandKey` | Offen | — |
| 8 | Logik | `SchedulableItem.StrandKey` + Setzen in `ExpandGenreAsync` (`show:{id}` / `other`) | Offen | — |
| 9 | Logik | `StrandState` + `RuleState`-Umbau (Stränge, `IsInterleaved`, Cursor je Strang) | Offen | — |
| 10 | Logik | `SelectAiringItemsInterleaved` + Einbindung in `MaterializeOccurrence(Evicting)` | Offen | — |
| 11 | Logik | `PersistCursors`/`NextIndexFor`/`RewindCursor` je Strang | Offen | — |
| 12 | Logik | `AiringReproducible` Interleaved-Pfad | Offen | — |
| 13 | UI | Regel-Editor als `admin-dialog-overlay`/`admin-dialog` + `CancelEdit` auf Overlay-Klick | Offen | — |
| 14 | UI | `.rule-editor-dialog` CSS; `.rule-editor-card` entfernen | Offen | — |
| 15 | Tests | N19-Tests: Interleaving, stabile Position, Episodenfolge, Cursor-Fortsetzung, Reconcile-Idempotenz/Rewind | Offen | — |
| 16 | Tests | N20-Tests: `BuildOccurrences` Mo–Fr/Sa+So + Validierung | Offen | — |
| 17 | Tests | N21: `TVSenderAdminTests` Overlay-Umbau + E2E-Assertion anpassen | Offen | — |
| 18 | Tests | Alt-Backup-Test `StrandKey` | Offen | — |
| 19 | Doku | `docs/help/tv-sender.md`, `changes.log`, `docs/RELEASE_NOTES.md` | Offen | — |
