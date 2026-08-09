# Updater-Einbindung und Statusoberflaechen

## Vorhandene Dateien

- `lib/msTools.Updater/msTools.Updater.dll`
- `lib/msTools.Updater/msTools.Updater.xml`
- `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`
- `VideoWebPlayer/Program.cs`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer/appsettings.Development.json`
- `docs/TECH_Auto_Update.md`
- `.github/scripts/create-update-manifest.sh`
- `.github/workflows/main-release.yml`

## Registrierung

`VideoWebPlayer/Program.cs` ruft nach `builder.AddVideoWebPlayerServices()` auch `builder.AddVideoWebPlayerAutoUpdate()` auf. Die Registrierung liegt in `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`.

Die Erweiterung:

- registriert `UpdateBackupOptions` aus `AutoUpdate:Backup`,
- registriert `UpdateBackupCoordinator` als Singleton,
- ruft `builder.UseAutoUpdate(...)` aus `msTools.Updater` auf,
- setzt die GitHub-Quelle auf `martin-stromberg/VideoPlayer`,
- liest optional `AutoUpdate:GitHubToken` fuer private GitHub-Releases,
- setzt aktuell einen konstanten systemd-Unit-Namen `VideoWebPlayer-AutoUpdate`,
- registriert `UpdateBackupEventBinder` als Hosted Service.

## Konfiguration

`VideoWebPlayer/appsettings.json` enthaelt:

- `AutoUpdate:Enabled = true`
- `AutoUpdate:EnableAutomaticDownload = true`
- `AutoUpdate:EnableAutomaticInstallation = true`
- `AutoUpdate:AllowPrereleaseUpdates = false`
- `AutoUpdate:DownloadPath = "Updates"`
- `AutoUpdate:SourceCheck:Interval = 360`
- `AutoUpdate:Backup:Enabled = true`
- `AutoUpdate:Backup:Path = "Backups"`
- `AutoUpdate:Backup:RetainedBackupCount = 5`
- `AutoUpdate:Backup:CancelInstallationOnFailure = true`

`VideoWebPlayer/appsettings.Development.json` deaktiviert `AutoUpdate:Enabled` und `AutoUpdate:EnableAutomaticInstallation`.

## Schnittstellen aus `msTools.Updater`

Die XML-Dokumentation der lokalen Assembly beschreibt folgende fuer die UI relevante Typen:

- `IAutoUpdateCommandHandler`: manuelle Operationen `CheckAsync`, `DownloadAsync`, `InstallAsync(confirmDowntime, ct)`.
- `IAutoUpdateOrchestrator`: vollstaendiger Workflow `RunUpdateAsync`, manuelle Teilschritte `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, Status `GetStatusAsync`.
- `IAutoUpdateStatusProvider`: synchroner Snapshot-Zugriff `GetSnapshot`.
- `AutoUpdateStatusSnapshot`: Statusfelder `State`, `InstalledVersion`, `AvailableVersion`, `LastCheckedAt`, `LastCheckResult`, `LastDownloadResult`, `LastInstallResult`, `LastError`, `IsLocked`, `LockCreatedAt`.
- `AutoUpdateState`: `Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `ReadyToInstall`, `Installing`, `Success`, `Failed`, `Disabled`.
- `AutoUpdateCheckResult`: `AvailableVersion`, `Package`, `ReleaseNotes`, `PublishedAt`, `IsPrerelease`.
- `AutoUpdateDownloadResult`: `LocalPath`, `SizeBytes`, `ChecksumValid`.
- `AutoUpdateInstallResult`: `Version`, `ScriptPath`, `StartedAt`.
- `AutoUpdateOptions`: runtime-mutierbare Singleton-Konfiguration.

## Fehlende UI-Schicht

Es gibt aktuell keine `UpdatesController`, keine Razor-Seite unter `Components/Pages/Admin` und keinen anwendungseigenen Service, der Updater-Status, Optionen und Kommandos fuer eine Admin-UI buendelt.

## Dienstname und Neustart

`AutoUpdateExtensions` setzt nur `WithUpdateUnitName("VideoWebPlayer-AutoUpdate")`. Die Updater-Optionen enthalten laut XML-Dokumentation zusaetzlich `ServiceName`, `ExecutablePath`, `StopHostAfterScriptStart`, `ScheduledInstallTime`, `HealthTimeoutSeconds` und `UpdateUnitName`. Fuer die Anforderung "Dienstname fuer Neustart" ist voraussichtlich `AutoUpdateOptions.ServiceName` der relevante persistente UI-Wert; `UpdateUnitName` betrifft den systemd-Unit-Namen des Installationsskripts.
