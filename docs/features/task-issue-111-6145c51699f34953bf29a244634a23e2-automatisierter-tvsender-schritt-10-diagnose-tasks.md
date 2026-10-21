# Tasks: Planungsdiagnose + manueller Generierungsanstoß (N5)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ProgramRuleDiagnosis`-Record + `GetRuleDiagnosesAsync` in `IProgramScheduleService` deklarieren | Offen | — |
| 2 | Logik | `ProgramScheduleService`: `IOptions<MediaProbeOptions>`-Parameter, `SummaryCounters`-Erweiterung, `MediaItemRef`-Lookup, `MaterialMissing`-Markierungen | Offen | — |
| 3 | Logik | `ProgramScheduleService.GetRuleDiagnosesAsync` + `RequireAdminAsync` implementieren | Offen | — |
| 4 | UI | `TVSenderAdmin.razor`: Diagnose-Spalte `.tvsender-rule-diagnosis` + `DiagnosisLabel` | Offen | — |
| 5 | UI | `TVSenderAdmin.razor`: „Jetzt generieren"-Button `#tvsender-generate-now` + `GenerateNowAsync` + Statusanzeige | Offen | — |
| 6 | Tests | `ProgramScheduleServiceTests_Diagnosis.cs` mit den 10 geplanten Testfällen | Offen | — |
| 7 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Diagnose-Zelle + Generieren-Button erweitern | Offen | — |
| 8 | Dokumentation | `docs/help/tv-sender.md` (Diagnose + manueller Anstoß), `docs/RELEASE_NOTES.md`, ggf. `README.md` | Offen | — |
