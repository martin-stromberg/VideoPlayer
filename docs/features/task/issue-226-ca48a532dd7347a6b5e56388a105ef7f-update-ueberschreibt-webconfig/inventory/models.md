# Datenmodell und Optionsklassen

Keine neuen Tabellen oder Spalten sind von der Anforderung direkt betroffen; relevant sind die bestehenden Options-/Einstellungstypen des Update-Subsystems.

## `UpdateSettings` (EF-Entity, Singleton-Zeile)
Datei: `VideoWebPlayer/Data/UpdateSettings.cs` — Konfiguration: `VideoWebPlayer/Data/Configurations/UpdateSettingsConfiguration.cs`, Migration `20260809163317_AddUpdateSettings.cs`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Singleton-Schlüssel (Default 1) |
| `AutomaticChecksEnabled` | `bool` | automatische Update-Prüfung ein/aus |
| `CheckIntervalMinutes` | `int` | Prüfintervall (Default 360) |
| `AllowPrereleaseUpdates` | `bool` | Pre-Releases akzeptieren |
| `AutomaticInstallationEnabled` | `bool` | automatische Installation erlaubt |
| `AutomaticDownloadEnabled` | `bool` | automatischer Download erlaubt |
| `ServiceName` | `string?` | OS-Dienstname für Neustart (**kein `AppPoolName`-Feld vorhanden**) |
| `CreateBackupBeforeInstallation` | `bool` | Backup vor Installation |
| `CancelInstallationOnBackupFailure` | `bool` | Abbruch bei Backupfehler |
| `UpdateBackupPath` | `string` | Zielpfad der Update-Backups (Default `"Backups"`) |
| `RetainedUpdateBackupCount` | `int` | aufzubewahrende `ProgramUpdate`-Backups (Default 5) |
| `UpdatedAtUtc` | `DateTime` | letzter Änderungszeitpunkt |

## `UpdateBackupOptions` (Options)
Datei: `VideoWebPlayer/Services/Updates/UpdateBackupOptions.cs` — gebunden aus `AutoUpdate:Backup` (`SectionName`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Enabled` | `bool` | Backup vor Update erstellen (Default true) |
| `Path` | `string` | Backup-Zielverzeichnis, relativ zum Content-Root (Default `"Backups"`) |
| `RetainedBackupCount` | `int` | Aufbewahrung (Default 5) |
| `CancelInstallationOnFailure` | `bool` | Installation bei fehlgeschlagenem/fehlendem Backup abbrechen (Default true) |

## `UpdateSettingsUpdate` (Record)
Datei: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` (Zeilen ~261–277)

Felder (Konstruktor-Parameter): `AutomaticChecksEnabled`, `CheckIntervalMinutes`, `AllowPrereleaseUpdates`, `AutomaticInstallationEnabled`, `AutomaticDownloadEnabled`, `ServiceName`, `CreateBackupBeforeInstallation`, `CancelInstallationOnBackupFailure`, `UpdateBackupPath`, `RetainedUpdateBackupCount` — Eingabemodell der Admin-UI.

## `UpdateAdminSnapshot` / `UpdateAdminActionResult` (Records)
Datei: `VideoWebPlayer/Services/Updates/UpdateAdminService.cs` (Zeilen 148, 157–182)

| Record | Felder | Zweck |
|--------|--------|-------|
| `UpdateAdminSnapshot` | `Settings`, `Status` (`AutoUpdateStatusSnapshot`), `DefaultSettings` | UI-Snapshot der Update-Seite |
| `UpdateAdminActionResult` | `Succeeded`, `IsBlocked`, `Message` (+ Fabrikmethoden `Success`/`Blocked`/`Failed`) | Ergebnis manueller Update-Aktionen |

## `AutoUpdateOptions` (msTools.Updater, externe Bibliothek)
Datei: `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg` → `msTools.Updater.dll` — als Singleton-Instanz registriert, zur Laufzeit mutierbar (`UpdateSettingsService.ApplyToRuntimeOptions`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Enabled` | `bool` | Update-System ein/aus |
| `EnableAutomaticDownload` | `bool` | automatischer Download |
| `DownloadPath` | `string` | Arbeitsverzeichnis (Default `"updates"`; hier `"Updates"`) |
| `EnableAutomaticInstallation` | `bool` | automatische Installation |
| `AllowPrereleaseUpdates` | `bool` | Pre-Releases |
| `Source` | `IAutoUpdateSource?` | Update-Quelle (nur per Code) |
| `SourceCheck` | `SourceCheckOptions` | Intervall + Zeitfenster |
| `MaxAssetBytes` | `long` | Paketgrößenlimit (512 MiB) |
| `HostedServicesEnabled` | `bool` | Hintergrunddienste ein/aus |
| `ScheduledInstallTime` | `TimeOnly?` | geplanter Installationszeitpunkt |
| `ServiceName` | `string?` | Windows-Dienst-/Linux-systemd-Unit-Name |
| `ExecutablePath` | `string?` | Executable als Neustart-Ziel |
| `AppPoolName` | `string?` | **IIS-App-Pool für Stop/Start während der Installation** |
| `SiteName` | `string?` | IIS-Site-Name (nur Logging) |
| `WorkspaceUser`/`WorkspaceGroup` | `string?` | Linux-Workspace-Eigentümer |
| `StopHostAfterScriptStart` | `bool` | Host direkt nach Skriptstart beenden (hier `true`) |
| `HealthTimeoutSeconds` | `int` | Health-Timeout (geclamped 10–600) |
| `UpdateUnitName` | `string` | systemd-Unit-Name (hier `VideoWebPlayer-AutoUpdate`) |

**Es gibt keine Eigenschaft zum Schutz oder Ausschluss einzelner Dateien beim Entpacken des Pakets.**

## `AutoUpdateInstallationTarget` (msTools.Updater, Record)
Geliefert von `IAutoUpdateServiceResolver.Resolve()`, Eingabe für `IAutoUpdateScriptGenerator.GenerateAsync`.

| Feld | Typ | Zweck |
|------|-----|-------|
| `Platform` | `string` | `"windows"` / `"linux"` |
| `ServiceName` | `string?` | zu stoppender/startender Dienst |
| `ExecutablePath` | `string?` | neu zu startende Datei |
| `AppPoolName` | `string?` | IIS-App-Pool (wählt die IIS-Skriptvariante) |
| `SiteName` | `string?` | Site-Name für Logging |
