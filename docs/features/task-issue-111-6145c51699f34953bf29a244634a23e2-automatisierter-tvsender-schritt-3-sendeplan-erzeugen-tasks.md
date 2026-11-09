# Tasks: Sendeplan aus Regeln erzeugen (Schritt 3)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramSlot`-Entity anlegen (Start/End, weiche Referenzen `MediaItemId`/`MovieId`/`TVShowEpisodeId`/`ProgramRuleId`, `RuleType`-Snapshot) | Erledigt | — |
| 2 | Datenmodell | `ProgramRuleCursor`-Entity anlegen (`ProgramRuleId` FK+Kaskade unique, `LastContentType`, `LastContentId`) | Erledigt | — |
| 3 | Datenmodell | `ProgramCursorContentType`-Enum anlegen (Movie, TVShowEpisode, PlaylistEntry) | Erledigt | — |
| 4 | Datenmodell | `ProgramSlotConfiguration` + `ProgramRuleCursorConfiguration` anlegen (Index `(TVChannelId, Start)`, Unique `ProgramRuleId`) | Erledigt | — |
| 5 | Datenmodell | DbSets `ProgramSlots`/`ProgramRuleCursors` in `ApplicationDbContext` | Erledigt | — |
| 6 | Konfiguration | `Setup.ProgramScheduleHorizonHours` + `Setup.ProgramScheduleIntervalMinutes` (Defaults 48/15) | Erledigt | — |
| 7 | Migration | EF-Migration `AddProgramSchedule` erzeugen | Erledigt | Backup-Regressionstest, Schema-Tests |
| 8 | Backup | `ProgramSlots`/`ProgramRuleCursors` in `OptionalRestoreTables`; neue Setup-Spalten in `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` | Erledigt | `ReadFromAsync_LegacyBackupWithoutProgramScheduleTables_RestoresSuccessfully` |
| 9 | Logik | `GermanHolidayCalendar` (Computus + Landesfeiertage `DE-*`, `null` = bundesweit) | Erledigt | `GermanHolidayCalendarTests` |
| 10 | Logik | `IProgramScheduleService` + `ProgramScheduleRunSummary` anlegen | Erledigt | `ProgramScheduleServiceTests_*` |
| 11 | Logik | `ProgramScheduleService`: Material-Expansion je Materialart (Serie/Film/Filmsammlung/Genre/Playlist), verwaiste Materialien überspringen | Erledigt | `EnsureCoverage_OrphanedMaterial_ProducesNothing` u. a. |
| 12 | Logik | `ProgramScheduleService`: Freigabe-Filter (nur für alle Nicht-Admins zugängliche Inhalte; Quelle ODER Einzelfreischaltung) | Erledigt | `ProgramScheduleServiceTests_Access` |
| 13 | Logik | `ProgramScheduleService`: Laufzeit-Filter (`MediaItem.Duration != null`) | Erledigt | `EnsureCoverage_ItemWithoutDuration_IsSkipped` |
| 14 | Logik | `ProgramScheduleService`: Termin-Erzeugung je Wiederholungsmuster (Daily/Weekly/Once/Holiday), Ortszeit→UTC | Erledigt | `ProgramScheduleServiceTests_Generation` |
| 15 | Logik | `ProgramScheduleService`: Umfang (`EpisodeCount` bzw. `SlotDuration`) materialisieren | Erledigt | `EnsureCoverage_SlotDuration_FillsUntilTarget` |
| 16 | Logik | `ProgramScheduleService`: Cursor-Führung + Wrap-around | Erledigt | `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder`, `..._WrapsAround` |
| 17 | Logik | `ProgramScheduleService`: Prioritäts-Konfliktauflösung (komplette Ausstrahlung entfällt, Cursor unverändert) | Erledigt | `ProgramScheduleServiceTests_Conflicts` |
| 18 | Logik | `ProgramScheduleService`: Lückenfüllung per Füller-Regeln (Round-Robin, nur passende Inhalte) | Erledigt | `ProgramScheduleServiceTests_Filler` |
| 19 | Logik | `ProgramScheduleWorker` (`BackgroundService`: Scope-Factory, Gate, TimeProvider, Intervall je Iteration) + DI | Erledigt | `ProgramScheduleWorkerTests` |
| 20 | Logik | `ProgramSettingsService`: `GetProgramScheduleHorizonAsync`/`GetProgramScheduleIntervalAsync`/`UpdateProgramScheduleSettingsAsync` mit Min-Validierung | Erledigt | `ProgramSettingsService*Tests` |
| 21 | UI | `TVSenderAdmin.razor`: Felder `tvsender-horizon-hours`/`tvsender-interval-minutes` + Speichern | Erledigt | `TVSenderAdminE2ETests` (erweitert) |
| 22 | Tests | `ProgramScheduleTestDb`-Testfixture + Seed-Helper | Erledigt | verwendet von allen Plan-Tests |
| 23 | Tests | `GermanHolidayCalendarTests` | Erledigt | Grün |
| 24 | Tests | `ProgramScheduleServiceTests_Generation` | Erledigt | Grün |
| 25 | Tests | `ProgramScheduleServiceTests_Conflicts` | Erledigt | Grün |
| 26 | Tests | `ProgramScheduleServiceTests_Filler` | Erledigt | Grün |
| 27 | Tests | `ProgramScheduleServiceTests_Access` | Erledigt | Grün |
| 28 | Tests | `ProgramScheduleWorkerTests` | Erledigt | Grün |
| 29 | Tests | `ProgramSettingsService`-Tests für neue Settings | Erledigt | Grün |
| 30 | Tests | `VideoWebPlayerBackupDataTests`: Alt-Backup-Regressionstest | Erledigt | Grün |
| 31 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` um Settings-Speicherung erweitern | Erledigt | Grün |
| 32 | Doku | `docs/help/tv-sender.md` + `docs/RELEASE_NOTES.md` aktualisieren, README prüfen | Erledigt | — |
