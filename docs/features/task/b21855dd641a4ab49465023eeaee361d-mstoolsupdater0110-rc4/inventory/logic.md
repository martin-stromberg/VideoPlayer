# Logik — konsumierender Code des `msTools.Updater`-Subsystems

## Registrierung und Einstiegspunkt

### `AutoUpdateExtensions`

Datei: `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddVideoWebPlayerAutoUpdate(WebApplicationBuilder)` | `public static` | Registriert das Update-Subsystem: bindet `UpdateBackupOptions` aus `AutoUpdate:Backup`, registriert `UpdateBackupCoordinator` (Singleton) und die Hosted Services `UpdateSettingsInitializer` + `UpdateBackupEventBinder`; ruft `builder.UseAutoUpdate(...)` der Bibliothek auf und setzt via `cfg.UseSource(...)` die GitHub-Quelle aus `VideoWebPlayerUpdateSourceFactory` sowie `WithUpdateUnitName("VideoWebPlayer-AutoUpdate")`. Liest `AutoUpdate:AllowPrereleaseUpdates` über `AutoUpdateHostBuilderExtensions.DefaultConfigurationSectionName` (Z. 31). |

Aufgerufen von `Program.cs` Z. 32 (`builder.AddVideoWebPlayerAutoUpdate()`).

DI-Registrierungen der Update-Services in `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
(Z. 302–307): `VideoWebPlayerUpdateSourceFactory` (Singleton), `IUpdateHostEnvironment` →
`UpdateHostEnvironment` (Singleton), `UpdateSettingsService` + `IUpdateSettingsService` (Scoped),
`UpdateAdminService` (Scoped), `IUpdateBackupService` → `VideoWebPlayerUpdateBackupService` (Scoped).

## Services `VideoWebPlayer/Services/Updates/`

### `UpdateAdminService`

Datei: `VideoWebPlayer/Services/Updates/UpdateAdminService.cs` — Fassade für Admin-UI und
`UpdatesController`; injiziert `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`,
`IUpdateSettingsService`, `ILogger`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSnapshotAsync` | `public` | Liefert `UpdateAdminSnapshot` (Settings + `AutoUpdateStatusSnapshot` + Defaults). |
| `UpdateSettingsAsync` | `public` | Delegiert an `IUpdateSettingsService.UpdateAsync`. |
| `CheckAsync` | `public` | `ApplyToRuntimeOptionsAsync`, blockiert bei `IsBusy`, sonst `IAutoUpdateCommandHandler.CheckAsync` → `UpdateAdminActionResult`. |
| `InstallAsync` | `public` | Wie `CheckAsync`; zusätzlich `IsInstallable`-Prüfung, bei `UpdateAvailable` erst `DownloadAsync`, dann `InstallAsync(true, false)`; `Exception` → `Failed`. |
| `IsBusy(AutoUpdateStatusSnapshot)` | `public static` | `IsLocked` oder State ∈ {`Checking`, `Downloading`, `Installing`}. |
| `IsInstallable(AutoUpdateStatusSnapshot)` | `public static` | State ∈ {`UpdateAvailable`, `ReadyToInstall`} und bekannte Version. |

### `UpdateSettingsService` / `UpdateSettingsInitializer`

Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs`,
`VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDefaultSettings` / `GetOrCreateAsync` | `public` | Singleton-Zeile lesen/erzeugen aus `AutoUpdate:*`-Konfiguration; `NormalizePersistedSettings` clampt Intervalle/Retention und bereinigt `ServiceName` unter IIS. |
| `UpdateAsync` | `public` | Validiert (Intervall 1–1440, Retention 1–10), persistiert, ruft `ApplyToRuntimeOptions`. Unter IIS wird `ServiceName` verworfen. |
| `ApplyToRuntimeOptionsAsync` / `ApplyToRuntimeOptions` | `public` / `private` | Schreibt persistierte Werte in die runtime-mutierbaren `AutoUpdateOptions`: `Enabled=true`, `SourceCheck.Interval`, `SourceCheck.TimeRanges` (leer = aktiv; bei deaktivierten Auto-Checks geschlossene Fenster `00:00–00:00` für alle Wochentage über `SourceCheckTimeRange`), `AllowPrereleaseUpdates`, `EnableAutomaticInstallation`, `EnableAutomaticDownload`, `ServiceName` (unter IIS `null`), `Source` via `VideoWebPlayerUpdateSourceFactory`. |
| `GetBackupOptionsAsync` | `public` | Mappt `UpdateSettings` → `UpdateBackupOptions`. |

`UpdateSettingsInitializer` (`IHostedService`) wendet beim Start die persistierten Einstellungen
an, bevor die Updater-Hosted-Services ihre Periodik beginnen; Fehler werden geloggt und
weitergeworfen.

### `UpdateBackupEventBinder`

Datei: `VideoWebPlayer/Services/Updates/UpdateBackupEventBinder.cs` — `IHostedService`.

- Abonnierte Events (auf `IAutoUpdateEventAggregator` der Bibliothek): `BeforeInstall`,
  `ErrorOccurred` (Z. 35–36; Abmeldung in `StopAsync`).
- `OnBeforeInstall` blockt synchron auf `UpdateBackupCoordinator.CreateBackupAsync`
  (`Programmupdate: {PackageFile.Name}`); bei `false` → `args.Cancel = true`.
- `OnErrorOccurred` loggt `AutoUpdateErrorEventArgs.Error`/`Phase`.

### `UpdateBackupCoordinator`

Datei: `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateBackupAsync(string reason, CancellationToken)` | `public` | Scope auflösen → `IUpdateSettingsService.GetBackupOptionsAsync`; bei `Enabled=false` `true`; ohne registriertes `IUpdateBackupService` → `!CancelInstallationOnFailure`; sonst `CreateBackupAsync(new UpdateBackupRequest(targetDirectory, reason))` aufrufen; Fehler/Fehlschlag → `!CancelInstallationOnFailure`. |
| `ResolveTargetDirectory` | `private` | Relativer `Path` → gegen `IHostEnvironment.ContentRootPath`; legt das Verzeichnis bewusst nicht an. |

### `VideoWebPlayerUpdateBackupService`

Datei: `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs` — implementiert
`IUpdateBackupService` über die `msTools.Backup`-Infrastruktur (`IBackupService`,
`IBackupDataSource`, `BackupOperationHistoryService`). Erstellt `programupdate-{timestamp}`-Export
mit `BackupGeneration.ProgramUpdate`, protokolliert `ProgramUpdateBackup` in der
Backup-Historie, wendet die bestehende Retention an und mappt auf `UpdateBackupResult`.

### `VideoWebPlayerUpdateSourceFactory`

Datei: `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Create(bool includePrereleases)` | `public` | `AutoUpdateGithubSource` für `martin-stromberg/VideoPlayer`; ohne `AutoUpdate:GitHubToken` via statischem `AutoUpdateGithubSource.Create(...)`, mit Token eigener `HttpClient` (Bearer, GitHub-API-Header, 5 min Timeout, 10 min PooledConnectionLifetime) → `new AutoUpdateGithubSource(httpClient, owner, repo, includePrereleases)`. |

### `UpdateHostEnvironment`

Datei: `VideoWebPlayer/Services/Updates/UpdateHostEnvironment.cs` — implementiert
`IUpdateHostEnvironment`; `RunsUnderIis` = `true`, sobald eine der ANCM-Marker-Umgebungsvariablen
(`ASPNETCORE_TOKEN`, `ASPNETCORE_PORT`, `ASPNETCORE_IIS_HTTPAUTH`, `ASPNETCORE_IIS_APPL_PATH`,
`ASPNETCORE_IIS_PHYSICAL_PATH`) gesetzt ist.

## HTTP-Endpunkte

### `UpdatesController`

Datei: `VideoWebPlayer/Controllers/UpdatesController.cs` — `[ApiController]`,
`[Authorize(Policy = "AdminOnly")]`, `[Route("admin/updates/api")]`.

| Aktion | Route | Beschreibung |
|--------|-------|--------------|
| `Check` | `POST admin/updates/api/check` | Antiforgery-Validierung, dann `UpdateAdminService.CheckAsync`; Redirect `/admin/updates?checkStatus|checkError=…`. |
| `Install` | `POST admin/updates/api/install` | Antiforgery-Validierung, dann `UpdateAdminService.InstallAsync`; Redirect `/admin/updates?updateStatus|updateError=…`. |

## UI

### `Updates.razor`

Datei: `VideoWebPlayer/Components/Pages/Admin/Updates.razor` — Seite `/admin/updates`,
`InteractiveServer`, Admin-Claim `IsAdmin=True`, injiziert `UpdateAdminService` und
`IUpdateHostEnvironment`.

- Zeigt `AutoUpdateStatusSnapshot`: `State` (Chip/Badge über `GetStateLabel`/`GetStatusChipClass`/
  `GetStatusIndicatorClass`/`GetStatusText` — switch über alle `AutoUpdateState`-Werte),
  `InstalledVersion`, `AvailableVersion ?? LastCheckResult?.AvailableVersion`, `LastCheckedAt`,
  `LastError` + `LastErrorCode` (generisch als `Code: @status.LastErrorCode`, Z. 86–89),
  `IsLocked`/`LockCreatedAt`, `LastDownloadResult` (`FormatDownload`), `LastInstallResult`
  (`FormatInstall`), `LastCheckResult.IsPrerelease`/`PublishedAt`.
- Buttons posten auf `UpdatesController`-Endpunkte (Antiforgery-Token); Sperrlogik clientseitig
  über `UpdateAdminService.IsBusy`/`IsInstallable`, serverseitig identisch.
- EditForm `SettingsModel` ↔ `UpdateSettingsUpdate` (Speichern über `UpdateAdminService.UpdateSettingsAsync`);
  Prerelease-Aktivierung erfordert Bestätigungs-Checkbox; `ServiceName`-Feld unter IIS
  `disabled` (über `IUpdateHostEnvironment`).
- Polling des Status alle 3 s via `PeriodicTimer`; `SettingsModel` mit `DataAnnotations`-Validierung
  (gleiche Grenzen wie `UpdateSettingsService`).

## Konfiguration `appsettings.json` (`AutoUpdate`-Sektion, Z. 115 ff.)

`Enabled=true`, `EnableAutomaticDownload=true`, `EnableAutomaticInstallation=true`,
`ScheduledInstallTime="02:00:00"`, `AllowPrereleaseUpdates=false`,
`StopHostAfterScriptStart=true`, `DownloadPath="Updates"`, `SourceCheck.Interval=360`,
`Backup` (Enabled/Path/RetainedBackupCount/CancelInstallationOnFailure) sowie
`ProtectedFiles`: `web.config` (Strategy `Merge`, `XmlElements`
`//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//system.webServer/httpProtocol`
+ `XmlAttributes`) und `appsettings*.json` (`Merge`, `JsonKeys`).
`appsettings.Development.json` deaktiviert das Subsystem (`Enabled=false`,
`EnableAutomaticInstallation=false`). `appsettings.Testing.json` enthält keine
`AutoUpdate`-Sektion. `web.config` liegt als ausgelieferte Datei in `VideoWebPlayer/web.config`
vor (Merge-Ziel der Schutzliste).
