# Tasks: TV-Sender — `ProgramSlot.Start`/`End` als UTC lesen (Issue #260)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `HasConversion` (`SpecifyKind` → `Utc` beim Lesen) auf `ProgramSlot.Start` in `ProgramSlotConfiguration` registrieren | Erledigt | `ProgramSlotTests_DateTimeKind.ReadSlot_AfterSaveAndReload_ExposesUtcKind` |
| 2 | Datenmodell | `HasConversion` auf `ProgramSlot.End` registrieren | Erledigt | `ProgramSlotTests_DateTimeKind.ReadSlot_AfterSaveAndReload_ExposesUtcKind` |
| 3 | Datenmodell | `HasConversion` auf `ProgramSlot.CreatedAt` registrieren | Erledigt | `ProgramSlotTests_DateTimeKind.ReadSlot_AfterSaveAndReload_ExposesUtcKind` |
| 4 | Tests | Testklasse `ProgramSlotTests_DateTimeKind` mit `ReadSlot_AfterSaveAndReload_ExposesUtcKind` anlegen | Erledigt | Test selbst |
| 5 | Tests | Test `ReadSlot_ToLocalTime_ConvertsInNonUtcZone` anlegen | Erledigt | Test selbst |
| 6 | Tests | Test `ReadSlot_KindAwareApis_TreatValueAsUtc` (Sabotage-diskriminierend: `DateTimeOffset`-Offset, `ToUniversalTime` Round-Trip) | Erledigt | Test selbst — rot unter `Unspecified`-Sabotage |
| 7 | Verifikation | `dotnet ef migrations has-pending-model-changes` ohne Befund; Debug- + Release-Build grün; volle Testsuite grün; Sabotageprobe des neuen Tests | Erledigt | `has-pending-model-changes` = keine Änderung; Sabotage 2/3 rot → revertiert, 3/3 grün |
