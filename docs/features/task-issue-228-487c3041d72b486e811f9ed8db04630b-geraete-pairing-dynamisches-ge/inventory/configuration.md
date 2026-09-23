# Konfiguration / Secrets — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

## Relevante Konfigurationsschlüssel (Ist-Zustand)

| Schlüssel | Verwendung | Fundstellen |
|-----------|------------|-------------|
| `Jwt:Key` | Base64-JWT-Signaturschlüssel; Pflicht in Production (`ServiceCollectionExtensions.cs` Zeile 56); registriert `SymmetricSecurityKey`-Singleton (Zeilen 284–287) | `ServiceCollectionExtensions.cs`, `BearerTokenCheckAttribute.cs`, `AuthorizationTokenService` |
| `Jwt:Issuer` | JWT Issuer/Audience, Default `"VideoWebPlayer"` | `ServiceCollectionExtensions.cs` Zeile 51, `BearerTokenCheckAttribute.cs` Zeile 27, `IAuthService.cs` Zeile 63 |
| `Jwt:ApiToken` | Legacy-Client-Gate, akzeptiert bei `AnyClient` | `ApiTokenCheckAttribute.cs` Zeile 72; `ServiceCollectionExtensions.cs` Zeilen 49, 57 |
| `Jwt:ApiToken:Web` | Web-Client-Gate, akzeptiert bei `AnyClient`; wird als `X-API-Key` auf dem internen `HttpClient` gesetzt | `ApiTokenCheckAttribute.cs` Zeile 73; `ServiceCollectionExtensions.cs` Zeilen 49, 57, 170–171 |
| `Jwt:ApiToken:Maui` | MAUI-Client-Gate, einzig akzeptiertes Token bei `MauiOnly`; **Pflicht in Production** (Zeile 58: `InvalidOperationException("Fehlende Konfiguration: Jwt:ApiToken:Maui")`) | `ApiTokenCheckAttribute.cs` Zeile 68; `ServiceCollectionExtensions.cs` Zeilen 50, 58 |
| `ConnectionStrings:DefaultConnection` | SQLite-Verbindung, Fallback `Data Source=app.db` | `ServiceCollectionExtensions.cs` Zeile 156; `appsettings.json` |
| `Host:Address` / `Host:Port` | Serveradresse für UDP-Discovery-Antwort | `Program.cs` Zeile 52 |
| `Kestrel:Limits:MaxRequestBodySize` | Request-Limit | `Program.cs` Zeilen 34–41 |
| `App:BaseUrl` | Fallback-BaseAddress für scoped `HttpClient` | `ServiceCollectionExtensions.cs` Zeile 201 |

## appsettings-Dateien

- `VideoWebPlayer/appsettings.json`: `ConnectionStrings`, `Backups`, `EpisodeBackgroundImage`, `Serilog`, `Logging`, `AllowedHosts`, `AutoUpdate`. **Keine** `Jwt`- oder `Pairing`-Sektion.
- `VideoWebPlayer/appsettings.Development.json` / `appsettings.Production.json`: nur Serilog/Logging, `AutoUpdate.Enabled=false` (Dev) bzw. Kestrel-Endpunkt `http://*:5002` (Prod). Keine Secrets in den Dateien.
- Eine `Pairing`-Sektion (`Pairing:CodeLength`, `Pairing:CodeTtlMinutes`, `Pairing:MaxAttemptsPerIp`) existiert nicht — müsste neu eingeführt oder über `Setup`/`ProgramSettingsService` abgebildet werden.

## Rate-Limiting

`Microsoft.AspNetCore.RateLimiting`/`AddRateLimiter` wird im Projekt **nicht** verwendet (Suche ergab nur Framework-Referenzen in `obj/project.assets.json`). Vorhandener Bruteforce-Schutz: `ILoginIpBlockService`/`LoginIpBlockService` (siehe [logic.md](logic.md)).

## Secrets-Management (Dokumentationsstand)

`docs/SECRETS_MANAGEMENT.md` listet aktuell nur `Jwt:Key`, `Jwt:ApiToken:Web`, `Jwt:Issuer`, Benutzerpasswörter und JWT-Access-Token als Konfigurationswerte (Zeilen 12–18); `Jwt:ApiToken:Maui` und `Jwt:ApiToken` sind dort **nicht** aufgeführt, obwohl der Produktions-Pflichtcheck sie verlangt — Diskrepanz im Ist-Stand.
