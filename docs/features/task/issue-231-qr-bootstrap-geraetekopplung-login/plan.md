# Umsetzungsplan: QR-Bootstrap — Gerätekopplung + Login per Einmal-Ticket

## Übersicht

Issue #231 (App-Projekt Schritt 5a): Eingeloggte Benutzer erzeugen im Profilbereich (`Account/Manage/Devices`) ein Einmal-Bootstrap-Ticket (QR `https://<server>/pairing?t=<ticket>` + 8-Zeichen-Kurzcode). Die App löst es am neuen öffentlichen Endpunkt `POST api/pairing/bootstrap` ein und erhält ECDH-verschlüsselt Geräte-Token + JWT + Refresh-Token. Neu: `RefreshToken`-Entität mit Rotation (`POST api/auth/refresh`) und Widerruf (`POST api/auth/logout`, Gerätewiderruf). `PairingCode` wird um `Kind` und `TicketHash` erweitert; `api/pairing/exchange` bleibt vertraglich unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Bootstrap-Logik | Neuer `PairingBootstrapService` (`internal sealed`, Interface `IPairingBootstrapService`) statt Erweiterung des `PairingService` | Bootstrap braucht `UserManager`, `AuthorizationTokenService`, `IRefreshTokenService` — drei neue Abhängigkeiten, die der schlanke `PairingService` nicht hat; eigene Klasse hält SRP und lässt `PairingService` unverändert testbar (Transaction-Script-/Service-Layer-Muster des Repos) |
| Kurzcode↔Ticket | Ein `PairingCode`-Datensatz trägt **beide** Hashes: `CodeHash` = SHA-256 des 8-Zeichen-Alias, `TicketHash` = SHA-256 des langen Secrets (Base64url, 24 Byte = 192 Bit) | Alias/Lookup-Modell lt. Entscheidung; ein Datensatz → TTL/Single-Use/atomarer Verbrauch gelten für beide Eingabewege |
| `Kind`-Feld | Enum `PairingCodeKind { AdminCode = 0, BootstrapTicket = 1, DeviceInitiated = 2 }` als `int`-Spalte, Default 0 | Altdaten bleiben `AdminCode` → `exchange` unverändert; `DeviceInitiated` trägt die TV-Gegenrichtung im Modell (wird nirgends erzeugt/konsumiert) |
| `exchange`-Absicherung | Lookup in `ExchangeAsync` um `c.Kind == PairingCodeKind.AdminCode` ergänzen | Sonst würde ein Bootstrap-Kurzcode auch am Admin-Endpoint matchen (Vertrags-/Semantikbruch); Response-Vertrag unverändert |
| Verschlüsselter Bootstrap-Payload | JSON `{ "deviceToken", "token", "expires", "refreshToken" }` (camelCase), AES-256-GCM-Layout identisch zu `exchange` (Base64(`nonce`‖`ct`‖`tag`)) | App entschlüsselt beide Antworten mit derselben Routine; `expires` = UTC-ISO-8601 (`token.ValidTo`) |
| Refresh-Token-Service | `RefreshTokenService` mit nur `ApplicationDbContext` + `IConfiguration` | JWT-Erzeugung bleibt beim Aufrufer (Controller via `UserManager` + `AuthorizationTokenService`); Service bleibt ohne Identity-Infrastruktur testbar (`PairingTestDb`-Muster) |
| Refresh-Rotation | Atomar via `ExecuteUpdateAsync` (`RevokedAtUtc == null && ExpiresAtUtc > now` → `RevokedAtUtc` + `ReplacedByHash` setzen); Reuse-Detection: bereits rotiertes Token → Familie (`UserId`+`DeviceId`) komplett sperren | `ExecuteUpdateAsync`-Muster des Repos; Reuse-Detection ist Standard-Härtung klassischer Refresh-Tokens mit ~5 Zeilen Aufwand |
| `IDeviceTokenService.IssueAsync` Rückgabe | `IssuedDeviceToken { Token, DeviceId }` statt `string` | Bootstrap braucht `DeviceId` für den Refresh-Token; interne Schnittstelle, Aufrufer (`ExchangeAsync`, Tests) werden angepasst — sauberer als Zweit-Query auf `TokenHash` |
| Gerätewiderruf | `DeviceTokenService` erhält `IRefreshTokenService`-Abhängigkeit, `RevokeAsync` ruft `RevokeAllForDeviceAsync` | Kein Zyklus (`RefreshTokenService` kennt `DeviceTokenService` nicht); Widerruf wirkt sofort auf Refresh-Pfad |
| Rate-Limit je Benutzer | DB-basiert: Zählen `PairingCodes` mit `Kind == BootstrapTicket`, `CreatedByUserId`, `CreatedAtUtc > now-1h` ≥ `Pairing:BootstrapMaxTicketsPerHour` (Default 10) | Persistenz über Prozessneustarts; kein zusätzlicher Cache nötig |
| Admin-Only | `Pairing:BootstrapAdminOnly` (Default `false`), geprüft in `CreateBootstrapTicketAsync` anhand `ApplicationUser.IsAdmin` | Entscheidung OP-7; Service-Signatur nimmt `isAdmin` entgegen (kein HttpContext im Service) |
| Bootstrap-Endpunkt | Im `PairingController` als `POST api/pairing/bootstrap`, öffentlich, `ILoginIpBlockService` wie `exchange` (429 ab 5 Fehlversuchen, 400 ohne Zählung, 401 mit Zählung) | Gleiches anonymes Profil; Kurzcode-Alias (≈40 Bit) braucht denselben Brute-Force-Schutz |
| Refresh-/Logout-Endpunkte | Im `AuthController`: `POST api/auth/refresh` + `POST api/auth/logout`, beide `[ApiTokenCheck(MauiOnly)]` | App besitzt nach Bootstrap ein Geräte-Token (Gate) — konsistent zu `api/auth/login`; kein neuer öffentlicher Angriffspunkt |
| Profilseite | Neue SSR-Seite `Components/Account/Pages/Manage/Devices.razor` (`/Account/Manage/Devices`) mit `EditForm`/`method="post"` nach Muster `Manage/Index.razor` + `ManageNavMenu`-Eintrag „Geräte" | `AccountLayout` erzwingt Reload bei fehlendem `HttpContext` → `InteractiveServer` unmöglich; SSR-Formular ist das etablierte Manage-Muster |
| QR-Rendering | `QRCoder` **1.8.0** (`PngByteQRCode` → PNG-Bytes → Data-URL) | MIT-Lizenz, publiziert 2026-04-04 (>7 Tage), Null-Abhängigkeiten, kein `System.Drawing`/SkiaSharp nötig, net5.0+/netstandard1.3 → net10.0-kompatibel |
| Server-Adresse im QR | `HttpContext.Request.Scheme` + `Request.Host`; Loopback (`localhost`/`127.*`/`::1`) → Warnhinweis auf der Seite (QR wird trotzdem gerendert) | Entscheidung: Header-Vertrauen ok für App-Topologie, aber benennen; Host-Header wird bereits früh validiert |
| Verbleibende Gültigkeit | Statische Anzeige „Gültig bis HH:mm:ss Uhr (noch ca. X Minuten)" (Lokalzeit) | SSR ohne JS-Countdown; hinreichend für 5-min-TTL |

## Programmabläufe

### Ticket erzeugen (Profilseite)

1. Benutzer öffnet `/Account/Manage/Devices` (SSR, `[Authorize]` via Manage-`_Imports`); `IdentityUserAccessor.GetUserOrRedirectAsync` liefert `ApplicationUser`.
2. Benutzer klickt „Gerät koppeln" → `EditForm`-Post → `CreateTicketAsync` im `@code`-Block.
3. `IPairingBootstrapService.CreateBootstrapTicketAsync(user.Id, user.IsAdmin)`:
   - `Pairing:BootstrapAdminOnly` && `!isAdmin` → `BootstrapTicketResult` mit `Error = Forbidden`.
   - Rate-Limit-Zählung (letzte Stunde) ≥ `Pairing:BootstrapMaxTicketsPerHour` → `Error = RateLimited`.
   - Ticket = `Base64Url(RandomBytes(24))`, Kurzcode = 8 Zeichen aus dem bestehenden Alphabet; Datensatz `PairingCode { Kind = BootstrapTicket, TicketHash = sha256(ticket), CodeHash = sha256(kurzcode), CreatedByUserId, ExpiresAtUtc = now + Pairing:BootstrapTicketTtlMinutes }`.
4. Seite rendert: QR-PNG-Data-URL aus `"{scheme}://{host}/pairing?t={ticket}"` (QRCoder), Kurzcode (`data-testid`), Anleitung, „Gültig bis"; bei Loopback-Host Warnhinweis; bei `Forbidden`/`RateLimited` verständliche Fehlermeldung.

### Bootstrap-Einlösung (App)

1. `POST api/pairing/bootstrap` mit `{ ticket, clientPublicKey, deviceName? }` (öffentlich).
2. `PairingController.Bootstrap`: `ILoginIpBlockService.IsBlocked` → 429.
3. `IPairingBootstrapService.BootstrapAsync`:
   - `ticket`/`clientPublicKey` leer oder `deviceName` > 200 (getrimmt) oder SPKI-Import/P-256-Prüfung scheitert → `InvalidRequest` → 400 (keine Fehlversuchszählung).
   - Lookup `Kind == BootstrapTicket && (TicketHash == h || CodeHash == h)`; nicht gefunden → `InvalidCode` → 401 + `RegisterFailure`.
   - Atomarer Verbrauch `ExecuteUpdateAsync` (`ConsumedAtUtc == null && ExpiresAtUtc > now`); 0 Zeilen → `InvalidCode` → 401 + `RegisterFailure`.
   - `UserManager.FindByIdAsync(CreatedByUserId)` → `null` → `InvalidCode` → 401.
   - `IDeviceTokenService.IssueAsync` → `IssuedDeviceToken`; `AuthorizationTokenService.CreateToken(user)`; `IRefreshTokenService.IssueAsync(user.Id, device.DeviceId)`.
   - Payload-JSON serialisieren, ECDH-Serverkey + `DeriveKeyFromHmac` + AES-256-GCM (Helfer wiederverwenden) → `serverPublicKey` + `encryptedPayload`.
4. 200 + `PairingBootstrapResponse`; `RegisterSuccess`.

### Session-Erneuerung (App)

1. `POST api/auth/refresh` mit `X-API-Key: <geraeteToken>` + `{ refreshToken }`.
2. `IRefreshTokenService.RotateAsync`: Hash-Lookup → unbekannt → `InvalidToken` (401); Token bereits `RevokedAtUtc` gesetzt → Reuse-Detection: `RevokeFamilyAsync(userId, deviceId)` → `InvalidToken`; zugehöriges `PairedDevice` `RevokedAtUtc != null` → `InvalidToken`; atomar `RevokedAtUtc`+`ReplacedByHash` setzen, neuen Token ausstellen.
3. Controller lädt `ApplicationUser` via `UserManager`, erzeugt JWT → `RefreshTokenResponse { token, expires, refreshToken }`.
4. `POST api/auth/logout` mit `X-API-Key` + `{ refreshToken }` → `RevokeAsync` (idempotent, immer 200 — kein Token-Orakel).
5. Admin widerruft Gerät (`/admin/devices`) → `DeviceTokenService.RevokeAsync` → `RevokeAllForDeviceAsync` → alle aktiven Refresh-Tokens des Geräts ungültig.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PairingCodeKind` | Enum (`Data/`) | `AdminCode = 0`, `BootstrapTicket = 1`, `DeviceInitiated = 2` (reserviert) |
| `RefreshToken` | Datenmodell (`Data/RefreshToken.cs`) | `Id`, `TokenHash`, `UserId`, `DeviceId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByHash` |
| `RefreshTokenConfiguration` | EF-Konfiguration (`Data/Configurations/`) | PK, `HasMaxLength`, unique Index `TokenHash`, Index `DeviceId`/`RevokedAtUtc` |
| `IPairingBootstrapService` / `PairingBootstrapService` | Interface + `internal sealed` (`Services/`) | `CreateBootstrapTicketAsync`, `BootstrapAsync` |
| `BootstrapTicketErrorKind` | Enum (`Services/`) | `None`, `InvalidRequest`, `InvalidTicket`, `Forbidden`, `RateLimited` |
| `BootstrapTicketResult` | Result-Klasse (`Services/`) | `Success`, `Error`, `ServerPublicKey`, `EncryptedPayload` |
| `CreatedBootstrapTicket` | Result-Klasse (`Services/`) | `Ticket`, `ShortCode`, `CreatedAtUtc`, `ExpiresAtUtc` |
| `IRefreshTokenService` / `RefreshTokenService` | Interface + `internal sealed` (`Services/`) | `IssueAsync`, `RotateAsync`, `RevokeAsync`, `RevokeAllForDeviceAsync` |
| `IssuedDeviceToken` | Result-Klasse (`Services/`) | `Token`, `DeviceId` (Rückgabe von `IssueAsync`) |
| `IssuedRefreshToken` | Result-Klasse (`Services/`) | `Token`, `ExpiresAtUtc` |
| `RefreshRotationResult` | Result-Klasse (`Services/`) | `Success`, `UserId`, `DeviceId`, `NewToken`, `NewExpiresAtUtc` |
| `PairingBootstrapRequest` | DTO (`VideoWebPlayer.Client/Models/`) | `Ticket`, `ClientPublicKey`, `DeviceName?` → `ticket`, `clientPublicKey`, `deviceName` |
| `PairingBootstrapResponse` | DTO | `ServerPublicKey`, `EncryptedPayload` → `serverPublicKey`, `encryptedPayload` |
| `PairingBootstrapPayload` | DTO (entschlüsselter Inhalt) | `DeviceToken`, `Token`, `Expires`, `RefreshToken` → `deviceToken`, `token`, `expires`, `refreshToken` |
| `RefreshTokenRequest` | DTO | `RefreshToken` → `refreshToken` |
| `RefreshTokenResponse` | DTO | `Token`, `Expires`, `RefreshToken` → `token`, `expires`, `refreshToken` |
| `Components/Account/Pages/Manage/Devices.razor` | Blazor-SSR-Seite | „Geräte"-Abschnitt mit „Gerät koppeln" |

## Änderungen an bestehenden Klassen

### `PairingCode` (Datenmodell)

- **Neue Eigenschaften:** `Kind` (`PairingCodeKind`, Default `AdminCode`) — Pairing-Richtung; `TicketHash` (`string?`, max. 128) — SHA-256 des langen Bootstrap-Secrets.

### `PairingCodeConfiguration` (EF)

- `TicketHash` max. 128 + Index; `Kind` Index (Filter pro Art).

### `PairingService` (Logik)

- **Geänderte Methoden:** `ExchangeAsync` — Lookup um `c.Kind == PairingCodeKind.AdminCode` ergänzt (verhindert Bootstrap-Alias am Admin-Endpoint); `GetActiveCodesAsync` — Filter `Kind == AdminCode` (Admin-Liste zeigt nur Admin-Codes).

### `DeviceTokenService` / `IDeviceTokenService` (Logik)

- **Geänderte Methoden:** `IssueAsync` — Rückgabe `IssuedDeviceToken` statt `string`; `RevokeAsync` — ruft zusätzlich `IRefreshTokenService.RevokeAllForDeviceAsync(deviceId)`.
- **Neue Abhängigkeit:** `IRefreshTokenService` im Konstruktor.

### `PairingController` (Controller)

- **Neue Methoden:** `Bootstrap(PairingBootstrapRequest, ct)` — öffentlicher `POST api/pairing/bootstrap`, IP-Block-Prüfung, Fehler-Mapping 400/401/429 analog `Exchange`.

### `AuthController` (Controller)

- **Neue Methoden:** `Refresh(RefreshTokenRequest)` — `POST api/auth/refresh` `[ApiTokenCheck(MauiOnly)]`, 401 bei ungültigem Token, sonst `RefreshTokenResponse`; `Logout(RefreshTokenRequest)` — `POST api/auth/logout` `[ApiTokenCheck(MauiOnly)]`, widerruft idempotent, immer 200.

### `ServiceCollectionExtensions` (DI)

- `AddScoped<IPairingBootstrapService, PairingBootstrapService>()`, `AddScoped<IRefreshTokenService, RefreshTokenService>()`.

### `ManageNavMenu.razor` (UI)

- `NavLink` „Geräte" → `Account/Manage/Devices`.

### `ApplicationDbContext` (Datenmodell)

- `DbSet<RefreshToken> RefreshTokens`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPairingBootstrapAndRefreshTokens` | `PairingCodes.Kind` (int, Default 0), `PairingCodes.TicketHash` (nvarchar(128), null, Index), `IX_PairingCodes_Kind`; neue Tabelle `RefreshTokens` (PK `Id`, `TokenHash` unique, `UserId`, `DeviceId` Index, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc` Index, `ReplacedByHash`) | Altdaten: bestehende `PairingCodes` → `Kind = 0` (`AdminCode`), `TicketHash = null`; `dotnet ef database update` beim Start via `MigrateDatabase()` |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `PairingBootstrapRequest.Ticket` | nicht leer | 400 `InvalidRequest` |
| `PairingBootstrapRequest.ClientPublicKey` | nicht leer, SPKI-importierbar, Kurve nistP256 | 400 `InvalidRequest` |
| `PairingBootstrapRequest.DeviceName` | getrimmt ≤ 200 Zeichen | 400 `InvalidRequest` |
| Ticket (lang oder Kurzcode) | vorhanden, `Kind == BootstrapTicket`, nicht verbraucht, nicht abgelaufen — atomar | 401 `InvalidTicket` |
| `CreatedByUserId` des Tickets | Benutzer existiert noch | 401 `InvalidTicket` |
| Ticket-Erzeugung | `BootstrapAdminOnly` → `IsAdmin` nötig; Rate-Limit ≤ N/h | `Forbidden` / `RateLimited` (UI-Meldung) |
| `RefreshTokenRequest.RefreshToken` | nicht leer; vorhanden, nicht widerrufen/abgelaufen, Gerät aktiv | 400 bzw. 401 |
| Config-Werte | TTL/Limit/TtlDays auf Mindestwert 1 geklemmt | — |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Pairing:BootstrapTicketTtlMinutes` | int | `5` | TTL des Bootstrap-Tickets |
| `Pairing:BootstrapMaxTicketsPerHour` | int | `10` | Rate-Limit je Benutzer |
| `Pairing:BootstrapAdminOnly` | bool | `false` | Nur Admins dürfen Tickets erzeugen |
| `Auth:RefreshTokenTtlDays` | int | `30` | Lebensdauer Refresh-Token |
| Paket `QRCoder` | NuGet | `1.8.0` | QR-PNG-Erzeugung (MIT, keine Abhängigkeiten) |

## Seiteneffekte und Risiken

- **`exchange`-Vertrag:** bleibt nach außen unverändert (Request/Response identisch); intern wird `Kind == AdminCode` gefiltert — Bootstrap-Kurzcodes werden dort abgelehnt (401), was der Spezifikation entspricht.
- **`IDeviceTokenService.IssueAsync`-Signatur:** interne Änderung (`IssuedDeviceToken`) — betrifft `ExchangeAsync` und `DeviceTokenServiceTests`.
- **`DeviceTokenService`-Konstruktor:** neue `IRefreshTokenService`-Abhängigkeit — betrifft DI (registriert) und `PairingTestDb`/`DeviceTokenServiceTests`-Aufbau.
- **Host-Header-Vertrauen:** QR-Adresse stammt aus `Scheme`/`Host` — bei Reverse-Proxy/Fehlkonfiguration kann die Adresse unbrauchbar sein; Loopback-Fall wird mit Hinweis abgefangen, im Review/REST-Doku benannt.
- **Brute-Force auf Kurzcode-Alias:** ≈40-Bit-Raum, mit `ILoginIpBlockService` abgesichert (geteilter Schwellenwert mit Login/exchange) — dokumentiert.
- **JWT-Gültigkeit nach Gerätewiderruf:** ausgestellte JWTs bleiben bis 12 h gültig (bestehendes, dokumentiertes Verhalten); Refresh-Tokens werden dagegen sofort gesperrt — asymmetrisch, aber konsistent zur Bestandssemantik.

## Umsetzungsreihenfolge

1. **NuGet-Paket `QRCoder` 1.8.0 ins `VideoWebPlayer.csproj`**
   - Voraussetzungen: keine.
   - `PngByteQRCode` für PNG-Bytes ohne System.Drawing.
2. **Datenmodell:** `PairingCodeKind`, `PairingCode` (`Kind`, `TicketHash`), `RefreshToken`, `RefreshTokenConfiguration`, `PairingCodeConfiguration`-Erweiterung, `ApplicationDbContext.RefreshTokens`
   - Voraussetzungen: keine.
3. **EF-Migration `AddPairingBootstrapAndRefreshTokens`** (`dotnet ef migrations add --project VideoWebPlayer`)
   - Voraussetzungen: Schritt 2.
4. **`IssuedDeviceToken` + `IDeviceTokenService.IssueAsync`-Rückgabe; `IRefreshTokenService`/`RefreshTokenService` inkl. Reuse-Detection; `DeviceTokenService` erweitern (Konstruktor + `RevokeAsync` → `RevokeAllForDeviceAsync`)**
   - Voraussetzungen: Schritt 2. Bestehende `DeviceTokenServiceTests`/`PairingTestDb` anpassen.
5. **`PairingService`: `Kind`-Filter in `ExchangeAsync` + `GetActiveCodesAsync`**
   - Voraussetzungen: Schritt 2.
6. **DTOs (`VideoWebPlayer.Client/Models`): `PairingBootstrapRequest`, `PairingBootstrapResponse`, `PairingBootstrapPayload`, `RefreshTokenRequest`, `RefreshTokenResponse`**
   - Voraussetzungen: keine.
7. **`IPairingBootstrapService`/`PairingBootstrapService`: `CreateBootstrapTicketAsync` (Admin-Only, Rate-Limit, Secret+Alias), `BootstrapAsync` (Lookup beider Hashes, atomarer Verbrauch, Token-Trio, AES-GCM-Payload); ECDH-/AES-Helfer mit `PairingService` teilen (interne Hilfsmethoden in gemeinsame `internal static` Klasse oder protected-scope-Duplikat vermeiden → Helferklasse `PairingCrypto` in `Services/Security/` analog `HashHelper`)**
   - Voraussetzungen: Schritte 2, 4, 6.
8. **`PairingController.Bootstrap` + `AuthController.Refresh`/`Logout`; DI-Registrierung**
   - Voraussetzungen: Schritte 4, 7.
9. **Profilseite `Manage/Devices.razor` + `ManageNavMenu`-Eintrag**
   - Voraussetzungen: Schritte 1, 7.
10. **Unit-/Service-Tests** (`PairingServiceTests_Bootstrap*`, `RefreshTokenServiceTests`, `DeviceTokenServiceTests`-Erweiterung)
    - Voraussetzungen: Schritte 4–8.
11. **Contract-Tests** (`PairingBootstrapContractTests`, Refresh/Logout über `PairingWebApplicationFactory`, `ApiDocumentationContractTests`-Erweiterung — letztere erst nach Schritt 13 grün)
    - Voraussetzungen: Schritte 6–8.
12. **E2E-Tests** (`ProfilePairingE2ETests` Playwright: Ticket erzeugen + QR/Code/TTL sichtbar; Gesamtfluss Ticket → bootstrap → entschlüsseltes Trio → Login + Refresh-Rotation)
    - Voraussetzungen: Schritte 8–9, Browser vorhanden (Baseline bestätigt).
13. **Dokumentation** (`docs/API.md` Vertrag, `docs/help/`, README/Release Notes/Install-Guide)
    - Voraussetzungen: Implementierung final.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateBootstrapTicketAsync_ReturnsTicketShortCodeAndPersistsHashes` | `PairingServiceTests_BootstrapTicket` | Ticket ≥ 128 Bit (Base64url-Länge), `Kind`, beide Hashes, `CreatedByUserId`, TTL |
| `CreateBootstrapTicketAsync_TtlConfig_ClampsAndApplies` | gleich | `Pairing:BootstrapTicketTtlMinutes` (inkl. Minimum) |
| `CreateBootstrapTicketAsync_RateLimitExceeded_Fails` | gleich | > N Tickets/h → `RateLimited` |
| `CreateBootstrapTicketAsync_AdminOnly_NonAdminForbidden_AdminOk` | gleich | `Pairing:BootstrapAdminOnly` |
| `BootstrapAsync_ValidTicket_ReturnsDecryptableTrio` | `PairingServiceTests_Bootstrap` | ECDH-Roundtrip; Payload enthält `deviceToken`, `token` (gültiges JWT), `expires`, `refreshToken`; Ticket verbraucht; Gerät persistiert |
| `BootstrapAsync_ShortCodeAlias_ResolvesSameTicket` | gleich | 8-Zeichen-Alias löst dasselbe Ticket auf (Single-Use geteilt) |
| `BootstrapAsync_UnknownOrWrongKindTicket_Fails` | gleich | Admin-Code am bootstrap → `InvalidTicket`; Bootstrap-Ticket am `ExchangeAsync` → `InvalidCode` |
| `BootstrapAsync_ExpiredTicket_Fails`, `_AlreadyConsumed_Fails`, `_ConcurrentConsume_OnlyOneSucceeds` | gleich | TTL/Single-Use/Atomarität |
| `BootstrapAsync_InvalidClientKey_Fails`, `_DeviceNameTooLong_Fails`, `_MissingTicket_Fails` | gleich | 400-Pfade ohne Fehlversuchszählung |
| `BootstrapAsync_DeletedUser_Fails` | gleich | `CreatedByUserId` nicht auflösbar → `InvalidTicket` |
| `RefreshTokenServiceTests` | neu (`PairingTestDb`-Muster) | Issue+Hash, Rotation (alt widerrufen + `ReplacedByHash`), unbekannt/abgelaufen/widerrufen → Fehler, Reuse-Detection sperrt Familie, Gerätewiderruf → Refresh schlägt fehl, `RevokeAsync` idempotent, `RevokeAllForDeviceAsync` |
| `DeviceTokenServiceTests.RevokeAsync_RevokesDeviceRefreshTokens` | bestehend | Verknüpfung Widerruf → Refresh-Sperre |
| `PairingBootstrapContractTests` | neu (Basis `PairingExchangeContractTestBase`-Muster) | öffentlich ohne Auth; 200 + camelCase-Felder via Roh-JSON; 400/401/429-Mapping; entschlüsseltes Trio → `api/auth/login` mit `deviceToken` + JWT + `api/auth/refresh` Rotation |
| `AuthRefreshContractTests` | neu | `refresh` ohne `X-API-Key` → 401; mit Geräte-Token Rotation erfolgreich; widerrufenes/wiederverwendetes Token → 401; `logout` → danach 401 am refresh; `logout` unbekanntes Token → 200 |
| `ProfilePairingE2ETests` (Playwright, Category E2E) | neu | Benutzer-Login → `/Account/Manage/Devices` → „Gerät koppeln" → QR-`<img>` + Kurzcode + „Gültig bis" sichtbar; Gesamtfluss Kurzcode → `api/pairing/bootstrap` → Trio → Login + Refresh; Rate-Limit-Meldung nach N Erzeugungen (konfigurierbar klein setzen) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `DeviceTokenServiceTests` (alle `IssueAsync`-Aufrufe) | Rückgabetyp `IssuedDeviceToken` |
| `PairingTestDb.CreatePairingService` / `CreateService`-Helfer | `DeviceTokenService` braucht `IRefreshTokenService`; ggf. neue Factory für `PairingBootstrapService`/`RefreshTokenService` |
| `PairingServiceTests_*` (`CreatePairingService`) | Konstruktoraufbau über `PairingTestDb` |
| `ApiDocumentationContractTests.ApiDocumentationContainsMauiRelevantRoutes` | neue Routen `POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout` in `docs/API.md` + Liste |
| `PairingExchangeContractTests_*` | `exchange` unverändert — Regression, muss ohne Anpassung grün bleiben |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Eingeloggter Benutzer erzeugt Ticket auf der Profilseite, sieht QR + Kurzcode + Gültigkeit | `ProfilePairingE2ETests.User_Creates_Bootstrap_Ticket_And_Sees_Qr_ShortCode_Ttl` | AK1 | Benutzerfluss über Browser-UI |
| Pflicht | Gesamtfluss: UI-Ticket → `api/pairing/bootstrap` → Entschlüsselung → Geräte-Token-Login + Refresh-Rotation | `ProfilePairingE2ETests.Bootstrap_Flow_UiTicket_Exchange_Login_Refresh_Succeeds` | AK2, AK4 | End-to-End-Vertrag inkl. Krypto |
| Pflicht | Rate-Limit je Benutzer sichtbar (nach N Erzeugungen erscheint Meldung statt Ticket) | `ProfilePairingE2ETests.Rate_Limit_Shows_Message_On_Profile_Page` | AK1 (Rate-Limit) | sichtbare Berechtigungs-/Limitregel über UI |
| Pflicht | `exchange` bleibt funktionsfähig (Admin-Code-Flow) | bestehend `DevicePairingE2ETests` | Regression | vorhanden, kein neuer Test |

Keine bestehenden E2E-Tests müssen angepasst werden.

## Offene Punkte

Keine — alle Entscheidungen sind im Issue/App-Projektplan festgelegt; Vertragsdetails (Feldnamen `ticket`/`serverPublicKey`/`encryptedPayload`, Payload `deviceToken`/`token`/`expires`/`refreshToken`, `refreshToken`-Request/-Response) werden hier verbindlich fixiert und in `docs/API.md` für den App-Schritt 5b dokumentiert.
