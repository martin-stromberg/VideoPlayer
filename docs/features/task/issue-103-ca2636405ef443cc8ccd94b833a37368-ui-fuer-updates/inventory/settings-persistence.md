# Einstellungs- und Konfigurationspersistenz

## Vorhandene Konfigurationsquellen

Statische Konfiguration liegt in:

- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer/appsettings.Development.json`
- optional User-Secrets fuer `AutoUpdate:GitHubToken`

Persistente Admin-Einstellungen liegen aktuell in der SQLite-Datenbank ueber EF Core.

## ProgramSettingsService

`VideoWebPlayer/Services/ProgramSettingsService.cs` speichert scanbezogene Programmeinstellungen in der Tabelle `Setups`. Das Datenmodell ist `VideoWebPlayer/Data/Setup.cs`.

Vorhandene Felder:

- `DataVersion`
- `GenresChanged`
- `ScanProcessIntervalMinutes`
- `MediaCollectionScanIntervalDays`

Die Adminseite `ProgramSettings.razor` nutzt diesen Service und speichert ueber `UpdateScanIntervalsAsync`.

## BackupSettingsService

`VideoWebPlayer/Services/Backups/BackupSettingsService.cs` speichert Backup-Einstellungen in `BackupSettings`. Das Datenmodell ist `VideoWebPlayer/Data/BackupSettings.cs`.

Vorhandene Felder:

- `StoragePath`
- `AutomaticBackupsEnabled`
- `SonRetentionCount`
- `FatherRetentionCount`
- `GrandfatherRetentionCount`
- `MaxUploadSizeBytes`
- `UpdatedAtUtc`

Der Service implementiert `IBackupOptionsProvider` und mappt persistente Einstellungen zur Laufzeit auf `msTools.Backup.BackupOptions`.

## AutoUpdate-Konfiguration

`AutoUpdate` wird bisher nur aus `IConfiguration` gebunden. Wichtige aktuelle Werte:

- `Enabled`
- `EnableAutomaticDownload`
- `EnableAutomaticInstallation`
- `AllowPrereleaseUpdates`
- `DownloadPath`
- `SourceCheck.Interval`
- `Backup.Enabled`
- `Backup.Path`
- `Backup.RetainedBackupCount`
- `Backup.CancelInstallationOnFailure`

`AutoUpdateOptions` aus `msTools.Updater` ist laut XML-Dokumentation runtime-mutierbar und wird als Singleton registriert. Der Updater-Hintergrunddienst liest diese Optionen frisch pro Iteration. Das ist ein brauchbarer Ansatzpunkt fuer eine UI, die Einstellungen sofort wirksam macht.

## Fehlende Persistenz fuer Update-Einstellungen

Es gibt kein Datenmodell wie `UpdateSettings`, kein `DbSet<UpdateSettings>`, keine EF-Konfiguration/Migration und keinen `UpdateSettingsService`. Fuer die Anforderung sollte ein persistenter Settings-Service entstehen, der:

- Defaultwerte aus `IConfiguration` liest,
- eine einzelne Settings-Zeile anlegt, falls sie fehlt,
- Admin-Aenderungen validiert und speichert,
- `AutoUpdateOptions` und `UpdateBackupOptions` kontrolliert aktualisiert.

## Options-Aktualisierung

`UpdateBackupCoordinator` nutzt aktuell `IOptions<UpdateBackupOptions>` und liest `_options.Value`. Da `IOptions<T>` nicht fuer dynamische Aenderungen gedacht ist, braucht die Planung eine klare Entscheidung:

- entweder einen eigenen runtime-mutierbaren `UpdateBackupOptions`-Singleton/Accessor einfuehren,
- oder `IOptionsMonitor<T>` mit eigener Quelle verwenden,
- oder den Coordinator auf einen `UpdateSettingsService` umstellen, der die aktuellen Werte liest.

Der letzte Ansatz passt am besten zum bestehenden Muster `BackupSettingsService` und vermeidet halb-dynamische Konfigurationsbindung.
