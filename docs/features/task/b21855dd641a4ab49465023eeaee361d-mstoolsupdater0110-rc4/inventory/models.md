# Datenmodell

## `UpdateSettings`

Datei: `VideoWebPlayer/Data/UpdateSettings.cs`

Entity für die Singleton-Zeile der administrativ änderbaren Update-Einstellungen
(`ApplicationDbContext.UpdateSettings`, Migration `20260809163317_AddUpdateSettings`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel, immer `1` (Singleton). |
| `AutomaticChecksEnabled` | `bool` | Automatische Update-Prüfung aktiv (Default `true`). |
| `CheckIntervalMinutes` | `int` | Prüfintervall in Minuten (Default `360`, Clamp 1–1440). |
| `AllowPrereleaseUpdates` | `bool` | Prerelease-Versionen akzeptieren. |
| `AutomaticInstallationEnabled` | `bool` | Gefundene Updates automatisch installieren. |
| `AutomaticDownloadEnabled` | `bool` | Updates automatisch herunterladen (Default `true`; bei aktivierter Auto-Installation implizit `true`). |
| `ServiceName` | `string?` | Dienstname für den Neustart (max. 200 Zeichen; unter IIS persistiert `null`). |
| `CreateBackupBeforeInstallation` | `bool` | Backup vor der Installation erstellen (Default `true`). |
| `CancelInstallationOnBackupFailure` | `bool` | Installation bei Backup-Fehler abbrechen (Default `true`). |
| `UpdateBackupPath` | `string` | Zielverzeichnis für Update-Backups (max. 1024 Zeichen, required, Default `"Backups"`). |
| `RetainedUpdateBackupCount` | `int` | Anzahl aufzubewahrender Update-Backups (Clamp 1–10, Default `5`). |
| `UpdatedAtUtc` | `DateTime` | Zeitpunkt der letzten Änderung. |

EF-Konfiguration: `VideoWebPlayer/Data/Configurations/UpdateSettingsConfiguration.cs` —
`HasKey(Id)`, `ServiceName` max. 200, `UpdateBackupPath` max. 1024 required.

Backup-Registrierung: `"UpdateSettings"` steht in `VideoWebPlayerBackupData.OptionalRestoreTables`
(`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`, Z. 25) — Alt-Backups ohne die
Tabelle bleiben wiederherstellbar. `BackupSettingsService` (Z. 114–116) liest die Zeile 1 beim
Restore-bezogenen Settings-Abgleich.

## Daten-Records (Update-Subsystem)

| Record | Datei | Zweck |
|--------|-------|-------|
| `UpdateSettingsUpdate` | `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` (Z. 275) | DTO der Admin-Eingaben: 10 Felder (`AutomaticChecksEnabled`, `CheckIntervalMinutes`, `AllowPrereleaseUpdates`, `AutomaticInstallationEnabled`, `AutomaticDownloadEnabled`, `ServiceName`, `CreateBackupBeforeInstallation`, `CancelInstallationOnBackupFailure`, `UpdateBackupPath`, `RetainedUpdateBackupCount`). |
| `UpdateAdminSnapshot` | `VideoWebPlayer/Services/Updates/UpdateAdminService.cs` (Z. 148) | Bündelt `Settings`, `Status` (`AutoUpdateStatusSnapshot` aus der Bibliothek) und `DefaultSettings` für die Admin-UI. |
| `UpdateAdminActionResult` | `VideoWebPlayer/Services/Updates/UpdateAdminService.cs` (Z. 157) | Ergebnis einer manuellen Aktion (`Succeeded`, `IsBlocked`, `Message`) mit Factory-Methoden `Success`/`Blocked`/`Failed`. |
| `UpdateBackupRequest` | `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs` (Z. 24) | `TargetDirectory` (absoluter Pfad) + `Reason` für den Backup-Aufruf. |
| `UpdateBackupResult` | `VideoWebPlayer/Services/Updates/IUpdateBackupService.cs` (Z. 32) | `Succeeded`, `BackupFilePath`, `Message` mit Factory-Methoden `Success`/`Failure`. |

## Options-Klassen

| Klasse | Datei | Zweck |
|--------|-------|-------|
| `UpdateBackupOptions` | `VideoWebPlayer/Services/Updates/UpdateBackupOptions.cs` | Binding der Sektion `AutoUpdate:Backup` (`SectionName`-Konstante): `Enabled` (true), `Path` (`"Backups"`), `RetainedBackupCount` (5), `CancelInstallationOnFailure` (true). Wird zusätzlich aus `UpdateSettings` über `IUpdateSettingsService.GetBackupOptionsAsync` befüllt. |

Keine Datenmodell- oder EF-Migrationsänderung durch das Bibliotheks-Upgrade zu erwarten —
die `UpdateSettings`-Tabelle und ihre Konfiguration sind unabhängig von der Paketversion.
