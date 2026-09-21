# Interfaces

## `IBackupService` (msTools.Backup)
Paket: `msTools.Backup` 1.1.0-RC.4 (`lib/packages/msTools.Backup.1.1.0-RC.4.nupkg`); injiziert in `BackupsController` und `VideoWebPlayerBackupFacade`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListBackupsAsync` | `CancellationToken` | `Task` (Liste von `BackupDescriptor`) | Vorhandene Backups auflisten |
| `OpenBackupReadAsync` | `string fileName`, `CancellationToken` | `Task` (`Stream`) | Backup-Datei lesend öffnen (Download) |
| `DeleteBackupAsync` | `string fileName`, `CancellationToken` | `Task` (`BackupOperationResult`) | Backup löschen |
| `StoreAsync` | `string name`, `BackupGeneration`, `IEnumerable<IBackupData>`, `CancellationToken` | `Task` (`BackupResult`) | Backup speichern |
| `RestoreAsync` | `string fileName`, `IBackupDataFactory`, `CancellationToken` | `Task` | Backup wiederherstellen |
| `ApplyRetentionAsync` | `CancellationToken` | `Task` | GVS-Aufbewahrung anwenden |

## `IBackupStore` (msTools.Backup)
Dateisystem-Speicherabstraktion (`FileSystemBackupStore`-Implementierung); von `IBackupService` verwendet, im Anwendungscode nicht direkt injiziert.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListAsync` | `CancellationToken` | `Task` | Backups auflisten |
| `StoreAsync` | `string`, `BackupGeneration`, `IEnumerable<IBackupData>`, `CancellationToken` | `Task` | Backup-Objekte in benannter Datei speichern |
| `OpenReadAsync` | `string`, `CancellationToken` | `Task` | Backup lesend öffnen |
| `DeleteAsync` | `string`, `CancellationToken` | `Task` | Backup löschen |
| `RestoreAsync` | `string`, `IBackupDataFactory`, `CancellationToken` | `Task` | Backup wiederherstellen |

## `IBackupOptionsProvider` (msTools.Backup)
Implementiert von `BackupSettingsService` (`VideoWebPlayer/Services/Backups/BackupSettingsService.cs`); injiziert in `VideoWebPlayerBackupFacade`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetOptionsAsync` | `CancellationToken` | `Task<BackupOptions>` | Laufzeit-Backupoptionen (u. a. `StoragePath`, `MaxUploadSizeBytes`) liefern |

## `IBackupDataSource` (msTools.Backup)
Implementiert von `VideoWebPlayerBackupDataSource` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataSource.cs`); liefert `IBackupData`-Objekte für Backups.

## `IBackupData` / `IBackupDataFactory` (msTools.Backup)
`IBackupData` implementiert von `VideoWebPlayerBackupData` (`WriteToAsync`/`ReadFromAsync`, objektbasiertes ZIP-Archiv mit `index.json`); `IBackupDataFactory` implementiert von `VideoWebPlayerBackupDataFactory` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs`, besitzt `UserId`- und `Progress`-Eigenschaften für Restore).

## `IBackupOperationHistory` (msTools.Backup)
Registriert als `InMemoryBackupOperationHistory` (`ServiceCollectionExtensions.cs` Zeile 246); zusätzlich existiert die eigene persistente `BackupOperationHistoryService`/`BackupOperationHistory`-Tabelle.

## `IBackupRestoreGuard` (msTools.Backup)
Implementiert von `VideoWebPlayerBackupRestoreGuard` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupRestoreGuard.cs`); Restore-Schutz (Singleton).

## `IAutomaticBackupRunner` (msTools.Backup)
Implementiert von `VideoWebPlayerAutomaticBackupRunner` (`VideoWebPlayer/Services/Backups/VideoWebPlayerAutomaticBackupRunner.cs`).

## `IBackupJobService` (msTools.Backup)
In der Bibliothek (`BackgroundBackupJobService` als In-Memory-Implementierung); in der Anwendung nicht direkt registriert — Job-Ausführung läuft über eigene Singletons `ManualBackupJobService`/`RestoreBackupJobService`.

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`; Implementierung `BackgroundProcessingGate` (Singleton). In `Backups.razor` injiziert (`IsPausedForRestore`-Anzeige).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsPausedForRestore` | — | `bool` | Restore-Modus blockiert neue Operationen |
| `ActiveOperationCount` | — | `int` | Anzahl aktiver Operationen |
| `EnterOperationAsync` | `string name`, `CancellationToken` | `Task<IAsyncDisposable>` | Schreibende Hintergrundoperation eintragen |
| `PauseForRestoreAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Neue Operationen pausieren und aktive abwarten |

## `IAntiforgery` (ASP.NET Core)
In `BackupsController` injiziert; `ValidateRequestAsync(HttpContext)` wird in `Create` und `Upload` manuell aufgerufen (Token bisher als Formularfeld `__RequestVerificationToken`; `AddAntiforgery` konfiguriert keinen eigenen Header-Namen → Default `RequestVerificationToken` für Header-Validierung). In `Backups.razor` wird `Antiforgery.GetAndStoreTokens(httpContext).RequestToken` erzeugt.
