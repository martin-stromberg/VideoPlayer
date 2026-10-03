# Interfaces

## `IAutoUpdateScriptGenerator` (msTools.Updater) — zentraler Erweiterungspunkt
Datei: extern, `msTools.Updater.dll` (XML-Doku Zeilen 2188–2200)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GenerateAsync` | `AutoUpdatePackageDescriptor package`, `string zipPath`, `AutoUpdateInstallationTarget target`, `CancellationToken ct` | `Task<string>` | erzeugt das plattformspezifische Installationsskript, das das Paket entpackt und die Anwendung neu startet; Rückgabe = voller Pfad der Skriptdatei |

- Standardimplementierung: `AutoUpdateScriptGenerator` (**`sealed`**, nicht ableitbar).
- Registrierung: `TryAddSingleton<IAutoUpdateScriptGenerator, AutoUpdateScriptGenerator>()` in `UseAutoUpdate` → **anwendungsseitig überschreibbar**, wenn die eigene Registrierung vor `builder.UseAutoUpdate(...)` erfolgt (in `AutoUpdateExtensions.AddVideoWebPlayerAutoUpdate` vor Zeile 29).
- Konsument: `AutoUpdateInstaller` (Konstruktor-Injektion, ruft `GenerateAsync` in `InstallAsync`).

## `IAutoUpdateEventAggregator` (msTools.Updater)
Datei: extern, `msTools.Updater.dll` (XML-Doku Zeilen 1814–1847)

| Event | Cancellbar | Zweck |
|-------|-----------|-------|
| `BeforeCheckSource` | ja | vor Quellenprüfung |
| `BeforeDownload` | ja | vor Paketdownload |
| `BeforeInstall` | ja | vor Installationsstart — von `UpdateBackupEventBinder` abonniert (Backup) |
| `BeforeStartUpdateScript` | ja | vor Start des Installationsskripts |
| `AfterStartUpdateScript` | nein | nach erfolgreichem Skriptstart |
| `ErrorOccurred` | nein | bei jedem Fehler im Update-Workflow — von `UpdateBackupEventBinder` abonniert (Logging) |

Abonnierte Events im VideoWebPlayer: `BeforeInstall`, `ErrorOccurred` (beide in `UpdateBackupEventBinder`).

## `IUpdateSettingsService` (VideoWebPlayer)
Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` — Implementierung `UpdateSettingsService` (Scoped).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetDefaultSettings` | — | `UpdateSettings` | Defaults aus Konfiguration |
| `GetOrCreateAsync` | `CancellationToken` | `Task<UpdateSettings>` | Singleton-Zeile lesen/erzeugen |
| `UpdateAsync` | `UpdateSettingsUpdate`, `CancellationToken` | `Task<UpdateSettings>` | Settings speichern + auf Laufzeitoptionen anwenden |
| `ApplyToRuntimeOptionsAsync` | `CancellationToken` | `Task` | DB-Werte in `AutoUpdateOptions` schreiben |
| `GetBackupOptionsAsync` | `CancellationToken` | `Task<UpdateBackupOptions>` | Backup-Optionen aus DB-Settings |

## `IUpdateBackupService` (VideoWebPlayer)
Datei: `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs` — Implementierung `VideoWebPlayerUpdateBackupService` (Scoped, optional aufgelöst in `UpdateBackupCoordinator`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateBackupAsync` | `UpdateBackupRequest` (`TargetDirectory`, `Reason`), `CancellationToken` | `Task<UpdateBackupResult>` (`Succeeded`, `BackupFilePath`, `Message`) | erzeugt die `ProgramUpdate`-Sicherung über die `msTools.Backup`-Infrastruktur — sichert Datenbankdaten, **keine** Konfigurationsdateien wie `web.config` |

## Weitere msTools.Updater-Verträge (alle per `TryAddSingleton` registriert, damit ebenfalls überschreibbar)

| Interface | Standardimplementierung | Rolle |
|-----------|------------------------|-------|
| `IAutoUpdateInstaller` | `AutoUpdateInstaller` | revalidiert Paket, löst Ziel + Skriptgenerator auf, startet Skript |
| `IAutoUpdateServiceResolver` | `AutoUpdateServiceResolver` | bestimmt `AutoUpdateInstallationTarget` (AppPool/Service/Executable) |
| `IAutoUpdateServiceProbe` | `DefaultAutoUpdateServiceProbe` | Auto-Erkennung (nur Windows-Dienste via `sc.exe`, Linux-systemd via cgroup/systemctl) |
| `IAutoUpdatePackageStore` | `FileSystemAutoUpdatePackageStore` | `Updates/`-Workspace-Pfade (`pending`, `staging`, `update.lock`, `update.log`, `update.{ps1,sh}`) |
| `IAutoUpdateProcessRunner` | `DefaultAutoUpdateProcessRunner` | startet Skript detached, verifiziert Paketdatei vorher |
| `IAutoUpdateEnvironment` | `HostAutoUpdateEnvironment` | liefert `ApplicationDirectory` (Ziel des Entpackens) |
| `IAutoUpdateOrchestrator` | `AutoUpdateOrchestrator` | koordiniert Check/Download/Install, singleton-serialisiert |
| `IAutoUpdateCommandHandler` | `AutoUpdateCommandService` | manuelle Kommandos (Check/Download/Install) |
| `IInstalledVersionProvider` | `ReleaseMetadataInstalledVersionProvider` | liest `release-metadata.json` aus dem Anwendungsverzeichnis |
| `IAutoUpdateStatusProvider` | `AutoUpdateStatusService` | persistenter Update-Status |
