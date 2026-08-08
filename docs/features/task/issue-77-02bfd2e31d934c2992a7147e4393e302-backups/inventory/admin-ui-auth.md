# Blazor Admin UI und Administrator-Berechtigungen

## Vorhandene Admin-Bereiche

Admin-Seiten liegen unter `VideoWebPlayer/Components/Pages/Admin/`:

- `/admin/mediasources`: Quellenverwaltung mit Liste, Neu, Loeschen, Scan zuruecksetzen und Komplettscan.
- `/admin/mediasources/new` und `/admin/mediasources/{Id:long}`: Quellendetails.
- `/admin/mediasources/{SourceId:long}/explorer`: Explorer und manueller Rescan.
- `/admin/genres`: Genre-Verwaltung.
- `/admin/program-settings`: Programmeinstellungen fuer Scanintervalle.
- `/admin/security`: gesperrte Login-IPs.

Die Navigation liegt in `VideoWebPlayer/Components/Layout/NavMenu.razor`. Dort wird ein Admin-Bereich angezeigt, wenn `context.User.HasClaim("IsAdmin", "True")` gilt. Eine neue Backup-Seite sollte dort neben `Programmeinstellungen`, `Quellen`, `Genres` und `Sicherheit` verlinkt werden.

## Autorisierungsmuster

Die Anwendung verwendet kein Role-System fuer Administratoren, sondern ein boolesches Feld auf dem User:

- `ApplicationUser.IsAdmin`
- `ApplicationUserClaimsPrincipalFactory` fuegt bei Admins den Claim `IsAdmin=True` hinzu.

Admin-Komponenten pruefen meistens lokal:

- `AuthorizeView` fuer angemeldete Benutzer.
- `AuthenticationStateProvider.GetAuthenticationStateAsync()`.
- `user.HasClaim("IsAdmin", "True")`.
- Bei fehlendem Claim wird eine Fehlermeldung gerendert.

Es gibt aktuell keine zentrale Authorization Policy wie `RequireClaim("IsAdmin", "True")`. Fuer Backup-Aktionen waere eine zentrale Policy robuster, besonders fuer Download/Upload/Restore-Endpunkte.

## UI-Muster

Die Admin-UI nutzt klassische Bootstrap-Klassen:

- Tabellen fuer Listen.
- `alert` fuer Status- und Fehlermeldungen.
- `button` fuer Aktionen.
- `EditForm`, `DataAnnotationsValidator`, `ValidationSummary` fuer Einstellungen.
- `InputFile` wird bereits in `GenreAdmin.razor` fuer Uploads verwendet.

Eine Backup-Seite sollte sich daran orientieren:

- Route `/admin/backups`.
- Liste vorhandener Backups als Tabelle.
- Button fuer manuelles Backup.
- Download-Aktion je Datei.
- Upload per `InputFile`.
- Restore-Aktion mit Sicherheitsabfrage, z. B. Modal oder bestaetigender zweiter Schritt.
- Einstellungsbereich fuer Speicherpfad und GVS-Aufbewahrung, entweder eigene Seite oder Erweiterung der Programmeinstellungen.

## Sicherheitsluecken fuer Planung

Nur UI-seitige Claim-Pruefung reicht fuer Download, Upload und Restore nicht aus, wenn diese Operationen ueber Controller/Endpoints laufen. Controller oder Minimal APIs muessen serverseitig `RequireAuthorization` bzw. `[Authorize]` plus Admin-Policy verwenden.

Die Sicherheitsabfrage fuer Restore muss serverseitig flankiert werden. Ein Client-Confirm allein verhindert versehentliche Klicks, schuetzt aber nicht gegen direkte HTTP-Aufrufe.
