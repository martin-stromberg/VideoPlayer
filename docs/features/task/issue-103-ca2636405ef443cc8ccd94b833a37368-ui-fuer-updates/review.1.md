# Plan-Review: UI fuer Updates

## Ergebnis

Die aktuelle Implementierung setzt den Umsetzungsplan weitgehend vollstaendig um. Datenmodell, EF-Migration, persistente Update-Settings, Runtime-Options-Anwendung, Admin-Service, Controller, Razor-UI, Navigation, Backup-Adapter, Dokumentation und die geplanten Kern-Tests sind vorhanden.

Es bleibt eine Planabweichung in der Testabdeckung: Der Controller validiert Antiforgery zur Laufzeit, aber der im Plan geforderte Testnachweis dafuer fehlt.

## Feststellungen

### 1. Fehlender Testnachweis fuer Antiforgery im UpdatesController

- Schweregrad: Niedrig
- Betroffen: `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs`
- Referenz: `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs:24`
- Planbezug: Abschnitt `Tests`, Punkt `UpdatesControllerAuthorizationTests`: "Controller validiert Antiforgery analog Backup-Controller."

Der Controller selbst ruft in beiden POST-Aktionen `IAntiforgery.ValidateRequestAsync(HttpContext)` auf. Die vorhandenen Tests pruefen jedoch nur `AdminOnly` und die POST-Routen. Damit ist das geplante Verhalten implementiert, aber nicht wie geplant abgesichert. Ein gezielter Test sollte verifizieren, dass `Check` und `Install` die Antiforgery-Validierung aufrufen.

## Abgleich Gegen Plan

- Datenmodell und Persistenz: Erfuellt. `UpdateSettings`, `DbSet`, EF-Konfiguration, Migration `AddUpdateSettings` und ModelSnapshot sind vorhanden.
- Runtime-Settings: Erfuellt. `UpdateSettingsService` erstellt Defaults aus Konfiguration, validiert/clamped Werte, persistiert Aenderungen und setzt `AutoUpdateOptions` inklusive Source-Neuerzeugung fuer Prerelease.
- Updater-Admin-Service: Erfuellt. Status-Snapshot, manuelle Pruefung, Download-vor-Install bei `UpdateAvailable`, Installationsschutz bei Busy/Locked/fehlender Version sind umgesetzt.
- Admin-Schutz: Erfuellt. `UpdatesController` nutzt `AdminOnly`, POST-Endpunkte und Antiforgery; die Razor-Seite prueft `IsAdmin=True`.
- UI: Erfuellt. `/admin/updates` zeigt Status, Aktionsbuttons, Settings-Formular, Prerelease-Bestaetigung, Polling und ist in der Navigation verlinkt.
- Backup-vor-Installation: Erfuellt. `BackupGeneration.ProgramUpdate` ist ergaenzt, `VideoWebPlayerUpdateBackupService` nutzt `msTools.Backup.IBackupService`, schreibt Historie, wendet Backup-Retention an und gibt den Descriptor-Pfad zurueck.
- Dynamische Backup-Optionen: Erfuellt. `UpdateBackupCoordinator` liest `IUpdateSettingsService.GetBackupOptionsAsync`.
- Dokumentation: Erfuellt. `docs/TECH_Auto_Update.md` und `README.md` enthalten die geplanten Hinweise.
- Tests: Groesstenteils erfuellt. Die Service-/Backup-/Coordinator-Tests sind vorhanden; der Antiforgery-Testnachweis fehlt.

## Ausgefuehrte Pruefung

- `dotnet test VideoWebPlayer.Tests\VideoWebPlayer.Tests.csproj`
- Ergebnis: 56/56 Tests bestanden.
- Hinweis: Es bleibt die bereits bekannte Warnung `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3`.

## Empfehlung

Vor dem Abschluss sollte die fehlende Antiforgery-Testabdeckung fuer `UpdatesController.Check` und `UpdatesController.Install` ergaenzt werden. Fachlich ist der Plan ansonsten umgesetzt.
