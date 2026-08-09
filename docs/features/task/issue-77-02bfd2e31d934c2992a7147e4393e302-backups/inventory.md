# Bestandsaufnahme

## Relevante Komponenten

- `msTools.Backup.BackupService`
  - `RestoreBackupAsync` validiert ZIP, nimmt den Restore-Guard und ruft `IBackupDataProvider.RestoreAsync` synchron im aufrufenden Task auf.
- `msTools.Backup.IBackupDataProvider`
  - Stellt `ExportAsync`, `ValidateAsync`, `RestoreAsync` bereit.
  - `BackupRestoreContext` enthält aktuell UserId und Payload-Entry-Resolver.
- `VideoWebPlayerBackupDataProvider`
  - Restore liest `index.json`, validiert Entitätsdateien, leert Tabellen und importiert Tabellen/Zeilen.
  - ZIP-Payload nutzt bereits `index.json` plus `entities/*.json`.
  - Fortschritt wird aktuell nicht gemeldet.
- `VideoWebPlayerBackupFacade`
  - `RestoreAsync` ruft den Backup-Service auf und schreibt Historie.
- `ManualBackupJobService`
  - Vorbild für Background-Job-Status beim manuellen Backup.
- `BackgroundProcessingGate` und `VideoWebPlayerBackupRestoreGuard`
  - Blockieren Hintergrund-Schreibprozesse während Restore, sperren aber keine UI-/API-Inhaltsrequests.
- `Backups.razor`
  - Restore läuft aktuell direkt über Blazor-Event und blockiert bis Abschluss.
  - Manuelles Backup hat bereits Polling und Statusanzeige.
- `WebApplicationExtensions.UseVideoWebPlayer`
  - Zentraler Ort für Middleware vor Razor-Komponenten und Controllern.
- API-Controller
  - `ItemsController`, `PicturesController`, `SourcesController`, `FavoritesController`, `ContinueWatchingController`, `SourceGenresController`, `SourceIconsController` liefern Inhaltsdaten.

## Testbestand

- `VideoWebPlayerBackupDataProviderTests`
  - Decken Export/Restore, getrennte Entitätsdateien, Admin-Erhalt und Dateirestore ab.
- `BackgroundProcessingGateTests`
  - Decken Restore-Exklusivität für Hintergrundoperationen ab.
- `BackupsControllerAuthorizationTests`
  - Prüfen Admin-Schutz und Routen.
- `msTools.Backup.Tests`
  - Decken ZIP-Store, Manifest und Payload-Validierung ab.

## Risiken

- Restore-Fortschritt muss ohne vollständiges Laden aller Datenbestände funktionieren; für `y von z` wird pro Entitätsdatei ein Zeilenzähler benötigt.
- UI-/API-Sperre darf Admin-Backup-Seite, Downloads/Status und statische Assets nicht sperren.
- Restore-Guard darf weiterhin nur die eigentliche Restore-Phase schützen, parallele Restores müssen im Job-Service verhindert werden.
