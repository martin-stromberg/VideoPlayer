# Backup-Funktion und Update-Backup-Anbindung

## Vorhandene Backup-Bibliothek

Das Projekt enthaelt die wiederverwendbare Bibliothek `msTools.Backup` mit Projekt `msTools.Backup/msTools.Backup.csproj`. `VideoWebPlayer` referenziert diese Bibliothek und registriert sie in `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` ueber `services.AddBackups(configuration.GetSection("Backups"))`.

Wichtige Schnittstellen und Klassen:

- `msTools.Backup.IBackupService`
- `BackupCreateRequest`
- `BackupGeneration`
- `BackupOperationResult`
- `IBackupOptionsProvider`
- `IAutomaticBackupRunner`
- `IBackupDataProvider`
- `IBackupRestoreGuard`

## Vorhandene VideoWebPlayer-Integration

Im Webprojekt existieren:

- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`
- `VideoWebPlayer/Services/Backups/BackupSettingsService.cs`
- `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs`
- `VideoWebPlayer/Services/Backups/RestoreBackupJobService.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataProvider.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerAutomaticBackupRunner.cs`
- `VideoWebPlayer/Controllers/BackupsController.cs`
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`

`VideoWebPlayerBackupFacade.CreateManualBackupAsync` erstellt manuelle Backups ueber:

`IBackupService.CreateBackupAsync(new BackupCreateRequest(BackupGeneration.Manual, "VideoWebPlayer"), cancellationToken)`

Danach wird die Historie geschrieben und bei Erfolg die Retention angewendet.

## Vorhandene Update-Backup-Anbindung

Unter `VideoWebPlayer/Services/Updates` existieren:

- `IUpdateBackupService.cs`
- `UpdateBackupCoordinator.cs`
- `UpdateBackupEventBinder.cs`
- `UpdateBackupOptions.cs`

`UpdateBackupEventBinder` abonniert `IAutoUpdateEventAggregator.BeforeInstall`. Vor der Installation ruft er synchron wartend `UpdateBackupCoordinator.CreateBackupAsync(reason)` auf. Wenn diese Methode `false` liefert, setzt der Binder `args.Cancel = true`.

`UpdateBackupCoordinator`:

- liest `UpdateBackupOptions`,
- ueberspringt Backups, wenn `Enabled` false ist,
- loest optional `IUpdateBackupService` aus einem Scope auf,
- erstellt das Zielverzeichnis,
- ruft `IUpdateBackupService.CreateBackupAsync(new UpdateBackupRequest(targetDirectory, reason), cancellationToken)` auf,
- wendet Retention anhand `RetainedBackupCount` an,
- bricht bei Fehler ab, wenn `CancelInstallationOnFailure` true ist.

## Zentrale Luecke

`IUpdateBackupService` ist in `ServiceCollectionExtensions.cs` nicht registriert. Damit wird bei Standardkonfiguration kein echtes `msTools.Backup`-Backup vor der Installation erstellt; bei `CancelInstallationOnFailure = true` kann die Installation wegen fehlendem Service abgebrochen werden.

Fuer die Umsetzung braucht es einen Adapter, der `IUpdateBackupService` implementiert und `IBackupService.CreateBackupAsync(...)` nutzt. Da `IBackupService` den Speicherort selbst ueber `IBackupOptionsProvider` ermittelt, muss geklaert werden, ob der vom Updater angeforderte `UpdateBackupRequest.TargetDirectory` verwendet werden muss oder ob die bestehenden Backup-Einstellungen als Speicherort massgeblich bleiben. Technisch sauber ist ein Adapter, der ein normales Backup in die bestehende Backup-Verwaltung integriert und das Ergebnis samt Datei zurueckmeldet.

## Datenupdate-Begriff

Im Code gibt es keine separate Datenmigration, die in diesem Kontext "Datenupdate" heisst. Die vorhandene technische Anbindung interpretiert die Anforderung bereits als vollstaendige Datensicherung vor der Programminstallation.
