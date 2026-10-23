# Plan-Review — Schritt 27 (N26–N28)

**Status: Vollständig umgesetzt**

| Planelement | Umsetzung |
|-------------|-----------|
| N26: invariante Prozent-Formatierung | `VideoWebPlayer/ViewModels/CssFormat.cs` (`Percent`), eingebunden in `LiveTv.razor` (progress-fill) und `VideoPlayer.razor` (player-progress-fill — gleicher latenter Bug). int-basierte `width:`-Stellen in `TVSenderAdmin.razor` unberührt. |
| N26: Format-Test | `LiveTvTests.Now_ProgressFill_WritesInvariantPercent_UnderGermanCulture` (bUnit, de-DE) + `CssFormatTests` (direkter Format-Assert). |
| N27: Tages-Header + Regelwechsel-Divider | `TVSenderAdmin.razor` Sendeplan: `GroupBy(s => s.Start.ToLocalTime().Date)` mit `.tvsender-day-header` (`data-day`), `.tvsender-rule-divider` (`data-rule-id`, Regelname) bei `ProgramRuleId`-Wechsel, kein Divider am Tagesanfang. Styles in `app.css`. |
| N27: bUnit-Test | `TVSenderAdminTests.Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges`. |
| N28: Tabelle TvSenderUserAccess | `Data/TvSenderUserAccess.cs` + `TvSenderUserAccessConfiguration.cs` + DbSet; Migration `AddTvSenderUserAccess` (FK Cascade → AspNetUsers). |
| N28: Service | `ProgramSettingsService.IsTvSenderAccessGrantedAsync` (Flag UND (Admin ODER Allowlist)), `GetTvSenderAllowedUserIdsAsync`, `UpdateTvSenderAllowedUserIdsAsync` (Replace, ein SaveChanges). |
| N28: Gating | NavMenu (Claim-basiert), LiveTv (`#live-tv-no-access` + kein Guide-Aufruf), ProgramGuideController + LiveStreamController (404 Flag / 403 User). TVSenderAdmin bleibt admin-only. |
| N28: Admin-UI | `ProgramSettings.razor` — Checkbox-Liste unter dem Schalter, Admin-Häkchen fest gesetzt (disabled). |
| N28: User-Löschung | `UserManagement.razor` entfernt Zeilen explizit. |
| N28: Backup | `OptionalRestoreTables` += `TvSenderUserAccess`; Regressionstest `VideoWebPlayerBackupDataTests_TvSenderUserAccess` (Alt-Backup ohne Tabelle + Rundreise). |
| N28: Tests | `TvSenderUserAccessTests` (Matrix + FK-Enforcement + Replace-Semantik, echtes SQLite), Controller-403, NavMenu/LiveTv-bUnit; E2E-Basen gewähren Test-Usern die Freigabe (`DeviceClientTestBase.GrantTvSenderAccessAsync`, `CreateUserAndPairDeviceAsync(grantsTvAccess: true)`, LiveTvE2E seedet UserA-Zeile). |

## Offene Aufgaben

Keine.
