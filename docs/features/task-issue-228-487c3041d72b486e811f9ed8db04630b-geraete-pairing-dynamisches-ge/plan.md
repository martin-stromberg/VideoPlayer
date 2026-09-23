# Umsetzungsplan: Geräte-Pairing mit dynamischem Geräte-Token

## Übersicht

Das `X-API-Key`-Gate wird von einem rein statischen Konfigurationstoken (`Jwt:ApiToken:Maui`) auf individuelle, widerrufbare Geräte-Tokens umgestellt. Ein Administrator erzeugt in einer neuen Admin-Seite (`/admin/devices`) einen kurzlebigen Einmal-Pairing-Code; die Client-App löst diesen über den neuen öffentlichen Endpunkt `POST api/pairing/exchange` gegen ein Geräte-Token ein, dessen Übertragung per ECDH-Schlüsselaustausch (P-256) plus AES-256-GCM auf Anwendungsebene geschützt wird. `ApiTokenCheckAttribute` akzeptiert im Scope `MauiOnly` zusätzlich die in der Datenbank gespeicherten, nicht widerrufenen Geräte-Tokens; `Jwt:ApiToken:Maui` bleibt als Fallback bestehen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Rate-Limiting `exchange` | Vorhandenes `ILoginIpBlockService`-Muster wiederverwenden (Singleton, `ConcurrentDictionary`, Persistenz in `BlockedLoginIps`, Schwelle 5) | Kein neues Paket (`Microsoft.AspNetCore.RateLimiting` ist nicht installiert), persistenter Bruteforce-Schutz, Sperren sind automatisch in `Security.razor` sichtbar und entsperrbar. |
| Service-Aufteilung | Zwei Services: `IDeviceTokenService` (Token-Issuing, Hash-Validierung, Widerruf, Auflistung, `LastUsedAtUtc`) und `IPairingService` (Code-Erzeugung/-Validierung, Exchange inkl. ECDH/AES-GCM), jeweils scoped | Trennung der Zuständigkeiten; `ApiTokenCheckAttribute` benötigt nur `IDeviceTokenService` und bleibt schmal. Interface + Implementierung in einer Datei folgt dem `LoginIpBlockService.cs`-Muster. |
| Admin-UI | Neue Seite `Devices.razor` unter `/admin/devices` plus Kachel in `AdminIndex.razor` | Pairing-Code-Erzeugung + Geräteliste + Widerruf ist ein eigenständiger Workflow; `Security.razor` bleibt auf IP-Sperren fokussiert. Layout/Interaktion folgt exakt dem `Security.razor`-Muster (`InteractiveServer`, `IsAdmin`-Claim-Prüfung, `admin-stat-card`/`admin-table-wrap`-CSS). |
| Token-Speicherung | Geräte-Token als 32-Byte-Zufall (Base64Url); in `PairedDevice.TokenHash` wird nur `SHA-256(token)` gespeichert | Klartext-Token verlässt den Server nur einmal, verschlüsselt in der Exchange-Response; Hash-Vergleich reicht, da Tokens hohe Entropie haben (kein Salt nötig). |
| Pairing-Code-Speicherung | In `PairingCode.CodeHash` wird `SHA-256(code)` gespeichert; der Klartext-Code wird nur einmal in der UI angezeigt | Konsistent mit Token-Hashing; Lookup erfolgt über Hash-Gleichheit. |
| `Jwt:ApiToken:Maui` | Bleibt als Fallback akzeptiert (Migration alter App-Versionen); Produktions-Pflichtcheck in `ServiceCollectionExtensions.cs` (Zeile 54–59) bleibt unverändert | Anforderung lässt beides zu; Bestandstests (`AddVideoWebPlayerServices_ProductionRequiresMauiApiToken`, `MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds`) bleiben gültig, alte App-Versionen bleiben funktionsfähig. |
| Krypto-Vertrag | ECDH über `nistP256` (`ECDiffieHellman`), PublicKeys als SubjectPublicKeyInfo (DER, Base64); Schlüsselableitung aus dem Shared Secret via SHA-256-HMAC (`DeriveKeyFromHmac` bzw. HKDF-Äquivalent); Verschlüsselung AES-256-GCM, Response-Feld `EncryptedToken` = Base64(nonce ‖ ciphertext ‖ tag) mit 12-Byte-Nonce. **Verbindliche JSON-Feldnamen** (camelCase, ASP.NET-Core-Standardserialisierung der DTOs): Request `code`, `clientPublicKey`, `deviceName`; Response `serverPublicKey`, `encryptedToken` | Bordmittel (`System.Security.Cryptography`), kein neues Paket. ENTSCHIEDEN: Der Server-Plan legt den Vertrag verbindlich fest; das App-Issue `martin-stromberg/VideoPlayer-App` übernimmt die JSON-Feldnamen unverändert. |
| Code-Format | 8 Zeichen aus Alphabet ohne verwechselbare Zeichen (ohne `0/O/1/I/L`), erzeugt mit `RandomNumberGenerator`; TTL 5 Minuten; beides über `Pairing:`-Konfiguration übersteuerbar | Einmalige manuelle Eingabe in der App; 8 Zeichen × ~31 Alphabetwerte ≈ 40 Bit Entropie, in Kombination mit TTL + IP-Sperre ausreichend. |
| Konfiguration | `appsettings`-Sektion `Pairing` mit Defaults im Code (gelesen via `IConfiguration`/`GetValue<T>` im `PairingService`) | Kein Admin-Editierbarkeitswunsch in der Anforderung; entspricht der `Jwt:*`-Handhabung. `Setup`/`ProgramSettingsService` wird nicht verwendet. |
| `ApiTokenCheckAttribute`-Asynchronität | Umstellung auf `OnActionExecutionAsync` (Async-Filter): bestehende synchrone Config-Prüfung bleibt erster Schritt; nur bei `MauiOnly` und Nicht-Match wird `IDeviceTokenService` aus `RequestServices` aufgelöst und die DB geprüft | DB-Zugriff braucht async; `AnyClient`-Verhalten und der schnelle Config-Pfad bleiben unverändert. Der bestehende isolierte Attribut-Test muss auf die Async-Pipeline umgestellt werden (siehe Tests). |
| `LastUsedAtUtc`-Pflege | Bei jeder erfolgreichen Geräte-Token-Validierung aktualisieren | `MauiOnly` gated aktuell nur `POST api/auth/login` — ein Schreibzugriff pro Login ist billig und liefert einen echten „zuletzt verwendet"-Wert. |
| Zusätzliche Absicherung `exchange` | Kein `ConnectionCheck`/IP-Whitelist am Endpunkt | Die App ist zum Pairing-Zeitpunkt noch nicht authentifiziert; `WhitelistIpMiddleware` greift erst bei authentifizierten Requests. Schutz erfolgt über Einmal-Code + TTL + IP-Sperre. |
| Scope für Geräte-Tokens | `MauiOnly` wiederverwenden, kein neuer `ApiTokenScope`-Wert | Der Scope deckt genau den App-Login (`AuthController.Login`) ab; die Anforderung nennt keinen weiteren Endpunkt. |
| Geräte-Name | Optionales Feld `DeviceName` im `PairingExchangeRequest`; leer → Server-Default (z. B. „Gerät"); Admin kann den Eintrag in `Devices.razor` sehen (Umbenennen nicht gefordert). Überschreitung von 200 Zeichen → Ablehnung mit `400` (keine stille Kürzung) | App kann Gerätemodell/Anzeigenamen liefern; keine zusätzliche Admin-Interaktion nötig. Explizite Ablehnung statt Kürzung, damit der Client den Fehler erkennt und die Validierung konsistent zu den übrigen Formatfehlern bleibt. |
| Aufräumlauf abgelaufener Pairing-Codes | **Nicht umgesetzt** — kein HostedService (Muster `MediaSourceScanService`/`ContinueWatchingWorker` wäre verfügbar) | Laut Anforderung ausdrücklich optional. Abgelaufene Codes werden bei der Validierung verworfen (`401`-Pfad wie ungültiger Code) und durch die `GetActiveCodesAsync`-Filterung (`ExpiresAtUtc`/`ConsumedAtUtc`) aus der UI ferngehalten; das Datenaufkommen ist gering (ein Datensatz pro Pairing-Vorgang). |

## Programmabläufe

### Pairing-Code erzeugen (Admin-UI)

1. Admin öffnet `/admin/devices` (`Devices.razor`, `@rendermode InteractiveServer`).
2. `OnInitializedAsync` prüft den `IsAdmin`-Claim über `AuthenticationStateProvider` (Muster `Security.razor`, Zeilen 91–98); Nicht-Admins sehen „Nicht autorisiert.".
3. Klick auf „Pairing-Code erzeugen" ruft `IPairingService.CreatePairingCodeAsync(createdByUserId)`.
4. `PairingService` generiert den Code (8 Zeichen, unverwechselbares Alphabet, `RandomNumberGenerator`), berechnet `CodeHash` (SHA-256) und persistiert `PairingCode` mit `CreatedAtUtc`, `ExpiresAtUtc` (jetzt + `Pairing:CodeTtlMinutes`), `ConsumedAtUtc = null`, `CreatedByUserId`.
5. UI zeigt den Klartext-Code einmalig mit Restlaufzeit an; die Tabelle aktiver Codes zeigt nur Metadaten (Ablaufzeitpunkt, Ersteller).

Beteiligte Klassen/Komponenten: `Devices.razor`, `IPairingService`, `PairingService`, `ApplicationDbContext`, `PairingCode`, `AuthenticationStateProvider`

### Pairing-Exchange (öffentlicher Endpunkt)

1. App sendet `POST api/pairing/exchange` mit `PairingExchangeRequest` (`Code`, `ClientPublicKey` = Base64-SPKI, optional `DeviceName`; JSON-Feldnamen verbindlich camelCase: `code`, `clientPublicKey`, `deviceName`). Der Endpunkt trägt weder `ApiTokenCheck` noch `BearerTokenCheck`.
2. `PairingController.Exchange` prüft `ILoginIpBlockService.IsBlocked(RemoteIpAddress)` → gesperrt: `429`.
3. `IPairingService.ExchangeAsync(request)` validiert das Request-Format (`Code` und `ClientPublicKey` Pflicht/nicht leer, `ClientPublicKey` als SPKI importierbar mit Kurve `nistP256`, `DeviceName` ≤ 200 Zeichen) → ungültig: `400` ohne Fehlversuchszählung. Sicherheitsrelevante Abgrenzung: Ein leerer/fehlender `Code` darf den Lookup-Pfad (Schritt 4) nicht erreichen — sonst würde ein Formatfehler fälschlich `RegisterFailure(ip)` auslösen und zur IP-Sperre beitragen.
4. Code-Lookup über `CodeHash` in `PairingCodes`: nicht vorhanden, `ExpiresAtUtc` überschritten oder `ConsumedAtUtc` gesetzt → `RegisterFailure(ip)` + `401` (generische Meldung, kein Orakel).
5. Atomarer Verbrauch: `ExecuteUpdateAsync` auf `PairingCodes` mit `WHERE Id = … AND ConsumedAtUtc IS NULL` — 0 betroffene Zeilen → wie ungültiger Code behandeln (Race-Schutz).
6. Server erzeugt flüchtiges ECDH-Schlüsselpaar (`nistP256`), importiert `ClientPublicKey`, leitet den AES-Schlüssel ab (`DeriveKeyFromHmac`/SHA-256).
7. `IDeviceTokenService.IssueAsync(deviceName, createdByUserId)` erzeugt das Geräte-Token (32 Byte → Base64Url), speichert `PairedDevice` mit `TokenHash` = SHA-256, `Name`, `IssuedAtUtc`, `LastUsedAtUtc = null`, `RevokedAtUtc = null`.
8. Token wird mit AES-256-GCM verschlüsselt; Response `PairingExchangeResponse` (`ServerPublicKey` = Base64-SPKI, `EncryptedToken` = Base64(nonce‖ciphertext‖tag) mit 12-Byte-Nonce; JSON-Feldnamen verbindlich: `serverPublicKey`, `encryptedToken`); `RegisterSuccess(ip)` löscht etwaige Fehlversuchszähler.
9. Der flüchtige Server-Key und das Klartext-Token werden nicht persistiert.

Beteiligte Klassen/Komponenten: `PairingController`, `PairingExchangeRequest`, `PairingExchangeResponse`, `IPairingService`, `PairingService`, `IDeviceTokenService`, `DeviceTokenService`, `ILoginIpBlockService`, `ApplicationDbContext`, `PairingCode`, `PairedDevice`

### Request-Gate mit Geräte-Token (`AuthController.Login`)

1. `ApiTokenCheckAttribute.OnActionExecutionAsync` liest `X-API-Key` wie bisher (inkl. `Bearer `-Präfix-Entfernung).
2. Token matcht einen Config-Wert des Scopes (`MauiOnly`: `Jwt:ApiToken:Maui`) → Request passiert (unverändert).
3. Nur bei `MauiOnly` und Nicht-Match: `IDeviceTokenService` via `RequestServices.GetService` auflösen; vorhanden → `IsValidDeviceTokenAsync(token)` prüft `SHA-256(token)` gegen `PairedDevices` mit `RevokedAtUtc == null`.
4. Treffer → `LastUsedAtUtc` aktualisieren, `await next()`.
5. Kein Treffer oder Service nicht registriert → `UnauthorizedResult`; Log ohne Token-Wert (bestehender Schutz bleibt).
6. `AnyClient` führt nie eine DB-Prüfung durch.

Beteiligte Klassen/Komponenten: `ApiTokenCheckAttribute`, `IDeviceTokenService`, `AuthController`

### Gerät widerrufen / Geräte auflisten (Admin-UI)

1. `Devices.razor` listet `PairedDevice`-Einträge via `IDeviceTokenService.GetDevicesAsync()` (Name, `IssuedAtUtc`, `LastUsedAtUtc`, Status aktiv/widerrufen).
2. „Widerrufen" ruft `IDeviceTokenService.RevokeAsync(id)` → setzt `RevokedAtUtc = UtcNow`.
3. Folgende Requests mit dem Token scheitern am Gate (`401`); bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf (12 h) gültig — entschieden und akzeptiert; Dokumentation in `docs/help`/`docs/API.md`, kein Folge-Issue in dieser Anforderung.

Beteiligte Klassen/Komponenten: `Devices.razor`, `IDeviceTokenService`, `ApplicationDbContext`, `PairedDevice`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PairedDevice` | Datenmodellklasse (`VideoWebPlayer/Data/PairedDevice.cs`) | Gekoppeltes Gerät: `Id` (int), `Name` (string), `TokenHash` (string), `IssuedAtUtc` (DateTime), `LastUsedAtUtc` (DateTime?), `RevokedAtUtc` (DateTime?), `CreatedByUserId` (string?) |
| `PairingCode` | Datenmodellklasse (`VideoWebPlayer/Data/PairingCode.cs`) | Einmal-Pairing-Code: `Id` (int), `CodeHash` (string), `CreatedAtUtc`, `ExpiresAtUtc` (DateTime), `ConsumedAtUtc` (DateTime?), `CreatedByUserId` (string?) |
| `PairedDeviceConfiguration` | `IEntityTypeConfiguration<PairedDevice>` (`VideoWebPlayer/Data/Configurations/`) | Schlüssel `Id`, `HasMaxLength` für `Name` (200)/`TokenHash` (128)/`CreatedByUserId` (64), Unique-Index auf `TokenHash`, Index auf `RevokedAtUtc` |
| `PairingCodeConfiguration` | `IEntityTypeConfiguration<PairingCode>` (`VideoWebPlayer/Data/Configurations/`) | Schlüssel `Id`, `HasMaxLength` für `CodeHash` (128)/`CreatedByUserId` (64), Index auf `CodeHash`, Index auf `ExpiresAtUtc` |
| `IDeviceTokenService` | Interface (`VideoWebPlayer/Services/DeviceTokenService.cs`, mit Implementierung in einer Datei) | `IssueAsync`, `IsValidDeviceTokenAsync` (inkl. `LastUsedAtUtc`-Update), `RevokeAsync`, `GetDevicesAsync` |
| `DeviceTokenService` | Klasse (`internal sealed`, scoped) | Implementierung: Token-Erzeugung (`RandomNumberGenerator` + Base64Url), SHA-256-Hashing, DB-Zugriff über `ApplicationDbContext` |
| `IPairingService` | Interface (`VideoWebPlayer/Services/PairingService.cs`, mit Implementierung in einer Datei) | `CreatePairingCodeAsync`, `ExchangeAsync`, `GetActiveCodesAsync` (Metadaten für UI) |
| `PairingService` | Klasse (`internal sealed`, scoped) | Code-Generierung/-Validierung, atomarer Verbrauch, ECDH (P-256/SPKI), Schlüsselableitung, AES-256-GCM; liest `Pairing:*` via `IConfiguration` |
| `PairingExchangeResult` | internes Ergebnisobjekt (`VideoWebPlayer/Services/PairingService.cs`) | Erfolg/Fehlergrund + Payload (`ServerPublicKey`, `EncryptedToken`) für klare Controller-/Testbarkeit |
| `PairingController` | Controller (`VideoWebPlayer/Controllers/PairingController.cs`) | `[ApiController]`, Route `api/pairing`, `POST exchange`; erbt `ApiBaseController`; keine Gate-Attribute, Rate-Limit über `ILoginIpBlockService` |
| `PairingExchangeRequest` | DTO (`VideoWebPlayer.Client/Models/PairingExchangeRequest.cs`) | `Code` (string), `ClientPublicKey` (string, Base64-SPKI), `DeviceName` (string?) — JSON: `code`, `clientPublicKey`, `deviceName` (verbindlich, camelCase) |
| `PairingExchangeResponse` | DTO (`VideoWebPlayer.Client/Models/PairingExchangeResponse.cs`) | `ServerPublicKey` (string, Base64-SPKI), `EncryptedToken` (string, Base64) — JSON: `serverPublicKey`, `encryptedToken` (verbindlich, camelCase) |
| `Devices.razor` | Razor-Komponente (`VideoWebPlayer/Components/Pages/Admin/Devices.razor`) | Admin-Seite `/admin/devices`: Pairing-Code erzeugen (TTL-Anzeige), aktive Codes, Geräteliste mit Widerruf-Aktion; Muster `Security.razor` |

## Änderungen an bestehenden Klassen

### `ApplicationDbContext` (Datenmodell, `VideoWebPlayer/Data/ApplicationDbContext.cs`)

- **Neue Eigenschaften:** `DbSet<PairedDevice> PairedDevices`, `DbSet<PairingCode> PairingCodes` — in der DbSet-Region (Zeilen 29–154).
- `OnModelCreating` bleibt unverändert (`ApplyConfigurationsFromAssembly` bindet die neuen Konfigurationen automatisch ein).

### `ApiTokenCheckAttribute` (ActionFilter, `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`)

- **Geänderte Methoden:** `OnActionExecuting` — Logik wird in `OnActionExecutionAsync` überführt: bestehender Config-Token-Pfad bleibt synchroner Vorab-Check; bei `MauiOnly` ohne Config-Match folgt die async DB-Geräte-Token-Prüfung über `IDeviceTokenService` aus `RequestServices` (`GetService` — fehlender Service = wie heute `Unauthorized`, damit isolierte Tests ohne DB weiterlaufen). `AnyClient` unverändert. Log-Meldungen enthalten weiterhin keinen Token-Wert.
- `ApiTokenScope` bleibt unverändert (kein neuer Wert).

### `ServiceCollectionExtensions` (DI, `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)

- **Neue Registrierungen:** `AddScoped<IDeviceTokenService, DeviceTokenService>()`, `AddScoped<IPairingService, PairingService>()` in `AddVideoWebPlayerServices`.
- Produktions-Pflichtcheck auf `Jwt:ApiToken:Maui` (Zeile 54–59) bleibt unverändert (Fallback-Entscheidung).

### `AdminIndex.razor` (`VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`)

- Neue Kachel (`admin-tile`) für `/admin/devices` („Geräte" o. ä.), eingereiht bei den bestehenden Kacheln (Muster Zeilen 51–55).

### `AuthController` (`VideoWebPlayer/Controllers/AuthController.cs`)

- Keine Codeänderung — `[ApiTokenCheck(ApiTokenScope.MauiOnly)]` auf `Login` (Zeile 42) bleibt und akzeptiert durch die Attribut-Erweiterung künftig Geräte-Tokens.

### Dokumentationsdateien

- `docs/API.md`: neuer öffentlicher Endpunkt `POST /api/pairing/exchange` (Request/Response-Felder inkl. verbindlicher JSON-Feldnamen, Krypto-Parameter, Fehlercodes), geänderte `X-API-Key`-Semantik (Geräte-Tokens + `Jwt:ApiToken:Maui`-Fallback) und das Widerruf-Verhalten dokumentieren (widerrufene Geräte-Tokens werden sofort abgelehnt; bereits ausgestellte Benutzer-JWTs bleiben bis zum Ablauf von 12 h gültig).
- `docs/GUIDE_Installation.md`: Abschnitt „Geräte-Pairing" (Admin-Ablauf, `Pairing:`-Konfiguration).
- `docs/help/einrichtung.md`: neue Kachel „Geräte" ergänzen; ggf. neue Hilfeseite zum Pairing-Ablauf inkl. Hinweis auf das Widerruf-Verhalten (JWT-Restlaufzeit).
- `docs/SECRETS_MANAGEMENT.md`: `Jwt:ApiToken:Maui` in der Secrets-Tabelle ergänzen und als Fallback zum Geräte-Token-Verfahren beschreiben.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddDevicePairing` (Arbeitstitel, via `dotnet ef migrations add`) | Neu: `PairedDevices` (`Id`, `Name`, `TokenHash`, `IssuedAtUtc`, `LastUsedAtUtc`, `RevokedAtUtc`, `CreatedByUserId`), `PairingCodes` (`Id`, `CodeHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc`, `CreatedByUserId`) | Neue Tabellen samt Indizes (`TokenHash` unique, `CodeHash`, `ExpiresAtUtc`, `RevokedAtUtc`); wird beim Start über `MigrateDatabase()` automatisch angewendet. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `PairingExchangeRequest.Code` | Pflichtfeld, nicht leer | `400` (Formatfehler, kein Fehlversuchszähler) |
| `PairingExchangeRequest.ClientPublicKey` | Pflichtfeld, als SubjectPublicKeyInfo importierbar, Kurve `nistP256` | `400` |
| `PairingExchangeRequest.DeviceName` | Optional, Länge ≤ 200 | Überschreitung → `400` (Formatfehler, kein Fehlversuchszähler) |
| `PairingCode` (Lookup per `CodeHash`) | Muss existieren, `ExpiresAtUtc` nicht überschritten, `ConsumedAtUtc == null`, atomarer Verbrauch | `401` generisch + `RegisterFailure(ip)` |
| Geräte-Token (`X-API-Key` bei `MauiOnly`) | `SHA-256`-Hash in `PairedDevices`, `RevokedAtUtc == null` | `401` (wie bisher, ohne Token im Log) |
| Request-IP (`exchange`) | `ILoginIpBlockService.IsBlocked` | `429` |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Pairing:CodeLength` | int | `8` | Länge des Pairing-Codes |
| `Pairing:CodeTtlMinutes` | int | `5` | Gültigkeitsdauer eines Pairing-Codes |

Hinweis: Die Fehlversuchsschwelle für `exchange` ist die geteilte Konstante `Threshold = 5` des `LoginIpBlockService` — kein eigener Konfigurationseintrag. `Jwt:ApiToken:Maui` bleibt Pflicht in Production (Fallback). Das Code-Alphabet ist eine Code-Konstante, nicht konfigurierbar.

## Seiteneffekte und Risiken

- **`ApiTokenCheckAttribute`:** Umstellung auf `OnActionExecutionAsync` — der isolierte Test `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` ruft `OnActionExecuting` direkt und muss angepasst werden. `AnyClient`-Endpunkte (`AuthController.Impersonate`) dürfen keinen DB-Zugriff auslösen.
- **`LoginIpBlockService`:** Pairing-Fehlversuche teilen sich die Sperrliste mit dem Web-Login — eine bruteforcende IP wird auch für Admin-Logins gesperrt (gewollte gemeinsame Sicherheitsliste; erscheint im Security-Badge in `NavMenu.razor` und in `Security.razor`).
- **Produktions-Pflichtcheck:** bleibt bestehen — Deployments ohne `Jwt:ApiToken:Maui` starten weiterhin nicht, obwohl Geräte-Tokens funktionieren würden (bewusste Fallback-Entscheidung).
- **`MauiOnly`-Semantik:** Geräte-Tokens gelten nur dort; bei späterer Aufnahme weiterer `MauiOnly`-Endpunkte gelten sie automatisch auch dort.
- **JWT-Nachwirkung:** Bei Widerruf bleiben bereits ausgestellte Benutzer-JWTs bis zum Ablauf (12 h, `AuthorizationTokenService.CreateToken` in `IAuthService.cs`) gültig — ENTSCHIEDEN und akzeptiert; das Verhalten wird in `docs/help`/`docs/API.md` dokumentiert, ein serverseitiger JWT-Widerruf ist nicht Teil dieser Anforderung.
- **Kein TLS:** Der Exchange schützt nur den Token selbst; `Code` und `ClientPublicKey` gehen im Klartext über HTTP — ist durch das Design (Einmal-Code, keine wiederverwendbaren Secrets) abgedeckt, in Doku zu vermerken.

## Umsetzungsreihenfolge

1. **Entitäten `PairedDevice` und `PairingCode` sowie `PairedDeviceConfiguration`/`PairingCodeConfiguration` anlegen**
   - Voraussetzungen: Keine (EF-Core- und `IEntityTypeConfiguration<T>`-Konventionen vorhanden).
   - Beschreibung: Neue Datenmodellklassen unter `VideoWebPlayer/Data/`, Konfigurationen unter `VideoWebPlayer/Data/Configurations/` im Stil von `BlockedLoginIp`/`BlockedLoginIpConfiguration` (`*AtUtc`-Timestamps, `HasMaxLength`, Indizes).

2. **`DbSet`s in `ApplicationDbContext` ergänzen und Migration `AddDevicePairing` erzeugen**
   - Voraussetzungen: Schritt 1; `Microsoft.EntityFrameworkCore.Design`/`.Tools` 10.0.10 sind im `VideoWebPlayer.csproj` vorhanden (`dotnet ef migrations add` bzw. Package-Manager-Konsole nutzbar).
   - Beschreibung: `DbSet<PairedDevice>`/`DbSet<PairingCode>` in der DbSet-Region ergänzen; Migration unter `VideoWebPlayer/Migrations/` generieren.

3. **DTOs `PairingExchangeRequest`/`PairingExchangeResponse` in `VideoWebPlayer.Client/Models/` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Schlichte DTO-Klassen im Namespace `VideoWebPlayer.Client.Models` (Stil `AuthenticationRequest`).

4. **`IDeviceTokenService`/`DeviceTokenService` implementieren**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: Token-Issuing (Base64Url-Zufallstoken + SHA-256-`TokenHash`), `IsValidDeviceTokenAsync` (Hash-Vergleich, `RevokedAtUtc == null`, `LastUsedAtUtc`-Update), `RevokeAsync`, `GetDevicesAsync`.

5. **`IPairingService`/`PairingService` implementieren**
   - Voraussetzungen: Schritte 1–4; `System.Security.Cryptography` ist BCL (kein Paket nötig).
   - Beschreibung: `CreatePairingCodeAsync` (Alphabet/Länge/TTL aus `Pairing:`-Config mit Defaults), `ExchangeAsync` (Code-Validierung, atomarer Verbrauch via `ExecuteUpdateAsync`, ECDH P-256, Schlüsselableitung, AES-256-GCM), `PairingExchangeResult`.

6. **DI-Registrierung in `ServiceCollectionExtensions.AddVideoWebPlayerServices`**
   - Voraussetzungen: Schritte 4–5.
   - Beschreibung: Beide Services scoped registrieren.

7. **`PairingController` mit `POST api/pairing/exchange` erstellen**
   - Voraussetzungen: Schritte 3, 5–6; `ILoginIpBlockService` ist bereits als Singleton registriert; `HealthController`/`AuthController` als Controller-Muster vorhanden.
   - Beschreibung: `[ApiController]`, Route `api/pairing`, IP-Sperrprüfung, Request-Validierung, Mapping `PairingExchangeResult` → `200`/`400`/`401`/`429`.

8. **`ApiTokenCheckAttribute` auf Geräte-Tokens erweitern**
   - Voraussetzungen: Schritte 4, 6.
   - Beschreibung: Umstellung auf `OnActionExecutionAsync`; DB-Prüfung nur bei `MauiOnly`; Fehlen von `IDeviceTokenService` tolerieren.

9. **Admin-Seite `Devices.razor` + Kachel in `AdminIndex.razor`**
   - Voraussetzungen: Schritte 4–6; UI-Muster `Security.razor`/`AdminIndex.razor` vorhanden.
   - Beschreibung: Neue Seite `/admin/devices` (`InteractiveServer`, `IsAdmin`-Claim-Check, `admin-*`-CSS) mit Code-Erzeugung inkl. TTL-Anzeige, aktiven Codes und Geräteliste mit Widerruf; Kachel in `AdminIndex.razor` ergänzen.

10. **Unit-/Integrationstests schreiben und bestehende Tests anpassen**
    - Voraussetzungen: Schritte 4–8; Testinfrastruktur vorhanden (`ApplicationDbContextTests`-Muster mit Shared-In-Memory-SQLite + `EventManager`, `ListLogger<T>`, `WebApplicationFactory<Program>` aus `ApiDocumentationContractTests`).
    - Beschreibung: Service-Tests (inkl. `GetActiveCodesAsync`-Filtertest, `DeviceName`-Negativtest, Negativtest für leeren/fehlenden `Code` und für Client-PublicKey falscher Kurve), Attribut-Tests, Controller-/Vertragstests inkl. `400`-Fällen und Prüfung der verbindlichen camelCase-JSON-Feldnamen (Details siehe Tests-Abschnitt).

11. **E2E-Tests für Admin-UI und Pairing-Gesamtfluss**
    - Voraussetzungen: Schritte 7–10; Playwright/Chromium-Infrastruktur und `MediaBoxContextMenuE2ETestBase` bzw. `SeedAdminAsync`-Muster (z. B. `UpdatesPageE2ETests`) vorhanden.
    - Beschreibung: Browser-Tests für `/admin/devices` (inkl. Nicht-Admin-Zugriff → „Nicht autorisiert."), Gesamtfluss Code → Exchange → Login, Widerruf, IP-Sperre sichtbar in `/admin/security`.

12. **Dokumentation aktualisieren**
    - Voraussetzungen: Schritte 7–9 (finaler Vertrag und UI stehen).
    - Beschreibung: `docs/API.md`, `docs/GUIDE_Installation.md`, `docs/help/einrichtung.md` (+ ggf. neue Hilfeseite), `docs/SECRETS_MANAGEMENT.md`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreatePairingCodeAsync_ReturnsCodeAndPersistsHash` | `PairingServiceTests` (neu, In-Memory-SQLite nach `ApplicationDbContextTests`-Muster) | Code-Länge/Alphabet, `ExpiresAtUtc` = TTL, Klartext nicht in DB |
| `ExchangeAsync_ValidRequest_ReturnsDecryptableTokenAndPersistsDevice` | `PairingServiceTests` | Erfolgspfad: `PairedDevice` mit Hash angelegt, Response per clientseitigem ECDH entschlüsselbar (Roundtrip) |
| `ExchangeAsync_UnknownCode_Fails` / `..._ExpiredCode_Fails` / `..._ConsumedCode_Fails` | `PairingServiceTests` | Ablehnung ohne `PairedDevice`-Eintrag |
| `ExchangeAsync_ConcurrentConsume_OnlyOneSucceeds` | `PairingServiceTests` | Atomarer Verbrauch (Race) |
| `ExchangeAsync_InvalidPublicKey_Fails` | `PairingServiceTests` | Formatfehler ohne Fehlversuchszählung |
| `ExchangeAsync_NonP256PublicKey_Fails` | `PairingServiceTests` | `ClientPublicKey` ist importierbares SPKI, aber falsche Kurve (z. B. `nistP384`) → Formatfehler ohne Fehlversuchszählung, kein `PairedDevice`-Eintrag |
| `ExchangeAsync_EmptyCode_Fails` | `PairingServiceTests` | `Code` leer/fehlend → Formatfehler (`400`-Pfad): Ergebnis ist Validierungsfehler, nicht „ungültiger Code"; kein `CodeHash`-Lookup, kein `PairedDevice`-Eintrag — sicherheitsrelevante Abgrenzung zu `401`/`RegisterFailure` |
| `ExchangeAsync_DeviceNameTooLong_Fails` | `PairingServiceTests` | `DeviceName` > 200 Zeichen wird als Formatfehler abgelehnt (kein `PairedDevice`-Eintrag, kein Fehlversuchszähler) |
| `GetActiveCodesAsync_ReturnsOnlyActiveCodes` | `PairingServiceTests` | Filterung der Metadatenliste: nur nicht abgelaufene (`ExpiresAtUtc` in der Zukunft) und nicht verbrauchte (`ConsumedAtUtc == null`) Codes; Ergebnis enthält nur Metadaten (Ablaufzeitpunkt, Ersteller) — kein `CodeHash`, kein Klartext-Code |
| `IssueAsync_StoresHashNotPlaintext` / `IsValidDeviceTokenAsync_ValidToken_SucceedsAndUpdatesLastUsed` / `..._RevokedToken_Fails` / `RevokeAsync_SetsRevokedAtUtc` / `GetDevicesAsync_ReturnsAll` | `DeviceTokenServiceTests` (neu) | Token-Lebenszyklus |
| `ApiTokenCheckAttribute_MauiOnly_AcceptsDeviceToken` / `..._RejectsRevokedDeviceToken` / `..._MauiConfigTokenStillAccepted` / `..._AnyClient_DoesNotTouchDeviceTokens` | `ApiTokenConfigurationTests` (erweitert) | Attribut mit `RequestServices` + echtem `DeviceTokenService` auf In-Memory-DB, Aufruf über `OnActionExecutionAsync` |
| `Exchange_RequiresNoAuth_Returns200OnValidCode` / `..._Returns401OnInvalidCode` / `..._Returns429AfterRepeatedFailures` / `Exchange_ResponseDecryptsToWorkingLoginToken` | `PairingExchangeContractTests` (neu, `WebApplicationFactory<Program>`, Environment `Testing`) | Öffentlicher Endpunkt Ende-zu-Ende auf HTTP-Ebene inkl. Gate-Nachweis (Token → `POST /api/auth/login` → 200) |
| `Exchange_Returns400OnMissingOrEmptyCode` / `..._Returns400OnDeviceNameTooLong` / `..._Returns400OnNonP256ClientPublicKey` | `PairingExchangeContractTests` | Request-Validierung auf HTTP-Ebene: fehlender/leerer `code`, `deviceName` > 200 Zeichen und Client-PublicKey falscher Kurve (importierbares SPKI, aber ≠ `nistP256`) → jeweils `400`; insbesondere der `code`-Fall darf kein `RegisterFailure`/keine IP-Sperre auslösen (400-vs-401-Abgrenzung, sicherheitsrelevant) |
| `Exchange_UsesBindingJsonFieldNames` | `PairingExchangeContractTests` | Verbindlicher JSON-Vertrag gegen das App-Repository: Request wird als Roh-JSON mit exakt den Feldnamen `code`, `clientPublicKey`, `deviceName` gesendet; Response-JSON enthält exakt `serverPublicKey` und `encryptedToken` (camelCase) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ApiTokenConfigurationTests.ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` | Ruft `OnActionExecuting` synchron; nach Umstellung auf `OnActionExecutionAsync` muss der Test die Async-Pipeline (mit `next`-Delegate) aufrufen. Erwartetes Verhalten (Unauthorized + kein Token im Log) bleibt. |
| `ApiDocumentationContractTests.ApiDocumentationContainsMauiRelevantRoutes` | Pflicht-Routenliste um `POST /api/pairing/exchange` erweitern (nach `docs/API.md`-Update). |
| `ApiDocumentationContractTests.MauiLogin_RejectsNonMauiApiTokens` | Keine Anpassung — bleibt unverändert gültiger Bestandstest: Die Test-Tokens (`test-legacy-api-token`, `test-web-api-token`) sind keine `PairedDevices`-Einträge und werden weiterhin mit `401` abgelehnt; der Test sichert die 400/401-relevante Gate-Semantik für Nicht-Maui-Tokens mit. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin meldet sich an, öffnet `/admin/devices`, erzeugt Pairing-Code; Code + TTL werden angezeigt | `DevicePairingE2ETests` (neu, `[Trait("Category","E2E")]`, Muster `MediaBoxContextMenuE2ETestBase`/`UpdatesPageE2ETests` mit `SeedAdminAsync`) | Admin kann Einmal-Code in der Web-UI erzeugen | Benutzerfluss nur über die Blazor-UI nachweisbar |
| Pflicht | Gesamtfluss: Code in UI erzeugt → Test-HttpClient führt ECDH-Exchange durch → entschlüsseltes Token → `POST /api/auth/login` mit `X-API-Key` erfolgreich | `DevicePairingE2ETests` | Gerät kann sich per Code ein Geräte-Token holen und damit einloggen | Kern der Anforderung; verbindet UI, Krypto-Vertrag und Gate über den echten Server (Kestrel) |
| Pflicht | Admin widerruft Gerät in `/admin/devices` → Login mit dem Geräte-Token → `401` | `DevicePairingE2ETests` | Widerruf sperrt das Gerät sofort | Anwender-sichtbare Aktion + Gate-Effekt |
| Pflicht | Ungültiger Code → Fehler; nach wiederholten Fehlversuchen ist die IP gesperrt (in `/admin/security` sichtbar). Der Test muss beide Teilnachweise im selben Szenario prüfen: HTTP-Fehler/Sperre am Endpunkt und Sichtbarkeit des Eintrags in `/admin/security` | `DevicePairingE2ETests` | Bruteforce-Schutz greift und ist für Admin sichtbar | Sichtbarer Fehlerfall über UI + Endpunkt |
| Pflicht | Benutzer ohne `IsAdmin`-Claim (Seeding-Muster `SeedRegularUserAsync` aus `BackupUploadE2ETests`, Zeilen 466–481) meldet sich an und öffnet `/admin/devices` → „Nicht autorisiert."; weder Pairing-Code-Schaltfläche noch Geräteliste/Widerruf-Aktionen werden gerendert | `DevicePairingE2ETests` | Admin-UI nur für Administratoren (`IsAdmin`-Claim, „Nicht autorisiert." für Nicht-Admins) | Berechtigungs-/Sichtbarkeitsregel ist über die UI erreichbar und anwenderrelevant |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — vorhandene E2E-Tests nutzen den Web-Login (Identity UI) und setzen keinen `X-API-Key`; `Jwt:ApiToken:Maui` bleibt als Fallback konfiguriert.

## Offene Punkte

Keine.
