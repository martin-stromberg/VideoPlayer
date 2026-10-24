# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- `ProgramRuleDiagnosis`-Record + `GetRuleDiagnosesAsync` in `IProgramScheduleService.cs`
  (mit XML-Doku) — vorhanden.
- `ProgramScheduleService`: `IOptions<MediaProbeOptions>`-Parameter (optional, Default 10),
  `SummaryCounters` erweitert (`Candidates`, `MaterialMissing`, `NoMediaItem`,
  `ProbePending`, `ProbeExhausted`), `MediaItemRef`-Lookup inkl. Probezustand mit
  Bevorzugung laufzeitbehafteter Items (bisherige `MediaItemId`-Reihenfolge erhalten),
  `MaterialMissing`-Markierungen an allen relevanten Early-Returns (Show/Movie/Collection/
  Playlist/Genre inkl. leerer kanonischer Namen und leerer wirksamer Genre-Ids),
  `GetRuleDiagnosesAsync` + `RequireAdminAsync` — vorhanden.
- UI: `Planbar`-Spalte mit `.tvsender-rule-diagnosis` + `DiagnosisLabel`,
  `#tvsender-generate-now`-Button + `GenerateNowAsync` + `#tvsender-schedule-status`,
  `ReloadDiagnosesAsync` in `ReloadRulesAsync` verkettet — vorhanden.
- Tests: `ProgramScheduleServiceTests_Diagnosis.cs` mit 10 Fällen (alle grün).
- E2E: `Admin_CreateEditDeactivateDeleteRule` um Diagnose-Zelle + Generieren-Button
  erweitert (im konsolidierten Lauf, Einzel-Verification grün).
- Doku: `docs/help/tv-sender.md` (Abschnitt „Planungsdiagnose und manueller Anstoß"),
  `docs/RELEASE_NOTES.md` (What's-New-Eintrag). README nennt den Sender bereits generisch —
  keine Erweiterung nötig.

## Offene Aufgaben

— (Status `Vollständig umgesetzt`)

## Hinweise

- Zusätzlich zum Plan bereinigt: die vorher bereits ungenutzte private Methode
  `TryEpisodeItem` wurde entfernt (sie musste für die geänderte Lookup-Signatur ohnehin
  angefasst werden); `GenerateNowAsync` fängt Fehler wie die übrigen Handler in
  `scheduleStatus` ab.
