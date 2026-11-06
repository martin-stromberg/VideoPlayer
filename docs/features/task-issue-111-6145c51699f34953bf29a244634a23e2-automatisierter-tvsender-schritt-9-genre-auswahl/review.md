# Review — Schritt 9

## Plan-Review (vor Implementierung)

Der Plan (`plan.md`) wurde gegen `requirement.md` und das Inventar geprüft (`plan-check.md`):

- [x] N1 (Genre-Mehrfachauswahl n:m) abgedeckt: `ProgramRuleGenre`, EF-Config, Migration
  inkl. Backfill, Backup-Registrierung, UI-Mehrfachauswahl, Union-Expansion + Dedupe + Cursor.
- [x] N2 (Quell-Filter pro Regel, leer = alle) abgedeckt: `ProgramRuleMediaSource`,
  Expansion schränkt Genre-Rows **und** Inhalte ein; zusätzlich zum Access-/Unlock-Filter.
- [x] N3 (nur sichtbare kanonische Genres wählbar; Alternativnamen; Saisonfenster; Legacy-
  Zuordnungen) abgedeckt: `GenreCanonicalResolver` geteilt zwischen Optionsquelle und
  Expansion; MaterialId-Fallback für Altbestand/Alt-Backup.
- [x] Offene Frage Optionen-Endpunkt: eigene Sender-Quelle statt `api/items/genres` zu
  verändern — begründet in plan.md (andere Consumer brauchen rohe Namen).

## Ergebnis

Implementierung entspricht dem Plan ohne fachliche Abweichungen. Detaildoku in
`review-usability.md` und `review-code.md`; Testbericht in `test-results.md`.
