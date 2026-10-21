# Tasks: Sendeplan aus Regeln erzeugen (Schritt 3)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramSlot`-Entity anlegen (Start/End, weiche Referenzen `MediaItemId`/`MovieId`/`TVShowEpisodeId`/`ProgramRuleId`, `RuleType`-Snapshot) | Offen | — |
| 2 | Datenmodell | `ProgramRuleCursor`-Entity anlegen (`ProgramRuleId` FK+Kaskade unique, `LastContentType`, `LastContentId`) | Offen | — |
| 3 | Datenmodell | `ProgramCursorContentType`-Enum anlegen (Movie, TVShowEpisode, PlaylistEntry) | Offen | — |
| 4 | Datenmodell | `ProgramSlotConfiguration` + `ProgramRuleCursorConfiguration` anlegen (Index `(TVChannelId, Start)`, Unique `ProgramRuleId`) | Offen | — |
| 5 | Datenmodell | DbSets `ProgramSlots`/`ProgramRuleCursors` in `ApplicationDbContext` | Offen | — |
| 6 | Konfiguration | `Setup.ProgramScheduleHorizonHours` + `Setup.ProgramScheduleIntervalMinutes` (Defaults 48/15) | Offen | — |
| 7 | Migration | EF-Migration `AddProgramSchedule` erzeugen | Offen | Backup-Regressionstest, Schema-Tests |
| 8 | Backup | `ProgramSlots`/`ProgramRuleCursors` in `OptionalRestoreTables`; neue Setup-Spalten in `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` | Offen | `ReadFromAsync_LegacyBackupWithoutProgramScheduleTables_RestoresSuccessfully` |
| 9 | Logik | `GermanHolidayCalendar` (Computus + Landesfeiertage `DE-*`, `null` = bundesweit) | Offen | `GermanHolidayCalendarTests` |
| 10 | Logik | `IProgramScheduleService` + `ProgramScheduleRunSummary` anlegen | Offen | `ProgramScheduleServiceTests_*` |
| 11 | Logik | `ProgramScheduleService`: Material-Expansion je Materialart (Serie/Film/Filmsammlung/Genre/Playlist), verwaiste Materialien überspringen | Offen | `EnsureCoverage_OrphanedMaterial_ProducesNothing` u. a. |
| 12 | Logik | `ProgramScheduleService`: Freigabe-Filter (nur für alle Nicht-Admins zugängliche Inhalte; Quelle ODER Einzelfreischaltung) | Offen | `ProgramScheduleServiceTests_Access` |
| 13 | Logik | `ProgramScheduleService`: Laufzeit-Filter (`MediaItem.Duration != null`) | Offen | `EnsureCoverage_ItemWithoutDuration_IsSkipped` |
| 14 | Logik | `ProgramScheduleService`: Termin-Erzeugung je Wiederholungsmuster (Daily/Weekly/Once/Holiday), Ortszeit→UTC | Offen | `ProgramScheduleServiceTests_Generation` |
| 15 | Logik | `ProgramScheduleService`: Umfang (`EpisodeCount` bzw. `SlotDuration`) materialisieren | Offen | `EnsureCoverage_SlotDuration_FillsUntilTarget` |
| 16 | Logik | `ProgramScheduleService`: Cursor-Führung + Wrap-around | Offen | `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder`, `..._WrapsAround` |
| 17 | Logik | `ProgramScheduleService`: Prioritäts-Konfliktauflösung (komplette Ausstrahlung entfällt, Cursor unverändert) | Offen | `ProgramScheduleServiceTests_Conflicts` |
| 18 | Logik | `ProgramScheduleService`: Lückenfüllung per Füller-Regeln (Round-Robin, nur passende Inhalte) | Offen | `ProgramScheduleServiceTests_Filler` |
| 19 | Logik | `ProgramScheduleWorker` (`BackgroundService`: Scope-Factory, Gate, TimeProvider, Intervall je Iteration) + DI | Offen | `ProgramScheduleWorkerTests` |
| 20 | Logik | `ProgramSettingsService`: `GetProgramScheduleHorizonAsync`/`GetProgramScheduleIntervalAsync`/`UpdateProgramScheduleSettingsAsync` mit Min-Validierung | Offen | `ProgramSettingsService*Tests` |
| 21 | UI | `TVSenderAdmin.razor`: Felder `tvsender-horizon-hours`/`tvsender-interval-minutes` + Speichern | Offen | `TVSenderAdminE2ETests` (erweitert) |
| 22 | Tests | `ProgramScheduleTestDb`-Testfixture + Seed-Helper | Offen | verwendet von allen Plan-Tests |
| 23 | Tests | `GermanHolidayCalendarTests` | Offen | Grün |
| 24 | Tests | `ProgramScheduleServiceTests_Generation` | Offen | Grün |
| 25 | Tests | `ProgramScheduleServiceTests_Conflicts` | Offen | Grün |
| 26 | Tests | `ProgramScheduleServiceTests_Filler` | Offen | Grün |
| 27 | Tests | `ProgramScheduleServiceTests_Access` | Offen | Grün |
| 28 | Tests | `ProgramScheduleWorkerTests` | Offen | Grün |
| 29 | Tests | `ProgramSettingsService`-Tests für neue Settings | Offen | Grün |
| 30 | Tests | `VideoWebPlayerBackupDataTests`: Alt-Backup-Regressionstest | Offen | Grün |
| 31 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Settings-Speicherung erweitern | Offen | Grün |
| 32 | Doku | `docs/help/tv-sender.md` + `docs/RELEASE_NOTES.md` aktualisieren, README prüfen | Offen | — |
