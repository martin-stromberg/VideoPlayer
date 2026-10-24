# Tasks: TV-Sender — `ProgramSlot.Start`/`End` als UTC lesen (Issue #260)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `HasConversion` (`SpecifyKind` → `Utc` beim Lesen) auf `ProgramSlot.Start` in `ProgramSlotConfiguration` registrieren | Offen | — |
| 2 | Datenmodell | `HasConversion` auf `ProgramSlot.End` registrieren | Offen | — |
| 3 | Datenmodell | `HasConversion` auf `ProgramSlot.CreatedAt` registrieren | Offen | — |
| 4 | Tests | Testklasse `ProgramSlotDateTimeKindTests` mit `ReadBack_StartEndCreatedAt_HaveUtcKind` anlegen | Offen | — |
| 5 | Tests | Test `ReadBack_Start_ToLocalTime_ConvertsInNonUtcZone` anlegen | Offen | — |
| 6 | Verifikation | `dotnet ef migrations has-pending-model-changes` ohne Befund; Debug- + Release-Build grün; volle Testsuite grün; Sabotageprobe des neuen Tests | Offen | — |
