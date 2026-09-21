# Datenmodell

## `BackupSettings`
Datei: `VideoWebPlayer/Data/BackupSettings.cs`

Persistierte, administrierbare Backup-Einstellungen (EF-Core-Entity, `ApplicationDbContext.BackupSettings`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Settings-Zeilen-ID |
| `StoragePath` | `string` | Backup-Speicherpfad, Default `Data/Backups` |
| `AutomaticBackupsEnabled` | `bool` | Automatische Backups aktiv |
| `SonRetentionCount` | `int` | GVS-Aufbewahrung Sohn, Default 7 |
| `FatherRetentionCount` | `int` | GVS-Aufbewahrung Vater, Default 4 |
| `GrandfatherRetentionCount` | `int` | GVS-Aufbewahrung Großvater, Default 12 |
| `MaxUploadSizeBytes` | `long` | Anwendungsseitiges Upload-Limit, Default 5 GiB (`5L * 1024³`); wird aktuell nur in der UI angezeigt, nicht serverseitig durchgesetzt |
| `UpdatedAtUtc` | `DateTime` | Zeitstempel der letzten Änderung |

## `BackupOperationHistory`
Datei: `VideoWebPlayer/Data/BackupOperationHistory.cs`

Audit-Historie für Backup-/Restore-/Lösch-/Upload-Operationen (`ApplicationDbContext.BackupOperationHistories`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Zeilen-ID |
| `StartedAtUtc` | `DateTime` | Startzeitpunkt |
| `CompletedAtUtc` | `DateTime?` | Endzeitpunkt |
| `Operation` | `string` | Operationsname (z. B. `"Backup"`, `"Upload"`, `"Restore"`, `"Löschen"`) |
| `FileName` | `string?` | Betroffener Dateiname |
| `Generation` | `string?` | Backup-Generation (aus `BackupDescriptor`) |
| `Succeeded` | `bool` | Erfolgskennzeichen |
| `UserId` | `string?` | Admin-Benutzer-ID |
| `Message` | `string` | Anwenderlesbare Meldung |

## `SettingsModel` (privat, in Komponente)
Datei: `VideoWebPlayer/Components/Pages/Admin/Backups.razor` (Zeilen 672–690)

Formularmodell für die Einstellungs-`EditForm`; spiegelt `BackupSettings`. `MaxUploadSizeBytes` hat hier den Default `512L * 1024L * 1024L` und `[Range(1048576, long.MaxValue)]`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `StoragePath` | `string` | `[Required]`, Default `Data/Backups` |
| `AutomaticBackupsEnabled` | `bool` | Toggle automatische Backups |
| `SonRetentionCount` / `FatherRetentionCount` / `GrandfatherRetentionCount` | `int` | `[Range(0, 365)]` |
| `MaxUploadSizeBytes` | `long` | `[Range(1048576, long.MaxValue)]`, Default 512 MiB |

## msTools.Backup-Typen (NuGet `msTools.Backup` 1.1.0-RC.4, `lib/packages/msTools.Backup.1.1.0-RC.4.nupkg`)

### `BackupOptions`
Vom `IBackupOptionsProvider` gelieferte Laufzeitoptionen; `BackupSettingsService.GetOptionsAsync` mappt `BackupSettings` darauf.

| Eigenschaft | Zweck |
|-------------|-------|
| `StoragePath` | Speicherpfad für Backups |
| `MaxUploadSizeBytes` | Upload-Limit (wird durch `BackupSettingsService` aus `BackupSettings` befüllt) |
| `AutomaticBackupsEnabled` | Automatische Backups aktiv |
| `Schedule` | `BackupScheduleOptions` (Intervalle/Frequenzen) |
| `Retention` | `BackupRetentionOptions` (GVS- + ProgramUpdate-Zähler) |

### `BackupOperationResult`
Rückgabetyp der `VideoWebPlayerBackupFacade`-Methoden.

| Eigenschaft | Zweck |
|-------------|-------|
| `Succeeded` | Erfolgskennzeichen |
| `Message` | Meldung |
| `Descriptor` | Zugehöriger `BackupDescriptor` (optional) |
| `Errors` | Fehlerliste |

Statische Fabriken: `BackupOperationResult.Success(...)`, `BackupOperationResult.Failure(...)` (verwendet in `VideoWebPlayerBackupFacade`).

### `BackupDescriptor`
Metadaten eines gespeicherten Backups; wird von `IBackupService.ListBackupsAsync` geliefert und in `Backups.razor` angezeigt (`FileName`, `CreatedAtUtc`, `SizeBytes`, `IsValid`, `ValidationErrors`, `Path`, `Generation`).

### `BackupRestoreProgress`
Datei: `VideoWebPlayer/Services/Backups/BackupRestoreProgress.cs`

Record für zweistufigen Restore-Fortschritt (`DataSetName`, `DataSetNumber`, `DataSetTotal`, `RecordNumber`, `RecordTotal`, `Message`); wird per `IProgress<BackupRestoreProgress>` aus `VideoWebPlayerBackupFacade.RestoreAsync` an `RestoreBackupJobService` gemeldet.
