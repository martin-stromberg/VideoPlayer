# Teststruktur und relevante vorhandene Tests

## Testprojekte

Vorhandene Testprojekte:

- `VideoWebPlayer.Tests`
- `msTools.Backup.Tests`
- `VideoWebPlayer.Maui.Tests`

Die Tests nutzen xUnit v3, Moq, EF Core InMemory/Sqlite und `Microsoft.Extensions.Logging.Abstractions`.

## Relevante vorhandene Tests

### Update-Backup

- `VideoWebPlayer.Tests/Services/UpdateBackupCoordinatorTests.cs`
- `VideoWebPlayer.Tests/Services/UpdateBackupEventBinderTests.cs`

Diese Tests pruefen bereits:

- deaktivierte Update-Backups,
- Verhalten bei fehlendem `IUpdateBackupService`,
- erfolgreiche Sicherung,
- Retention,
- Fehlerfall und Installationsabbruch ueber das BeforeInstall-Event.

### Backup-Einstellungen und Jobs

- `VideoWebPlayer.Tests/BackupSettingsServiceTests.cs`
- `VideoWebPlayer.Tests/VideoWebPlayerAutomaticBackupRunnerTests.cs`
- `VideoWebPlayer.Tests/RestoreBackupJobServiceTests.cs`
- `VideoWebPlayer.Tests/RestoreInProgressMiddlewareTests.cs`
- `VideoWebPlayer.Tests/VideoWebPlayerBackupDataProviderTests.cs`

Diese Tests zeigen Muster fuer EF-basierte Settings-Services, Hintergrundjobs und Backup-Fassade.

### Admin-Autorisierung

- `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs`

Dieser Test prueft per Reflection, dass `BackupsController` die Policy `AdminOnly` nutzt und die Backup-Endpunkte korrekt geroutet sind. Fuer Update-Endpunkte kann dieses Muster direkt uebernommen werden.

## Luecken fuer neue Tests

Fuer die Update-UI-Anforderung fehlen Tests fuer:

- einen neuen `UpdateSettingsService`, der Defaults aus `IConfiguration` liest und DB-Werte persistiert,
- Validierung/Clamping des Pruefintervalls in Minuten,
- Aktualisierung von `AutoUpdateOptions` aus persistenten Einstellungen,
- Aktualisierung der Update-Backup-Optionen aus persistenten Einstellungen,
- Prerelease-Aktivierung nur nach expliziter bestaetigter Sicherheitsabfrage auf UI-/Service-Ebene,
- Admin-Schutz fuer neue Update-Controller-Endpunkte,
- manuelle Check-/Install-Aktionen gegen `IAutoUpdateCommandHandler` oder `IAutoUpdateOrchestrator`,
- Button-/Concurrency-Logik mindestens service-/controllerseitig, sofern UI-Tests nicht vorhanden sind,
- Adapter von `IUpdateBackupService` auf `msTools.Backup.IBackupService`.

## Testlauf

Im Rahmen dieser Bestandsaufnahme wurden keine Tests ausgefuehrt; es wurden nur Struktur und vorhandene Tests analysiert.
