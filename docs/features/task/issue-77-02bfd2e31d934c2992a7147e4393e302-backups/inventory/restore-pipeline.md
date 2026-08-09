# Detail: Restore-Pipeline

## Ist-Zustand

1. `Backups.razor` ruft `VideoWebPlayerBackupFacade.RestoreAsync` direkt auf.
2. `VideoWebPlayerBackupFacade.RestoreAsync` ruft `IBackupService.RestoreBackupAsync`.
3. `BackupService.RestoreBackupAsync` validiert das ZIP, nimmt `IBackupRestoreGuard`, öffnet `index.json` und ruft `IBackupDataProvider.RestoreAsync`.
4. `VideoWebPlayerBackupDataProvider.RestoreAsync` löscht alle Tabellen und importiert Tabellen aus den referenzierten `entities/*.json`-Dateien.

## Erweiterungspunkte

- `BackupRestoreContext` kann um einen Fortschrittsreporter erweitert werden.
- `VideoWebPlayerBackupDataProvider` kann beim Wechsel der Tabelle und pro Datensatz Fortschritt melden.
- Ein neuer `RestoreBackupJobService` kann analog zu `ManualBackupJobService` die Facade scoped ausführen und Status halten.
- Eine Middleware kann anhand des Restore-Job-Status Inhaltsseiten und API-Requests zentral abfangen.
