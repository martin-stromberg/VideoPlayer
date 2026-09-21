# Enums

## `ManualBackupJobStatus`
Datei: `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` (Zeilen 112–138)

| Wert | Bedeutung |
|------|-----------|
| `Idle` | Kein manueller Backup-Job bekannt |
| `Queued` | Job angenommen, wartet auf Ausführung |
| `Running` | Backup wird erstellt |
| `Succeeded` | Erfolgreich abgeschlossen |
| `Failed` | Fehlgeschlagen |

## `RestoreBackupJobStatus`
Datei: `VideoWebPlayer/Services/Backups/RestoreBackupJobService.cs` (Zeilen 158–184)

| Wert | Bedeutung |
|------|-----------|
| `Idle` | Kein Restore-Job bekannt |
| `Queued` | Job angenommen, wartet auf Ausführung |
| `Running` | Restore läuft |
| `Succeeded` | Erfolgreich abgeschlossen |
| `Failed` | Fehlgeschlagen |

## `BackupGeneration` (msTools.Backup)
Paket: `msTools.Backup` 1.1.0-RC.4 (`lib/packages/msTools.Backup.1.1.0-RC.4.nupkg`)

| Wert | Bedeutung |
|------|-----------|
| `Son` | GVS-Generation Sohn (täglich) |
| `Father` | GVS-Generation Vater (wöchentlich) |
| `Grandfather` | GVS-Generation Großvater (monatlich) |
| `Manual` | Manuell erstelltes Backup (verwendet in `VideoWebPlayerBackupFacade.CreateManualBackupAsync`) |
| `Uploaded` | Hochgeladenes Backup |
| `ProgramUpdate` | Backup vor Programm-Update |

## `BackupOperationType` (msTools.Backup)
Paket: `msTools.Backup` 1.1.0-RC.4

| Wert | Bedeutung |
|------|-----------|
| `Backup` | Backup-Erstellung |
| `Restore` | Wiederherstellung |
| `Delete` | Löschung |
| `Import` | Import/Upload |

Hinweis: `VideoWebPlayerBackupFacade` verwendet für Historieneinträge eigene Strings (`"Backup"`, `"Upload"`, `"Restore"`, `"Löschen"`), nicht `BackupOperationType`.

## `BackupJobState` (msTools.Backup)
Paket: `msTools.Backup` 1.1.0-RC.4 — interner Jobstatus der Bibliothek (`IBackupJobService`); im Anwendungscode nicht direkt verwendet. Enum-Werte sind in der Paket-Dokumentations-XML nicht ausgewiesen.
