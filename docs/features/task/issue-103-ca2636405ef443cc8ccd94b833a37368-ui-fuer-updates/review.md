# Plan-Review: UI fuer Updates

## Status

Vollstaendig umgesetzt.

## Ergebnis

Die aktuelle Implementierung setzt den Plan vollstaendig um. Es sind keine offenen Planelemente verblieben.

Die nachtraegliche Korrektur fuer `ProgramUpdate`-Retention ist enthalten und plan-konform umgesetzt: Update-Backups werden weiterhin ueber die bestehende `msTools.Backup`-Infrastruktur erstellt, `BackupGeneration.ProgramUpdate` ist als eigene Generation vorhanden, `BackupRetentionOptions.ProgramUpdateCount` steuert die Aufbewahrung, und `BackupSettingsService` mappt `UpdateSettings.RetainedUpdateBackupCount` in diese Retention-Option. Der `UpdateBackupCoordinator` fuehrt keine eigene pauschale Verzeichnis-Retention aus.

## Gepruefte Planpunkte

- Datenmodell und Persistenz: `UpdateSettings`, `ApplicationDbContext`, EF-Konfiguration, Migration `AddUpdateSettings` und ModelSnapshot sind vorhanden.
- Migrationspfad: Die Migration liegt regulaer unter `VideoWebPlayer/Migrations/`; die Anwendung fuehrt Datenbankmigrationen wie geplant beim Start ueber `app.MigrateDatabase()` aus.
- Runtime-Settings: `UpdateSettingsService` legt Defaults aus `IConfiguration` an, speichert Admin-Aenderungen, clampet Grenzwerte und wendet die Werte auf runtime-mutierbare Updater-Optionen an.
- Manuelle Update-Administration: `UpdateAdminService` buendelt Status, Settings und manuelle Aktionen; laufende oder gelockte Zustaende blockieren parallele Aktionen.
- Admin-Endpunkte: `UpdatesController` ist mit `AdminOnly` geschuetzt, stellt POST-Endpunkte fuer `check` und `install` bereit und validiert Antiforgery serverseitig.
- UI: `Updates.razor` stellt Status, Detailwerte, manuelle Aktionen, Settings-Formular, Prerelease-Bestaetigung und Polling bereit; `NavMenu.razor` verlinkt die Seite im Adminbereich.
- Backup vor Installation: `VideoWebPlayerUpdateBackupService` nutzt `msTools.Backup.IBackupService` mit `BackupGeneration.ProgramUpdate`, schreibt Backup-Historie und ruft die bestehende Backup-Retention auf.
- ProgramUpdate-Retention: `msTools.Backup.BackupRetentionService` beruecksichtigt `BackupGeneration.ProgramUpdate` und loescht nur abgelaufene Backups dieser Generation entsprechend `ProgramUpdateCount`; `Manual`- und Upload-Backups bleiben davon unberuehrt.
- Vorherige Review-Befunde: Die unsichere eigene Retention im `UpdateBackupCoordinator` ist entfernt; `AutomaticChecksEnabled=false` deaktiviert das Update-Subsystem nicht mehr global, sodass manuelle Aktionen moeglich bleiben; Antiforgery wird getestet.
- Dokumentation: `docs/TECH_Auto_Update.md` und `README.md` sind aktualisiert.

## Testnachweis

Ausgefuehrt am 2026-08-09:

```text
dotnet test msTools.Backup.Tests\msTools.Backup.Tests.csproj
```

Ergebnis:

```text
Bestanden!   : Fehler:     0, erfolgreich:    14, uebersprungen:     0, gesamt:    14
```

Ausgefuehrt am 2026-08-09:

```text
dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj
```

Ergebnis:

```text
Bestanden!   : Fehler:     0, erfolgreich:    61, uebersprungen:     0, gesamt:    61
```

Bekannte Warnung:

- `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

## Offene Aufgaben

Keine.
