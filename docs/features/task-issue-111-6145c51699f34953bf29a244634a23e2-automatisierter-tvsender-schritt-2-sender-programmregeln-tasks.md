# Tasks: Sender und Programmregeln verwalten (Schritt 2)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enums `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType` anlegen | Erledigt | Service-Validierungstests |
| 2 | Datenmodell | Entity `TVChannel` anlegen | Erledigt | `ProgramRuleServiceTests_Channel` |
| 3 | Datenmodell | Entity `ProgramRule` anlegen | Erledigt | `ProgramRuleServiceTests_Crud` |
| 4 | Datenmodell | `TVChannelConfiguration` anlegen | Erledigt | Channel-Tests |
| 5 | Datenmodell | `ProgramRuleConfiguration` anlegen (FK-Kaskade, Unique `(TVChannelId,Name)`) | Erledigt | `DeleteChannel_CascadesRules`, `CreateRule_DuplicateNameSameChannel_Fails` |
| 6 | Datenmodell | DbSets `TVChannels`/`ProgramRules` in `ApplicationDbContext` | Erledigt | alle Service-Tests |
| 7 | Konfiguration | `Setup.HolidayFederalState` + `ProgramSettingsService.Get/UpdateHolidayFederalStateAsync` | Erledigt | `ProgramSettingsServiceHolidayFederalStateTests` |
| 8 | Migration | EF-Migration `AddTVChannelsAndProgramRules` erzeugen | Erledigt | Backup-Regressionstest, Schema-Tests |
| 9 | Backup | `TVChannels`/`ProgramRules` in `OptionalRestoreTables`, `Setups.HolidayFederalState` in `OptionalRestoreColumns` | Erledigt | `ReadFromAsync_LegacyBackupWithoutTVChannelTablesAndHolidayFederalState_RestoresSuccessfully` |
| 10 | Logik | `ProgramRuleInput`-Record anlegen | Erledigt | Service-Tests |
| 11 | Logik | `IProgramRuleService`/`ProgramRuleService` anlegen (CRUD, Channel, Admin-Erzwingung, Validierung) | Erledigt | `ProgramRuleServiceTests_*` |
| 12 | Logik | DI-Registrierung `IProgramRuleService` | Erledigt | E2E-Tests (Seite funktionsfähig) |
| 13 | UI | `MediaSearchSelector.AllowedMediaTypes`-Parameter | Erledigt | `AllowedMediaTypes_FiltersResultsToPermittedTypes` |
| 14 | UI | `TVSenderAdmin.razor` (Sender, Bundesland, Regel-CRUD, Auswahldialoge) | Erledigt | `TVSenderAdminE2ETests` |
| 15 | UI | `AdminIndex`-Kachel „TV-Sender" | Erledigt | E2E (Navigation erreichbar) |
| 16 | Tests | `ProgramRuleServiceTests_Crud` | Erledigt | Grün |
| 17 | Tests | `ProgramRuleServiceTests_Channel` | Erledigt | Grün |
| 18 | Tests | `ProgramRuleServiceTests_Authorization` | Erledigt | Grün |
| 19 | Tests | `ProgramRuleServiceTests_Validation` | Erledigt | Grün |
| 20 | Tests | `ProgramSettingsServiceTests` um `HolidayFederalState` erweitern | Erledigt | Grün |
| 21 | Tests | `VideoWebPlayerBackupDataTests` Alt-Backup-Regressionstest | Erledigt | Grün |
| 22 | Tests | `MediaSearchSelectorTests` um `AllowedMediaTypes` erweitern | Erledigt | Grün |
| 23 | Tests | `PlaylistsE2ETestBase.DriveUntilStateAsync` protected machen | Erledigt | Grün |
| 24 | E2E-Tests | `TVSenderAdminE2ETests` (CRUD-Fluss + Nicht-Admin) | Erledigt | Grün (nach `@using`-Fix) |
