# Solution- und Projektstruktur

## Solution

`VideoPlayer.sln` enthaelt mehrere Projekte. Fuer die Backup-Anforderung relevant sind vor allem:

- `VideoWebPlayer/VideoWebPlayer.csproj`: ASP.NET Core Webprojekt, Blazor Server, EF Core, Identity, Serilog, SQLite, SFTP.
- `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj`: `net10.0` Client-/Shared-Bibliothek fuer API-Client und DTO-nahe Logik.
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`: xUnit v3 Tests fuer Webprojekt-Services.
- `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj` und `VideoWebPlayer.Maui.Tests`: mobile Client-App, fuer Admin-Backups vermutlich nicht primaer relevant.
- Weitere historische/alternative Projekte: `WebPlayer`, `WebPlayer.Client`, `WebPlayerApi`, `WebPlayerApi.Common`, `Videos`.

Die Solution referenziert ausserdem `SMBLibrary-master/Utilities/Utilities.csproj`; ein entsprechendes Verzeichnis war in der Root-Auflistung nicht sichtbar. Das sollte vor Build-/Solution-Aenderungen beachtet werden.

## Webprojekt

`VideoWebPlayer/VideoWebPlayer.csproj` nutzt:

- `Microsoft.NET.Sdk.Web`
- `TargetFramework` `net10.0`
- `WarningsAsErrors` fuer XML-Dokumentationswarnung `1591`
- EF Core SQLite und Identity EF Core
- Serilog Console/File
- `SSH.NET`

Wichtig: Neue public Typen in `VideoWebPlayer` und besonders in einer neuen Bibliothek muessen XML-Dokumentationskommentare erhalten, weil das Webprojekt `GenerateDocumentationFile` und `WarningsAsErrors` aktiviert.

## Startup-Struktur

`VideoWebPlayer/Program.cs` ist schlank:

- `builder.AddVideoWebPlayerServices()` registriert Dienste.
- `app.MigrateDatabase()` fuehrt EF-Migrationen aus.
- `app.UseVideoWebPlayer()` registriert Middleware und Endpoints.
- Danach wird ein `UdpDiscoveryListener` manuell gestartet.

`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` ist der wichtigste DI-Ort:

- `AddAuthorization()`, Cookie-/JWT-Authentication.
- `AddDbContext<ApplicationDbContext>(...UseSqlite(connectionString))`.
- `AddIdentityCore<ApplicationUser>()`.
- Fachservices und Hosted Services.

`VideoWebPlayer/Extensions/WebApplicationExtensions.cs` ist der wichtigste Pipeline-Ort:

- Fehlerbehandlung/HSTS/HTTPS/IP-Whitelist.
- Static files, Auth, Authorization, Antiforgery.
- Blazor Razor Components.
- Controller, SignalR Hub und Identity Endpoints.

## Integrationsfolgen fuer msTools.Backup

Eine neue Bibliothek sollte als eigenes Projekt in die Solution aufgenommen werden, wahrscheinlich `msTools.Backup/msTools.Backup.csproj` mit `net10.0` oder bei Wiederverwendbarkeit eher `netstandard`/`net8.0`/`net10.0` je nach Projektstandard. Da die Host-App `net10.0` ist, ist `net10.0` kurzfristig am einfachsten, aber weniger wiederverwendbar.

Empfohlene Integration im Webprojekt:

- `builder.Services.AddBackups(...)` oder `builder.AddBackups(...)` fuer Services und Optionen.
- `app.UseBackups(...)` fuer optionale Endpoints/Middleware, falls die Bibliothek eigene Download/Upload-Endpoints anbietet.
- Hostseitiger Adapter in `VideoWebPlayer`, der `ApplicationDbContext` exportiert und wiederherstellt.

Die vorhandene Extension-Struktur spricht dafuer, neue Registrierungen in `ServiceCollectionExtensions.AddVideoWebPlayerServices` und `WebApplicationExtensions.UseVideoWebPlayer` einzubauen.
