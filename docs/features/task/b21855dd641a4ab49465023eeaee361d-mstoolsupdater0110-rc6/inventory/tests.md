# Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-01 15:12 (lokale Systemzeit, Zeitzone nicht ausgegeben — Windows-Host, voraussichtlich CET)
- Branch und Commit-ID: `task/b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc6` @ `02377b522550d01135823fdcbafbd985d80e5ae4` (`chore: Dummy-Commit für Update-Test-Prerelease (#325)`)
- Uncommittete Änderungen im getesteten Stand: nur untracked — `docs/features/task/b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc6/` (Anforderungsdokumente) und `lib/packages/msTools.Updater.0.11.0-rc.6.nupkg`; keine geänderten getrackten Dateien
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK `10.0.403`, Testprojekt `net10.0`, xunit.v3 3.2.2, `Microsoft.Playwright` 1.62.0 (E2E)
- Ermittelte Testsuiten und Quellen der Testbefehle: Projekt-Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (AGENTS.md §7); lokaler Build-Nachweis via `dotnet build VideoPlayer.sln` (Debug+Release); Markdown-Linkcheck `dotnet test tools/MarkdownLinkCheck.Tests` (im rc.5-Tasks-Dokument als nachgelagerter Schritt vermerkt)
- Aufgelöste Paketversion: `project.assets.json` beider Projekte zeigt `msTools.Updater/0.11.0-rc.5`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| — | kein lokaler Lauf für diese Bestandsaufnahme (schlanke Bestandsaufnahme; CI-Nachweis unten) | — | — | unbekannt | unbekannt | unbekannt | — |

**Stattdessen nachgewiesener Zustand „zuletzt grün":**

- PR #324 (`feat: msTools.Updater auf 0.11.0-rc.5 aktualisieren`) — `gh pr view 324`: state `MERGED`, gemergt 2026-11-01T10:49:36Z; Checks `build & test`, `static checks`, `detect-backmerge` je `SUCCESS`, `back-merge-skip` SKIPPED.
- Nach dem Merge nur zwei `chore:`-Dummy-Commits (`de35de4`, `02377b5`) zur Auslösung von Prereleases — keine Code-Änderungen.
- Beim rc.4→rc.5-Upgrade dokumentierter Ausgangszustand der Voll-Suite (Tasks-Dokument `b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc4-tasks.md`, Aufgabe 7): 2591 + 247 bestanden, 2 dokumentierte Skips, 0 Fehlschläge.

### Nachgewiesene bestehende Testfehler

Keine — es wurde kein lokaler Testlauf durchgeführt. Aus CI (PR #324) und dem dokumentierten Vorgänger-Ausgangszustand ergibt sich kein Hinweis auf vorhandene Fehlschläge.

### Testlücken und Ausführungsprobleme

- Für diese Bestandsaufnahme wurde **kein** Testlauf ausgeführt; der Ausgangszustand ist über CI-Nachweis (PR #324) und das Vorgänger-Tasks-Dokument indirekt belegt. Ein späterer „preexisting"-Verweis kann sich daher nur auf den CI-Stand stützen, nicht auf einen dokumentierten lokalen Lauf.
- Bekannt (AGENTS.md §7): Einzelne Playwright-Tests flackern gelegentlich unter Last.
- Der `AutoUpdateProtectedFilesTests`-Test `GeneratedScript_ContainsBackupAndMergeInOrder` hängt an Markern im Bibliotheks-generierten Skript; alle assertierten Marker sind in der rc.6-DLL nachweislich noch enthalten (siehe [packages.md](packages.md), Abschnitt „Verifikation der vom Test assertierten Skript-Marker").

## Testklassen mit Update-Bezug

### `MsToolsUpdaterIntegrationTests` (`VideoWebPlayer.Tests/Services/MsToolsUpdaterIntegrationTests.cs`)
- `ManualCheckAsync_ClearsPreviousLastErrorInUpdaterStatus` — End-to-End über die echte Bibliothek: `UseAutoUpdate` mit lokalem Quellordner + `update.json`-Manifest, `CheckAsync` über `IAutoUpdateCommandHandler`, Statuszurücksetzung. Läuft direkt gegen die aufgelöste DLL → Nachweis, dass die aufgelöste Paketversion im Test ankommt.

### `AutoUpdateProtectedFilesTests` (`VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs`)
- `ConfigurationBindsProtectedFiles` — Konfig-Binding auf `AutoUpdateOptions.ProtectedFiles`
- `ShippedAppsettingsProtectedFiles_AreValid` — ausgelieferte `appsettings.json`-Schutzliste validiert
- `ShippedWebConfig_ContainsAllMergeParents` — `web.config` enthält alle Merge-Elternknoten
- `GeneratedScript_ContainsBackupAndMergeInOrder` — generiert über `IAutoUpdateScriptGenerator` ein **Windows**-Skript (plattformerzwungen via `IAutoUpdatePlatformResolver`) und assertiert Marker plus Reihenfolge *Backup → Copy → Merge*. **Paket-abhängig** — Marker in rc.6 verifiziert vorhanden.

### `UpdateAdminServiceTests` (`VideoWebPlayer.Tests/Services/UpdateAdminServiceTests.cs`)
- `GetSnapshotAsync_IncludesSettingsStatusAndDefaultSettings`, `CheckAsync_WhenIdle_CallsCommandHandler`, `InstallAsync_WhenNoInstallableVersion_Blocks`, `InstallAsync_WhenUpdateAvailable_DownloadsThenInstalls`, `InstallAsync_WhenDownloadIsSkipped_DoesNotInstall`, `InstallAsync_WhenDownloadIsCanceled_DoesNotInstall`, `ManualActions_WhenStatusLocked_AreBlocked` — Admin-Fassade gegen gemockte Updater-Interfaces

### `UpdateBackupCoordinatorTests` (`VideoWebPlayer.Tests/Services/UpdateBackupCoordinatorTests.cs`)
- `CreateBackupAsync_WithoutRegisteredBackupService_BlocksInstallation`, `CreateBackupAsync_WithoutRegisteredBackupServiceAndFailureAllowed_AllowsInstallation`, `CreateBackupAsync_WhenDisabled_SkipsBackupService`, `CreateBackupAsync_PassesExistingConfiguredDirectory`, `CreateBackupAsync_DoesNotCreateConfiguredDirectoryBeforeCallingProvider`, `CreateBackupAsync_DoesNotApplyUnsafeRetentionToConfiguredDirectory`, `CreateBackupAsync_WhenBackupServiceThrows_BlocksInstallation`, `CreateBackupAsync_WhenBackupFails_BlocksInstallation`

### `UpdateBackupEventBinderTests` (`VideoWebPlayer.Tests/Services/UpdateBackupEventBinderTests.cs`)
- `BeforeInstall_WhenBackupSucceeds_InstallationIsNotCanceled`, `BeforeInstall_WithoutBackupService_CancelsInstallation`, `StopAsync_UnsubscribesFromBeforeInstall` — Bindung an `BeforeInstall`-Event

### `UpdateSettingsServiceTests` / `UpdateSettingsServiceTests_Iis` (`VideoWebPlayer.Tests/Services/`)
- Defaults, Validierung, Laufzeit-Übernahme in `AutoUpdateOptions` (u. a. `ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged`), IIS-Verhalten des `ServiceName`-Felds

### `UpdatesControllerAuthorizationTests` (`VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs`)
- `UpdatesController_RequiresAdminOnlyPolicy`, `ActionEndpoint_IsExposedAsServerSidePost`, `Check_ValidatesAntiforgeryToken`, `Install_ValidatesAntiforgeryToken`

### `UpdatesPageE2ETests` (`VideoWebPlayer.Tests/UpdatesPageE2ETests.cs`)
- Playwright-E2E: `Admin_Navigates_To_Updates_Page_And_Sees_Structured_Status`, `Admin_Checks_For_Updates_And_Refreshes_Changed_Data`, `Admin_Edits_Resets_And_Saves_Update_Configuration`, `Admin_Installs_Update_Through_Post_And_Sees_Result`, `Updates_Page_Disables_Manual_Actions_When_Busy_Or_Locked`, `Updates_Page_Remains_Usable_On_Mobile_Viewport`

### Weitere Tests mit `AutoUpdate`-Berührung (nicht paketkritisch)
`ApiDocumentationContractTests_Runtime.cs`, `BackupSettingsServiceTests.cs`, `BackupUploadE2ETests.cs`, `DevicePairingE2ETests.cs`, `PairingBootstrapE2ETests.cs`, `PairingBootstrapEndpointsTests.cs`, `ProgramSettingsE2ETests.cs`, `Helpers/DeviceClientTestBase.cs`, `Helpers/LocalMediaPlaylistE2ETestBase.cs` — nutzen `AutoUpdate` nur am Rand (z. B. `DisableHostedServices` in Test-Host-Setups oder appsettings-Kontext).

## Hilfsmethoden

### `AutoUpdateProtectedFilesTests`
- `LoadShippedAutoUpdateOptions` — lädt die ausgelieferte `VideoWebPlayer/appsettings.json` und bindet `AutoUpdate`-Sektion
- `FindRepositoryFile` — sucht Repo-Dateien aufwärts von `AppContext.BaseDirectory`
- `AutoUpdatePlatformResolver`-Fake (inline) — erzwingt `win-x64`-Skriptgenerierung auf beliebigem Test-Host

### `MsToolsUpdaterIntegrationTests`
- `ComputeSha256Async`, `GetCurrentPlatform`, `GetCurrentRuntimeIdentifier` — Manifest-/Plattform-Helfer für die lokale Update-Quelle
