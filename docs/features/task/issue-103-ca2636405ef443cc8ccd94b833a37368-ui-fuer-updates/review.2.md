# Plan-Review: UI fuer Updates

## Status

Vollstaendig umgesetzt.

## Ergebnis

Die aktuelle Implementierung nach der zweiten Iteration setzt den Plan vollstaendig um. Es sind keine offenen Planelemente verblieben.

Die zuvor gemeldete Planabweichung zur Antiforgery-Testabdeckung ist behoben: `UpdatesControllerAuthorizationTests` enthaelt jetzt explizite Tests fuer `Check` und `Install`, die den Aufruf von `IAntiforgery.ValidateRequestAsync(HttpContext)` verifizieren.

## Gepruefte Planpunkte

- Datenmodell und Persistenz: `UpdateSettings`, `ApplicationDbContext`, EF-Konfiguration, Migration `AddUpdateSettings` und ModelSnapshot sind vorhanden.
- Runtime-Settings: `UpdateSettingsService` legt Defaults aus `IConfiguration` an, speichert Admin-Aenderungen, clampet Grenzwerte und wendet die Werte auf runtime-mutierbare Updater-Optionen an.
- Manuelle Update-Administration: `UpdateAdminService` buendelt Status, Settings und manuelle Aktionen; laufende oder gelockte Zustaende blockieren parallele Aktionen.
- Admin-Endpunkte: `UpdatesController` ist mit `AdminOnly` geschuetzt, stellt POST-Endpunkte fuer `check` und `install` bereit und validiert Antiforgery serverseitig.
- UI: `Updates.razor` stellt Status, Detailwerte, manuelle Aktionen, Settings-Formular, Prerelease-Bestaetigung und Polling bereit; `NavMenu.razor` verlinkt die Seite im Adminbereich.
- Backup vor Installation: `VideoWebPlayerUpdateBackupService` nutzt `msTools.Backup.IBackupService` mit `BackupGeneration.ProgramUpdate`, schreibt Backup-Historie und verwendet die bestehende Backup-Retention.
- Zweite Iteration: Die unsichere eigene Retention im `UpdateBackupCoordinator` ist entfernt; `AutomaticChecksEnabled=false` deaktiviert das Update-Subsystem nicht mehr global, sodass manuelle Aktionen moeglich bleiben.
- Dokumentation: `docs/TECH_Auto_Update.md` und `README.md` sind aktualisiert.

## Testnachweis

Ausgefuehrt:

```text
dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj
```

Ergebnis:

```text
Bestanden!   : Fehler:     0, erfolgreich:    60, uebersprungen:     0, gesamt:    60
```

Bekannte Warnung:

- `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

## Offene Aufgaben

Keine.
