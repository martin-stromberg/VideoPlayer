# Checkpoint (Arbeitsstand — intern)

Lifecycle: Planung+Planungscommit (`f5edc7a`) erledigt. Implementierung größtenteils geschrieben:

## Erledigt im Code
- N20: `ProgramRuleRecurrence.Weekday=4`/`Weekend=5`; `BuildOccurrences`-Switch erweitert; `TVSenderAdmin` Select-Optionen + `RecurrenceLabel` („Werktags (Mo–Fr)", „Wochenende (Sa+So)"); `ProgramRule.Recurrence`-Kommentar.
- N21: `#rule-editor` in `.admin-dialog-overlay`+`.admin-dialog rule-editor-dialog` gewrapt (Overlay-Klick → `CancelEdit`, stopPropagation); `.rule-editor-dialog{width:min(960px,100%)}` in app.css; `.rule-editor-card` entfernt.
- N19 (ProgramScheduleService.cs): `SchedulableItem.StrandKey` (record-Param, default `""`); `OtherStrandKey="other"`; `StrandState` (Key/Items/NextIndex/LastPlaced/PeekAt/Commit/RewindTo); `RuleState` umgebaut (Strands, IsInterleaved, SetItems mit Strang-Gruppierung für Genre+Nicht-Filler — Episoden `show:{id}`, Rest `other`; ≤1 Strang → flat); Cursor-Maps `Dictionary<long,IReadOnlyList<ProgramRuleCursor>>` in beiden Läufen; `SelectAndCommitConsumed`+`CommitTaken`+`SelectAiringItemsInterleaved` (picks-basiert, s-- retry bei oversized head); `RewindCursor` je Strang; `PersistCursors` je Strang; `AiringReproducible` dispatch → `AiringReproducibleInterleaved` (p-Hypothesen, Simulation); `ExpandGenreAsync` setzt StrandKey; `ExpandTrailerStatesAsync` → `state.SetItems(...)`; `ProgramRuleCursor.StrandKey` (string?); Config: Composite-Unique-Index `(ProgramRuleId, StrandKey)`; Backup: `OptionalRestoreColumns` += `ProgramRuleCursors.StrandKey`.

## Offen
- Kompilierung prüfen (läuft: `dotnet build VideoWebPlayer/VideoWebPlayer.csproj -p:OutDir=<repo>/artifacts/schritt23-bin-app`).
- Migration `AddProgramRuleCursorStrandKey`: `dotnet ef migrations add AddProgramRuleCursorStrandKey -p VideoWebPlayer -c Release` (bin\\Debug gesperrt durch laufende Benutzer-App PID 58256 — NIEMALS beenden; ggf. Migration+Snapshot manuell).
- Tests schreiben: N19 (Interleaving stabil über Tage, Episodenfolge je Serie, Cursor-Fortsetzung, Reconcile-Idempotenz), N20 (Weekday/Weekend-Occurrences + Validierung), N21 (`TVSenderAdminTests.RuleEditor_RendersAsCard_InsideRulesTab` → Overlay umschreiben; `TVSenderAdminE2ETests` ~Z.178 `Assert.Equal(0, ...admin-dialog-overlay)` → Overlay sichtbar), Alt-Backup `StrandKey` (Muster `VideoWebPlayerBackupDataTests_Trailer`).
- Reviews (review.md, review-usability.md, review-code.md), test-results.md (volle Suite synchron, `-p:OutDir=<repo>/artifacts/schritt23-bin`, Baseline war 2157 grün + 1 Skip), todo.md/tasks.md aktualisieren.
- Doku: `docs/help/tv-sender.md`, `changes.log` (prüfen ob existiert), `docs/RELEASE_NOTES.md`.
- Abschluss: Feature-Verzeichnis löschen (`git rm -r`), `feat:`-Commit, Autor `Devin (Softwareschmiede Bot)`/`bot@softwareschmiede.local` + Trailer `Co-Authored-By: Devin <158243242+devin-ai-integration[bot]@users.noreply.github.com>`. Commits deutsch, WIP `step:`.
