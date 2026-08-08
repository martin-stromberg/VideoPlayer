# Bestandsaufnahme: Backups

## Kurzfazit

Die Backup-Anforderung betrifft primaer das aktuelle Webprojekt `VideoWebPlayer`. Die Anwendung ist eine ASP.NET Core Blazor Server App auf `net10.0` mit EF Core/SQLite, ASP.NET Core Identity und mehreren Hintergrundprozessen, die waehrend eines Restores Daten veraendern koennen. Eine wiederverwendbare Bibliothek `msTools.Backup` existiert noch nicht.

Die beste Integration ist eine neue Klassenbibliothek plus schmale Host-Adapter im Webprojekt:

- `msTools.Backup`: dateibasierte Backup-Verwaltung, ZIP-Format, GVS-Aufbewahrung, Services, Optionen, Interfaces fuer Export/Restore.
- `VideoWebPlayer`: Implementierung des Backup-Datenproviders fuer `ApplicationDbContext`, Admin-UI, Admin-Autorisierung, Restore-Koordination fuer Scanner/Worker.

## Detaildokumente

- [Solution- und Projektstruktur](inventory/solution-projektstruktur.md)
- [Blazor Admin UI und Administrator-Berechtigungen](inventory/admin-ui-auth.md)
- [EF Core DbContext und Datenmodell](inventory/ef-core-datenmodell.md)
- [Hintergrunddienste und Scanner](inventory/hintergrunddienste-scanner.md)
- [Konfiguration, Tests und Integrationspunkte fuer msTools.Backup](inventory/konfiguration-tests-integration.md)

## Relevante Einstiegspunkte

- `VideoWebPlayer/Program.cs`: ruft `builder.AddVideoWebPlayerServices()`, `app.MigrateDatabase()` und `app.UseVideoWebPlayer()` auf.
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`: zentrale DI-Registrierung, inklusive Auth, EF Core, Identity, Scanner und Worker.
- `VideoWebPlayer/Extensions/WebApplicationExtensions.cs`: zentrale Middleware-/Endpoint-Registrierung, passend als Vorbild fuer `app.UseBackups(...)`.
- `VideoWebPlayer/Components/Pages/Admin/*`: vorhandene Admin-Bereiche fuer Quellen, Genres, Sicherheit und Programmeinstellungen.
- `VideoWebPlayer/Data/ApplicationDbContext.cs`: zentraler `IdentityDbContext<ApplicationUser>` mit allen fachlichen `DbSet`s.

## Zentrale Risiken fuer Planung

- Restore muss laufende Datenveraenderer pausieren. Aktuell gibt es keinen gemeinsamen Pause-Koordinator fuer Hosted Services und manuelle Scans.
- Admin-Schutz wird in Razor-Komponenten meist ueber `AuthorizeView` plus `HasClaim("IsAdmin", "True")` umgesetzt, nicht ueber eine zentrale Policy.
- Backup/Restore betrifft Identity-Tabellen und fachliche Tabellen. Das ausfuehrende Admin-Konto muss vor dem Loeschen separat gesichert und danach gemergt oder neu eingefuegt werden.
- Das Datenmodell enthaelt viele Beziehungen und Loeschregeln. Restore sollte nicht mit ad hoc Reihenfolge arbeiten, sondern ueber eine explizite Export-/Import-Schnittstelle des Hostprojekts.
- Es gibt keine vorhandene Backup-Konfiguration in `appsettings.json` und keine Backup-Testbasis.

## Empfehlung fuer den Plan

Die Bibliothek sollte keine direkte Abhaengigkeit auf `VideoWebPlayer` oder EF-Entities haben. Sie sollte generische Konzepte liefern:

- Optionen fuer Speicherpfad, automatische Backups, Aufbewahrung und Upload-Grenzen.
- `IBackupDataProvider` oder aehnlich fuer Export, Validierung und Restore.
- `IBackupOperationCoordinator` oder aehnlich fuer Pausieren/Fortsetzen hostseitiger Prozesse.
- `IBackupStore` fuer Dateisystem-Zugriff und ZIP-Verwaltung.
- Erweiterungsmethoden fuer `IServiceCollection` und `IApplicationBuilder`/`WebApplication`, wobei `UseBackups(...)` nur Middleware/Endpoints registriert, wenn die Bibliothek eigene Endpoints bereitstellt.

Das Webprojekt sollte die Admin-Seite unter `/admin/backups` integrieren und im linken Admin-Menue verlinken.
