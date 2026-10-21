# Tasks: Planungsdiagnose + manueller Generierungsanstoß (N5)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ProgramRuleDiagnosis`-Record + `GetRuleDiagnosesAsync` in `IProgramScheduleService` deklarieren | Erledigt | `ProgramScheduleServiceTests_Diagnosis` (10 Tests) |
| 2 | Logik | `ProgramScheduleService`: `IOptions<MediaProbeOptions>`-Parameter, `SummaryCounters`-Erweiterung, `MediaItemRef`-Lookup, `MaterialMissing`-Markierungen | Erledigt | `ProgramScheduleServiceTests_Diagnosis.*`, Bestandstests `ProgramScheduleServiceTests_*` grün |
| 3 | Logik | `ProgramScheduleService.GetRuleDiagnosesAsync` + `RequireAdminAsync` implementieren | Erledigt | `Diagnosis_NonAdmin_Throws` u. a. |
| 4 | UI | `TVSenderAdmin.razor`: Diagnose-Spalte `.tvsender-rule-diagnosis` + `DiagnosisLabel` | Erledigt | E2E `Admin_CreateEditDeactivateDeleteRule` (Diagnose-Zelle) |
| 5 | UI | `TVSenderAdmin.razor`: „Jetzt generieren"-Button `#tvsender-generate-now` + `GenerateNowAsync` + Statusanzeige | Erledigt | E2E `Admin_CreateEditDeactivateDeleteRule` (Klick + Statustext) |
| 6 | Tests | `ProgramScheduleServiceTests_Diagnosis.cs` mit den 10 geplanten Testfällen | Erledigt | Lauf: 10/10 grün |
| 7 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Diagnose-Zelle + Generieren-Button erweitern | Erledigt | Einzel-Lauf 3/3 grün |
| 8 | Dokumentation | `docs/help/tv-sender.md` (Diagnose + manueller Anstoß), `docs/RELEASE_NOTES.md`, README nicht nötig (generischer Verweis vorhanden) | Erledigt | — |
