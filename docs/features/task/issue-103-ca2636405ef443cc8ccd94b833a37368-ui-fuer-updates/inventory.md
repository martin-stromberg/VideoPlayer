# Bestandsaufnahme: UI fuer Updates

## Ergebnisueberblick

Die Anwendung bindet `msTools.Updater` bereits ueber eine lokale DLL ein und registriert das Update-System beim Start von `VideoWebPlayer`. Es gibt Konfiguration in `appsettings.json`, eine technische Dokumentation und eine vorhandene Pre-Install-Backup-Anbindung. Fuer die geforderte Admin-UI fehlen jedoch ein eigener Einstellungs-/Persistenzdienst fuer Update-Einstellungen, eine Update-Seite, serverseitige Admin-Endpunkte fuer manuelle Aktionen und die konkrete Registrierung eines `IUpdateBackupService`, der `msTools.Backup` fuer Update-Backups nutzt.

Die vorhandene Admin-/Berechtigungslogik basiert im aktuellen `VideoWebPlayer` auf dem Claim `IsAdmin=True` und der Authorization-Policy `AdminOnly`. Bestehende Adminseiten pruefen den Claim in Razor-Komponenten; serverseitige Backup-Endpunkte sind bereits mit `[Authorize(Policy = "AdminOnly")]` abgesichert.

## Detaildokumente

- [Updater-Einbindung und Statusoberflaechen](inventory/updater.md)
- [Backup-Funktion und Update-Backup-Anbindung](inventory/backup.md)
- [Admin- und Berechtigungslogik](inventory/admin-auth.md)
- [Einstellungs- und Konfigurationspersistenz](inventory/settings-persistence.md)
- [Blazor/MAUI/UI-Struktur fuer Einstellungsseiten](inventory/ui-structure.md)
- [Teststruktur und relevante vorhandene Tests](inventory/tests.md)

## Relevante Integrationspunkte

| Bereich | Vorhanden | Luecke fuer Umsetzung |
|---------|-----------|------------------------|
| Updater-Bibliothek | `lib/msTools.Updater/msTools.Updater.dll` und XML-Dokumentation; referenziert in `VideoWebPlayer.csproj` und `VideoWebPlayer.Tests.csproj` | Keine eigene UI-/API-Schicht fuer Status, Einstellungen und manuelle Aktionen |
| Updater-Registrierung | `VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`, aufgerufen aus `VideoWebPlayer/Program.cs` | Dienstname ist aktuell konstant `VideoWebPlayer-AutoUpdate`; kein persistenter Admin-Wert |
| Updater-Konfiguration | `VideoWebPlayer/appsettings.json` enthaelt `AutoUpdate` mit `Enabled`, `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `AllowPrereleaseUpdates`, `DownloadPath`, `SourceCheck.Interval`, `Backup` | Runtime-Aenderungen ueber UI muessen persistent werden und in `AutoUpdateOptions`/`UpdateBackupOptions` wirksam sein |
| Status und manuelle Aktionen | `msTools.Updater` stellt `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler` und `AutoUpdateStatusSnapshot` bereit | Noch keine Controller/Services/Razor-Seite, die diese Schnittstellen konsumiert |
| Backup vor Installation | `UpdateBackupEventBinder`, `UpdateBackupCoordinator`, `UpdateBackupOptions`, `IUpdateBackupService` existieren | Kein registrierter Adapter von `IUpdateBackupService` auf `msTools.Backup.IBackupService` |
| Backup-UI und Backup-Persistenz | Admin-Seite `/admin/backups`, `BackupsController`, `VideoWebPlayerBackupFacade`, `BackupSettingsService` | Update-Backup-Einstellung ist separat unter `AutoUpdate:Backup`, nicht mit der Backup-Admin-UI gekoppelt |
| Admin-Schutz | Policy `AdminOnly` und Claim `IsAdmin=True`; `BackupsController` nutzt `[Authorize(Policy = "AdminOnly")]` | Neue Update-Endpunkte sollten dieselbe Policy nutzen; neue Razor-Seite mindestens Claim-gated |
| Einstellungen | `ProgramSettingsService` nutzt DB-Tabelle `Setups`; `BackupSettingsService` nutzt DB-Tabelle `BackupSettings` | Fuer Update-Einstellungen fehlt ein Datenmodell, DbSet, EF-Konfiguration/Migration und Service |
| UI-Struktur | Adminseiten liegen unter `VideoWebPlayer/Components/Pages/Admin`; Navigation in `NavMenu.razor` | Neue Seite sollte dort ergaenzt und im Admin-Menue verlinkt werden |
| Tests | xUnit v3, Moq, EF InMemory/Sqlite; vorhandene Update-Backup-Tests | Neue Tests fuer Update-Settings-Service, Admin-Autorisierung, Options-Anwendung, manuelle Aktionen und Backup-Adapter fehlen |

## Geklaerte offene Punkte aus der Anforderung

- Administrationslogik: Im `VideoWebPlayer` ist der passende Mechanismus `IsAdmin=True` plus `AdminOnly`-Policy.
- Pruefintervall: `msTools.Updater.SourceCheckOptions.Interval` ist in Minuten und muss mindestens `1` sein.
- Updatestatus: `AutoUpdateStatusSnapshot` enthaelt `State`, `InstalledVersion`, `AvailableVersion`, `LastCheckedAt`, `LastCheckResult`, `LastDownloadResult`, `LastInstallResult`, `LastError`, `IsLocked`, `LockCreatedAt`. Der State umfasst `Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `ReadyToInstall`, `Installing`, `Success`, `Failed`, `Disabled`.
- Backup-Funktion: Der bestehende Anschluss fuer Update-Backups ist `IUpdateBackupService.CreateBackupAsync(UpdateBackupRequest, CancellationToken)`; fuer die eigentliche Datensicherung steht `msTools.Backup.IBackupService.CreateBackupAsync(BackupCreateRequest, CancellationToken)` zur Verfuegung.
- Dienstname: `msTools.Updater.AutoUpdateOptions.ServiceName` ist fuer die Service-Zielauflösung relevant; zusaetzlich gibt es `UpdateUnitName` fuer Linux/systemd. Der aktuelle Code setzt nur `UpdateUnitName` konstant.

## Risiken und Hinweise fuer die Planung

- `AutoUpdateOptions` ist laut XML-Dokumentation runtime-mutierbar und als Singleton registriert. Persistierte UI-Aenderungen muessen daher sowohl gespeichert als auch kontrolliert in diese Singleton-Optionen uebertragen werden.
- `IOptions<UpdateBackupOptions>` wird im `UpdateBackupCoordinator` verwendet. Wenn Einstellungen aus der UI veraenderbar werden, reicht reines `Configure<UpdateBackupOptions>` aus `IConfiguration` nicht aus, sofern keine dynamische Options-Quelle oder ein eigener Options-Accessor eingefuehrt wird.
- Manuelle Update-Aktionen duerfen parallel zu automatischen Hosted Services laufen; `msTools.Updater` beschreibt die Orchestrator-/Command-Schicht als intern serialisiert. Die UI sollte trotzdem laufende Aktionen anhand von `AutoUpdateStatusSnapshot.State` und Button-Disable-States abbilden.
- Die vorhandene Backup-UI nutzt serverseitige Form-Posts fuer Dateioperationen und direkte Razor-Methoden fuer kleinere Aktionen. Fuer Update-Aktionen sind Controller-Endpunkte mit `AdminOnly` naheliegend, weil Installation/Check sicherheitsrelevant sind.
- Die vorhandenen `.gitignore`-Aenderungen stammen nicht aus dieser Bestandsaufnahme und wurden nicht bewertet.
