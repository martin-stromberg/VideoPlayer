# Tests — Ausgangszustand und Bestand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-01, Läufe ca. 10:34–10:44 +0100 (CET)
- Branch und Commit-ID: `task/b21855dd641a4ab49465023eeaee361d-mstoolsupdater0110-rc4` @
  `ab0e5307612a4ea61b497a4b5cef3d6a3c6def61`
- Uncommittete Änderungen im getesteten Stand: keine geänderten getrackten Dateien; untracked
  waren das Feature-Verzeichnis `docs/features/task/b21855dd…-mstoolsupdater0110-rc4/`
  (`requirement.md`, `todo.md`, diese Bestandsaufnahme) und das neue Paket
  `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg`. **Der getestete Stand referenziert weiterhin
  rc.4** — das rc.5-nupkg lag unreferenziert im Feed-Verzeichnis und beeinflusst Restore/Build
  nicht.
- Testumgebung und Runtime-/SDK-Versionen: Windows, Git Bash; .NET SDK 10.0.403
  (`C:\Program Files\dotnet\sdk`), Testlaufzeit .NET 10.0.14, xunit.v3 3.2.2, VSTest-Adapter
  xunit.runner.visualstudio 3.1.5; Playwright 1.62.0 mit installierten Chromium-Browsern
  (`~/AppData/Local/ms-playwright`, headless). Arbeitsverzeichnis aller Befehle: Repository-Root
  `D:\Repositories\softwareschmiede\b21855dd-641a-4ab4-9465-023eeaee361d`.
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - AGENTS.md „Definition of Done": `dotnet build VideoPlayer.sln` in Debug **und** Release;
    volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`.
  - `.github/workflows/staging-ci.yml` und `pr-staging-ci.yml` (identische Testschritte):
    Release-Build des Testprojekts, dann drei Läufe mit Filtern `Category!=E2E`,
    `Category=Integration`, `Category=E2E`. **Hinweis:** Im gesamten Testprojekt existiert kein
    einziger Test mit `Trait("Category", "Integration")` — der CI-Integration-Lauf matcht
    aktuell 0 Tests; alle Integrationstests laufen im `Category!=E2E`-Lauf mit.
  - `release.yml`: ein Lauf ohne Filter (volle Suite).
  - Die Kategorie `E2E` markiert 75 Trait-Attribute (Playwright-/Host-Tests); der Filter
    `Category=E2E` discoverte 248 Testfälle (Klassen-Level-Traits + Theory-Auflösung).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Restore | `dotnet restore VideoPlayer.sln` | Repo-Root | 0 | – (6 Projekte wiederhergestellt) | – | – | Konsolenausgabe (nicht archiviert) |
| Build Debug | `dotnet build VideoPlayer.sln -c Debug --no-restore` | Repo-Root | 0 | 0 Fehler, 140 Warnungen | – | – | [build-debug.log](test-results/build-debug.log) |
| Build Release | `dotnet build VideoPlayer.sln -c Release --no-restore` | Repo-Root | 0 | 0 Fehler, 140 Warnungen | – | – | [build-release.log](test-results/build-release.log) |
| Tests Nicht-E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --filter "Category!=E2E" --logger "trx;LogFileName=inventory-nonaute2e-results.trx"` | Repo-Root | 0 | 2591 | 0 | 1 | [test-none2e.log](test-results/test-none2e.log), [inventory-nonaute2e-results.trx](test-results/inventory-nonaute2e-results.trx) |
| Tests E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --filter "Category=E2E" --logger "trx;LogFileName=inventory-e2e-results.trx"` | Repo-Root | 0 | 247 | 0 | 1 | [test-e2e.log](test-results/test-e2e.log), [inventory-e2e-results.trx](test-results/inventory-e2e-results.trx) |

Gesamt: 2840 Testfälle, 2838 bestanden, 2 übersprungen, **0 fehlgeschlagen**. Die 140
Build-Warnungen sind Bestand (u. a. BL0008 in Identity-Razor-Seiten), identisch in Debug und
Release.

### Nachgewiesene bestehende Testfehler

Keine. Beide Läufe endeten mit Exit-Code 0 ohne Fehlschlag. Damit gilt für spätere Arbeiten:
Jeder Fehlschlag, der nach dem Paket-Bump auftritt, ist **nicht** preexisting, sofern er nicht
einen der beiden unten dokumentierten Skip-Fälle betrifft.

### Testlücken und Ausführungsprobleme

| Test | Lauf | Grund / Umfang |
|------|------|----------------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Nicht-E2E | Übersprungen: Unix-only-Test, auf Windows nicht ausführbar (kein Fehler). |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | E2E | Übersprungen per `Skip` mit dokumentierter Begründung: bekannter Alt-Befund — `api/continue-watching` antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.Create` ohne Null-Prüfung); außerhalb dieses Auftrags. Derselbe Skip ist im Tasks-Dokument des Vorprojekts (Schritt V1) dokumentiert. |

Infrastruktur-/Setup-Fehler: keine. Der CI-Lauf `Category=Integration` wurde nicht separat
ausgeführt, da im Projekt kein Test dieses Trait trägt und der Nicht-E2E-Lauf alle übrigen
Tests bereits vollständig abdeckt.

## Testklassen (Update-bezogen)

### `MsToolsUpdaterIntegrationTests`

Datei: `VideoWebPlayer.Tests/Services/MsToolsUpdaterIntegrationTests.cs` — Integrationstest
gegen die echte Bibliothek: baut einen `HostApplicationBuilder` mit `UseAutoUpdate`,
`UseLocalFolderSource` (Temp-Verzeichnis mit `update.json`-Manifest + Paket + SHA256),
`release-metadata.json`, `DisableHostedServices`; löst `AutoUpdateStatusService`,
`IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`. Temp-Fixture via `IDisposable`.

- `ManualCheckAsync_ClearsPreviousLastErrorInUpdaterStatus` — ein gesetzter
  `AutoUpdateErrorCode.SourceUnavailable`/`LastError` wird durch die nächste manuelle Prüfung
  bereinigt (`LastErrorCode` danach `null`/`None`).

### `AutoUpdateProtectedFilesTests`

Datei: `VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs` — prüft die
`ProtectedFiles`-Schutzliste; Temp-Fixture.

- `ConfigurationBindsProtectedFiles` — `AutoUpdate:ProtectedFiles` bindet korrekt in
  `AutoUpdateOptions` (Pfad, `Strategy`, `XmlElements`, `XmlAttributes`, `JsonKeys`).
- `ShippedAppsettingsProtectedFiles_AreValid` — die ausgelieferte `appsettings.json` besteht
  `AutoUpdateOptionsValidator` und enthält 2 Schutz-Einträge.
- `ShippedWebConfig_ContainsAllMergeParents` — alle `XmlElements`/`XmlAttributes`-Elternpfade
  existieren im ausgelieferten `VideoWebPlayer/web.config` (XPath).
- `GeneratedScript_ContainsBackupAndMergeInOrder` — **skript-assertierender Test**: erzeugt das
  Windows-Installationsskript über `IAutoUpdateScriptGenerator` (erzwungene Win-x64-Plattform
  via `AutoUpdatePlatformResolver`), assertiert Marker (`$protectedFiles = @(`,
  `Path = 'web.config'`, `Path = 'appsettings*.json'`, `Strategy = 'Merge'`, XPath-/JsonKey-
  Inhalte, `PackageStore.RootDirectory/backup`) und die Reihenfolge
  `Backup-AutoUpdateProtectedFiles` → `Copy-AutoUpdateStagedFiles` →
  `Invoke-AutoUpdateProtectedFiles`. Beim rc.4-Upgrade musste hier der Kopiermarker angepasst
  werden — **primärer Kandidat für Bruch bei rc.5**.

### `UpdateAdminServiceTests`

Datei: `VideoWebPlayer.Tests/Services/UpdateAdminServiceTests.cs` — Moq auf
`IAutoUpdateOrchestrator`/`IAutoUpdateCommandHandler`/`IUpdateSettingsService`; baut
`AutoUpdateStatusSnapshot` direkt (Record-Positionskonstruktor — Form-Änderung in rc.5 würde
kompilierbrechen, ist aber additiv).

- `GetSnapshotAsync_IncludesSettingsStatusAndDefaultSettings` — Snapshot-Bündelung.
- `CheckAsync_WhenIdle_CallsCommandHandler` — manuelle Prüfung bei `Idle`.
- `InstallAsync_WhenNoInstallableVersion_Blocks` — Block ohne bekannte Version.
- `InstallAsync_WhenUpdateAvailable_DownloadsThenInstalls` — Download → Install-Reihenfolge.
- `InstallAsync_WhenDownloadIsSkipped_DoesNotInstall` / `..._IsCanceled_DoesNotInstall` —
  `Skipped`/`Canceled` stoppen vor `InstallAsync`.
- `ManualActions_WhenStatusLocked_AreBlocked` — `IsLocked` blockiert `CheckAsync`.

### `UpdateSettingsServiceTests`

Datei: `VideoWebPlayer.Tests/Services/UpdateSettingsServiceTests.cs` — EF InMemory +
echte `AutoUpdateOptions` + `VideoWebPlayerUpdateSourceFactory` + `IUpdateHostEnvironment`-Mock.

- `GetOrCreateAsync_UsesConfiguredDefaults` — Defaults aus Konfiguration.
- `UpdateAsync_AppliesValidValuesToPersistenceAndRuntimeOptions` — Persistenz + Options-Sync.
- `UpdateAsync_RejectsInvalidIntervalAndRetentionValues` (Theory) — Grenzvalidierung.
- `GetOrCreateAsync_CleansPersistedLegacyLimitsBeforeReturningSettingsAndRuntimeOptions` (Theory) —
  Clamp/Normalisierung persistierter Alt-Werte.
- `AdminSnapshot_UsesCleanedPersistedLegacyLimits` — normalisierte Werte in der Admin-Sicht.
- `GetDefaultSettings_UsesSameConfiguredFallbacksAsNewSettings` — Defaults identisch.
- `ApplyToRuntimeOptions_WhenAutomaticChecksDisabled_KeepsUpdaterEnabledForManualActions` —
  deaktivierte Auto-Checks erzeugen geschlossene `SourceCheck.TimeRanges`, `Enabled` bleibt.
- `GetBackupOptionsAsync_ReturnsPersistedUpdateBackupSettings` — Mapping auf
  `UpdateBackupOptions`.
- `ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged` — `ProtectedFiles` der Options werden
  nicht überschrieben.

### `UpdateSettingsServiceTests_Iis`

Datei: `VideoWebPlayer.Tests/Services/UpdateSettingsServiceTests_Iis.cs` — gleiche Fixture mit
`RunsUnderIis=true`-Mock.

- `UpdateAsync_UnderIis_DiscardsSubmittedServiceName` — `ServiceName` wird nicht persistiert.
- `ApplyToRuntimeOptions_UnderIis_DoesNotCopyPersistedServiceName` — kein Options-Transfer.
- `GetOrCreateAsync_UnderIis_ClearsPersistedServiceName` — Alt-Wert wird bereinigt.
- `GetOrCreateAsync_UnderIis_SkipsConfiguredServiceNameDefault` — Konfig-Default entfällt.

### `UpdateBackupCoordinatorTests`

Datei: `VideoWebPlayer.Tests/Services/UpdateBackupCoordinatorTests.cs` — eigene
`ServiceCollection` (echte `UpdateSettingsService` auf EF InMemory, `AutoUpdateOptions`,
`IUpdateHostEnvironment`-Mock), `IUpdateBackupService`-Mocks; Temp-ContentRoot.

- 8 Tests: Block bei fehlendem/fehlschlagendem Backup-Dienst (`CancelInstallationOnFailure`),
  Skip bei `Enabled=false`, Weitergabe des konfigurierten Verzeichnisses ohne es anzulegen,
  keine pauschale Retention auf das konfigurierte Verzeichnis.

### `UpdateBackupEventBinderTests`

Datei: `VideoWebPlayer.Tests/Services/UpdateBackupEventBinderTests.cs` — nutzt die konkrete
Bibliotheksklasse `AutoUpdateEvents` (mit `RaiseBeforeInstall`) als `IAutoUpdateEventAggregator`.

- `BeforeInstall_WhenBackupSucceeds_InstallationIsNotCanceled` — `Cancel` bleibt `false`.
- `BeforeInstall_WithoutBackupService_CancelsInstallation` — `Cancel` wird `true`.
- `StopAsync_UnsubscribesFromBeforeInstall` — Abmeldung wirkt.

### `UpdatesControllerAuthorizationTests`

Datei: `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs` — prüft die serverseitige
Absicherung der Aktionsendpunkte; baut `AutoUpdateStatusSnapshot` per Positionskonstruktor.

- `UpdatesController_RequiresAdminOnlyPolicy` — `[Authorize(Policy="AdminOnly")]` auf Klasse.
- `ActionEndpoint_IsExposedAsServerSidePost` (Theory) — `check`/`install` als HttpPost-Route.
- `Check_ValidatesAntiforgeryToken` / `Install_ValidatesAntiforgeryToken` — Redirect mit
  Fehlermeldung bei ungültigem Token.

### `UpdatesPageE2ETests`

Datei: `VideoWebPlayer.Tests/UpdatesPageE2ETests.cs` — 6 Playwright-Tests
(`Trait("Category", "E2E")`) gegen `WebApplicationFactory<Program>` + Kestrel;
`AutoUpdate:HostedServicesEnabled=false`; `FakeUpdateBackend` ersetzt
`IUpdateSettingsService`/`IAutoUpdateOrchestrator`/`IAutoUpdateCommandHandler` in der DI.

- `Admin_Navigates_To_Updates_Page_And_Sees_Structured_Status` — Seitenstruktur/Status.
- `Admin_Checks_For_Updates_And_Refreshes_Changed_Data` — Check-POST + Polling-Refresh
  (inkl. `LastErrorCode`/`AutoUpdateErrorCode.SourceUnavailable`-Anzeige).
- `Admin_Edits_Resets_And_Saves_Update_Configuration` — Formular-Roundtrip inkl. Prerelease-
  Bestätigung.
- `Admin_Installs_Update_Through_Post_And_Sees_Result` — Install-POST + Redirect-Message.
- `Updates_Page_Disables_Manual_Actions_When_Busy_Or_Locked` — Sperrlogik der Buttons.
- `Updates_Page_Remains_Usable_On_Mobile_Viewport` — Mobile-Layout.

## Hilfsmethoden

### `FakeUpdateBackend` (privat in `UpdatesPageE2ETests.cs`, Z. 469 ff.)

Implementiert `IUpdateSettingsService`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`
in einer Klasse: hält `UpdateSettings` + `AutoUpdateStatusSnapshot` im Speicher, simuliert
Check/Download/Install-Ergebnisse (`AutoUpdateResult`), konstruiert Snapshots über den
Positionskonstruktor (Z. 584–603, inkl. `AutoUpdateErrorCode? lastErrorCode`).

### Fixture-Helfer der Update-Unit-Tests

- `UpdateAdminServiceTests.CreateService`/`Status` — Moq-Fixture, `AutoUpdateStatusSnapshot`
  per Positionskonstruktor.
- `UpdateSettingsServiceTests.CreateDb`/`CreateService` — EF-InMemory-`ApplicationDbContext`
  (`new EventManager()`), `UpdateSettingsService` mit `VideoWebPlayerUpdateSourceFactory` und
  `isIisHosted`-schaltbarem `IUpdateHostEnvironment`-Mock.
- `UpdateBackupCoordinatorTests.CreateCoordinator`/`CreateBackupService`/`WriteBackupFile` —
  eigene `ServiceCollection` mit echter `UpdateSettingsService`-Kette (EF InMemory,
  `AutoUpdateOptions`, `VideoWebPlayerUpdateSourceFactory`, `IUpdateHostEnvironment`-Mock) und
  `IUpdateBackupService`-Delegatmocks.
- `UpdateBackupEventBinderTests.CreateBinder`/`StartBinderAsync` — gleiche DI-Kette plus
  `AutoUpdateEvents`-Instanz.
- `AutoUpdateProtectedFilesTests.LoadShippedAutoUpdateOptions`/`FindRepositoryFile` — lädt die
  echte `appsettings.json`/`web.config` aus dem Repo in `AutoUpdateOptions`
  (`AutoUpdateLocalFolderSource`-Fallback wie `BuildOptions` in `UseAutoUpdate`).
- `MsToolsUpdaterIntegrationTests.ComputeSha256Async`/`GetCurrentPlatform`/
  `GetCurrentRuntimeIdentifier` — manifest-konforme Paketdaten je Plattform.

## Weitere Dateien mit Updater-Bezug

- `BackupSettingsServiceTests` — prüft u. a. `GetOptionsAsync_MapsUpdateBackupRetentionFromUpdateSettings`
  (Retention-Mapping `UpdateSettings` → `BackupRetentionOptions.ProgramUpdateCount`).
- Weitere Test-Hosts deaktivieren das Subsystem über den Bibliotheks-Schlüssel
  `AutoUpdate:HostedServicesEnabled=false`: `ApiDocumentationContractTests_Runtime`,
  `BackupUploadE2ETests`, `DevicePairingE2ETests`, `PairingBootstrapE2ETests`,
  `PairingBootstrapEndpointsTests`, `ProgramSettingsE2ETests`,
  `Helpers/DeviceClientTestBase`, `Helpers/LocalMediaPlaylistE2ETestBase`.
