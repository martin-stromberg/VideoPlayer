# Tasks: Sender und Programmregeln verwalten (Schritt 2)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enums `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType` anlegen | Offen | — |
| 2 | Datenmodell | Entity `TVChannel` anlegen | Offen | — |
| 3 | Datenmodell | Entity `ProgramRule` anlegen | Offen | — |
| 4 | Datenmodell | `TVChannelConfiguration` anlegen | Offen | — |
| 5 | Datenmodell | `ProgramRuleConfiguration` anlegen (FK-Kaskade, Unique `(TVChannelId,Name)`) | Offen | — |
| 6 | Datenmodell | DbSets `TVChannels`/`ProgramRules` in `ApplicationDbContext` | Offen | — |
| 7 | Konfiguration | `Setup.HolidayFederalState` + `ProgramSettingsService.Get/UpdateHolidayFederalStateAsync` | Offen | — |
| 8 | Migration | EF-Migration `AddTVChannelsAndProgramRules` erzeugen | Offen | — |
| 9 | Backup | `TVChannels`/`ProgramRules` in `OptionalRestoreTables`, `Setups.HolidayFederalState` in `OptionalRestoreColumns` | Offen | — |
| 10 | Logik | `ProgramRuleInput`-Record anlegen | Offen | — |
| 11 | Logik | `IProgramRuleService`/`ProgramRuleService` anlegen (CRUD, Channel, Admin-Erzwingung, Validierung) | Offen | — |
| 12 | Logik | DI-Registrierung `IProgramRuleService` | Offen | — |
| 13 | UI | `MediaSearchSelector.AllowedMediaTypes`-Parameter | Offen | — |
| 14 | UI | `TVSenderAdmin.razor` (Sender, Bundesland, Regel-CRUD, Auswahldialoge) | Offen | — |
| 15 | UI | `AdminIndex`-Kachel „TV-Sender" | Offen | — |
| 16 | Tests | `ProgramRuleServiceTests_Crud` | Offen | — |
| 17 | Tests | `ProgramRuleServiceTests_Channel` | Offen | — |
| 18 | Tests | `ProgramRuleServiceTests_Authorization` | Offen | — |
| 19 | Tests | `ProgramRuleServiceTests_Validation` | Offen | — |
| 20 | Tests | `ProgramSettingsServiceTests` um `HolidayFederalState` erweitern | Offen | — |
| 21 | Tests | `VideoWebPlayerBackupDataTests` Alt-Backup-Regressionstest | Offen | — |
| 22 | Tests | `MediaSearchSelectorTests` um `AllowedMediaTypes` erweitern | Offen | — |
| 23 | Tests | `PlaylistsE2ETestBase.DriveUntilStateAsync` protected machen | Offen | — |
| 24 | E2E-Tests | `TVSenderAdminE2ETests` (CRUD-Fluss + Nicht-Admin) | Offen | — |
