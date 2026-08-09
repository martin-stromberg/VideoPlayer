# Code-Review: UI fuer Updates

## Status

Befunde vorhanden.

## Befunde

### 1. Update-Backup-Aufbewahrung greift fuer `ProgramUpdate`-Backups nicht

- Schweregrad: Mittel
- Betroffen:
  - `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs:35`
  - `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateBackupService.cs:46`
  - `msTools.Backup/BackupRetentionService.cs:19`
  - `msTools.Backup/BackupRetentionService.cs:21`
  - `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs:118`

Die unsichere eigene Coordinator-Retention aus `review-code.1.md` ist entfernt; der Coordinator loescht keine Dateien mehr pauschal im konfigurierten Zielverzeichnis. Der neue Adapter erstellt Backups aber mit `BackupGeneration.ProgramUpdate` und ruft danach `IBackupService.ApplyRetentionAsync` auf.

Die vorhandene `msTools.Backup`-Retention loescht aktuell nur die Generationen `Son`, `Father` und `Grandfather`. `Manual`, `Uploaded` und die neue Generation `ProgramUpdate` werden nicht betrachtet. Dadurch bleibt `UpdateSettings.RetainedUpdateBackupCount` zwar persistent und wird in `UpdateBackupOptions.RetainedBackupCount` gemappt, hat in der realen Produktivverdrahtung aber keine Wirkung auf die erzeugten Programmupdate-Backups.

Folge: Bei haeufigen Programmupdates koennen `ProgramUpdate`-Backups unbegrenzt anwachsen, obwohl die UI eine Aufbewahrung fuer Update-Backups anbietet. Die Tests erkennen das nicht, weil `VideoWebPlayerUpdateBackupServiceTests` nur verifiziert, dass `ApplyRetentionAsync` aufgerufen wird, nicht dass `ProgramUpdate`-Backups tatsaechlich nach `RetainedUpdateBackupCount` reduziert werden.

Empfehlung: Entweder `msTools.Backup` um eine sichere Retention fuer `BackupGeneration.ProgramUpdate` erweitern und die Update-Aufbewahrung dorthin durchreichen, oder die Update-spezifische Aufbewahrungsoption aus UI/Persistenz entfernen bzw. klar als nicht wirksam fuer die bestehende Backup-Infrastruktur behandeln. Wichtig ist, die alte pauschale Verzeichnisloeschung nicht wieder einzufuehren.

## Gepruefte vorherige Befunde

- Update-Backup-Retention loescht falsche Dateien: Behoben. `UpdateBackupCoordinator` erstellt nur noch das Zielverzeichnis und ruft den Backup-Provider auf; es gibt keine eigene Datei-Retention mehr.
- Deaktivierte automatische Pruefung deaktiviert manuelle Aktionen: Behoben. `UpdateSettingsService.ApplyToRuntimeOptions` setzt `AutoUpdateOptions.Enabled` fest auf `true` und sperrt nur die periodische Pruefung ueber leere Zeitfenster; `UpdateAdminService` ruft vor manuellen Aktionen weiterhin `ApplyToRuntimeOptionsAsync` auf.
- Antiforgery-Aufruf im `UpdatesController` ungetestet: Behoben. `UpdatesControllerAuthorizationTests` verifizieren fuer `Check` und `Install` jeweils `IAntiforgery.ValidateRequestAsync(HttpContext)`.
- Kritische Backup-/Disabled-Testluecken: Weitgehend behoben. Es gibt Tests fuer keine Coordinator-Retention im Zielverzeichnis, fuer `ProgramUpdate` im Backup-Adapter, fuer deaktivierte automatische Pruefung mit aktivem Updater-Subsystem und fuer Antiforgery. Offen bleibt der oben genannte Test-/Integrationsnachweis fuer echte `ProgramUpdate`-Retention.

## Unauffaellige Bereiche

- `UpdatesController` ist mit `AdminOnly` geschuetzt, nutzt POST-Endpunkte und validiert Antiforgery vor den manuellen Aktionen.
- Die Razor-Seite erzeugt Antiforgery-Tokens nach dem bereits vorhandenen Muster der Backup-Seite.
- Manuelle Check-/Install-Aktionen pruefen Busy-/Lock-Zustaende serverseitig erneut.
- Die EF-Migration fuer `UpdateSettings` ist vorhanden; `Program.cs` ruft `app.MigrateDatabase()` vor dem Start der Anwendung auf.
- `BackupGeneration.ProgramUpdate` ist in `msTools.Backup` ergaenzt und wird in der Backup-Historie als eigene Generation sichtbar.

## Ausgefuehrte Pruefung

- Statischer Code-Review der aktuellen Arbeitsbaum-Aenderungen nach der zweiten Iteration.
- Abgleich mit den Befunden aus `review-code.1.md`.
- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj`
  - Ergebnis: 60/60 Tests bestanden.
  - Bekannte Warnung bleibt: `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3`.
