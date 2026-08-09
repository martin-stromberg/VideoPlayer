# Code-Review: UI fuer Updates

## Status

Befunde vorhanden.

## Befunde

### 1. Manuelle Installation laeuft nach abgebrochenem/uebersprungenem Download weiter

- Schweregrad: Mittel
- Betroffen:
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:82`
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:84`
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:85`
  - `VideoWebPlayer/Services/Updates/UpdateAdminService.cs:89`
  - `VideoWebPlayer.Tests/Services/UpdateAdminServiceTests.cs:42`

`InstallAsync` startet bei Status `UpdateAvailable` zuerst `DownloadAsync`, prueft danach aber nur `AutoUpdateOutcome.Failed`. Bei `Skipped` oder `Canceled` wird trotzdem `InstallAsync(true, ...)` aufgerufen. Laut lokaler `msTools.Updater`-Dokumentation bedeutet `Skipped`, dass der naechste automatische Schritt deaktiviert ist, und `Canceled`, dass ein Event-Subscriber die Operation abgebrochen hat. Beide Faelle sind keine belastbare Grundlage fuer die direkt anschliessende Installation.

Das kann zu fehlerhaften oder irrefuehrenden manuellen Installationsversuchen fuehren, z. B. wenn ein Download per Event abgebrochen wird oder der Updater den Download bewusst ueberspringt. Die vorhandenen Tests decken nur den erfolgreichen Download-Happy-Path und die Blockade ohne installierbare Version ab; ein Test fuer `Skipped`/`Canceled` nach `DownloadAsync` fehlt.

Empfehlung: Nach `DownloadAsync` nur bei `AutoUpdateOutcome.Success` fortfahren oder alternativ `ToActionResult(download)` fuer alle nicht erfolgreichen Download-Ergebnisse zurueckgeben. Dazu Tests ergaenzen, die bei `Skipped` und `Canceled` verifizieren, dass `InstallAsync` nicht aufgerufen wird.

### 2. Update-Backup-Pfad kann Installation unnoetig blockieren, obwohl der reale Adapter ihn nicht nutzt

- Schweregrad: Mittel
- Betroffen:
  - `VideoWebPlayer/Components/Pages/Admin/Updates.razor:145`
  - `VideoWebPlayer/Components/Pages/Admin/Updates.razor:146`
  - `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs:59`
  - `VideoWebPlayer/Services/Updates/UpdateBackupCoordinator.cs:63`
  - `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs:35`
  - `VideoWebPlayer/Services/Updates/UpdateBackupOptions.cs:20`

Die UI bietet weiterhin `Update-Backup-Pfad` als Einstellung an, und `UpdateBackupOptions.Path` beschreibt diesen Wert als Speicherort der Backups. In der produktiven Standardverdrahtung delegiert `VideoWebPlayerUpdateBackupService` aber an `msTools.Backup.IBackupService.CreateBackupAsync(...)`; der erzeugte Speicherort kommt damit aus der bestehenden Backup-Infrastruktur, analog zum manuellen Web-Backup, und nicht aus `UpdateBackupRequest.TargetDirectory`.

Trotzdem legt `UpdateBackupCoordinator` vor dem Adapter-Aufruf immer das konfigurierte Zielverzeichnis an. Ein ungueltiger oder nicht beschreibbarer `UpdateBackupPath` kann dadurch die Installation abbrechen, obwohl der registrierte Adapter dieses Verzeichnis fuer das eigentliche Backup gar nicht benoetigt. Damit passen UI, Options-Dokumentation und Service-Verhalten nicht sauber zusammen.

Empfehlung: Entweder den Update-Backup-Pfad aus UI/Persistenz entfernen bzw. klar als nur fuer alternative Provider ausweisen, oder die Zielverzeichnis-Erstellung in den jeweiligen Provider verschieben. Fuer den aktuellen `msTools.Backup`-Adapter sollte ein defekter `UpdateBackupPath` nicht verhindern, dass das normale Backup ueber die bestehende Backup-Konfiguration erstellt wird. Ein Regressionstest sollte den Fall abdecken, dass der Standardadapter den Pfad nicht benoetigt.

## Gepruefte vorherige Befunde

- Unsichere verzeichnisbasierte Retention im `UpdateBackupCoordinator`: Behoben. Der Coordinator loescht keine Dateien mehr nach `LastWriteTimeUtc`; er ruft nur den Backup-Provider auf.
- `ProgramUpdate`-Retention in `msTools.Backup`: Behoben. `BackupRetentionOptions.ProgramUpdateCount` ist vorhanden, `BackupRetentionService` loescht abgelaufene `BackupGeneration.ProgramUpdate`-Descriptoren, und `BackupSettingsService` mappt `UpdateSettings.RetainedUpdateBackupCount` auf diese Option.
- Deaktivierte automatische Hintergrundpruefung deaktiviert manuelle Aktionen: Behoben. `UpdateSettingsService` setzt `AutoUpdateOptions.Enabled` weiterhin auf `true` und schliesst nur die periodischen Check-Zeitfenster; der Test mit `SourceCheckWindowEvaluator` bestaetigt das.
- Controller-Autorisierung und Antiforgery: Behoben. `UpdatesController` ist mit `AdminOnly` geschuetzt, `check`/`install` sind POST-Endpunkte, und die Tests verifizieren `IAntiforgery.ValidateRequestAsync(HttpContext)` fuer beide Aktionen.

## Unauffaellige Bereiche

- EF-Artefakte passen zusammen: `UpdateSettings`, Entity-Konfiguration, `ApplicationDbContext`, Migration `20260809163317_AddUpdateSettings` und `ApplicationDbContextModelSnapshot` enthalten dieselben Spalten/Laengen.
- Startup-Reihenfolge ist plausibel: `Program.cs` ruft `app.MigrateDatabase()` vor `app.Run()` auf; `UpdateSettingsInitializer` laeuft als Hosted Service erst danach.
- Backup vor Installation nutzt `BackupGeneration.ProgramUpdate`, schreibt Backup-Historie und ruft die zentrale `msTools.Backup`-Retention auf.
- Navigation und Razor-Seite sind adminseitig eingebunden; der Controller bleibt die serverseitige Absicherung fuer manuelle Aktionen.

## Ausgefuehrte Pruefung

- Statischer Code-Review der aktuellen Arbeitsbaum-Aenderungen und Abgleich mit `review-code.1.md` sowie `review-code.2.md`.
- `dotnet test msTools.Backup.Tests\msTools.Backup.Tests.csproj`
  - Ergebnis: 14/14 Tests bestanden.
- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj`
  - Ergebnis: 61/61 Tests bestanden.
  - Bekannte Warnung bleibt: `NU1903` fuer `SQLitePCLRaw.lib_e_sqlite3`/`SQLitePCLRaw.lib.e_sqlite3`.
