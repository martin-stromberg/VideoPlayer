# Logik — Bestandsaufnahme

## `PairingService` (`internal sealed`)
Datei: `VideoWebPlayer/Services/PairingService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreatePairingCodeAsync(createdByUserId, ct)` | public | Erzeugt 8-Zeichen-Code (Alphabet `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, `Pairing:CodeLength` Default 8, `Pairing:CodeTtlMinutes` Default 5; auf Mindestwerte geklemmt), speichert SHA-256-Hash, gibt `CreatedPairingCode` (Klartext + Zeiten) zurück |
| `ExchangeAsync(request, ct)` | public | Validiert Request (400-Fälle), importiert Client-ECDH-Key (nur nistP256), sucht `PairingCode` per `CodeHash`, atomarer Verbrauch via `ExecuteUpdateAsync` (`ConsumedAtUtc == null && ExpiresAtUtc > now`), ECDH-Serverkey + `DeriveKeyFromHmac` SHA-256, `IDeviceTokenService.IssueAsync`, AES-256-GCM `EncryptToken` → `PairingExchangeResult` |
| `GetActiveCodesAsync(ct)` | public | Aktive Codes als `PairingCodeInfo` (kein Hash/Klartext) |
| `TryImportClientPublicKey(base64)` | private static | SPKI-Import + P-256-Oid-Prüfung; Fehler → `null` (→ 400) |
| `EncryptToken(aesKey, token)` | private static | AES-256-GCM → Base64(12B-Nonce ‖ CT ‖ 16B-Tag) |

Result-Typen in derselben Datei: `PairingExchangeErrorKind` (`None`/`InvalidRequest`/`InvalidCode`), `PairingExchangeResult` (`Success`, `Error`, `ServerPublicKey`, `EncryptedToken`), `CreatedPairingCode`, `PairingCodeInfo`.

Abhängigkeiten: `ApplicationDbContext`, `IDeviceTokenService`, `IConfiguration`. **Kein** `UserManager`, kein `AuthorizationTokenService`.

## `PairingController`
Datei: `VideoWebPlayer/Controllers/PairingController.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `POST api/pairing/exchange` | Öffentlich (kein `ApiTokenCheck`/`BearerTokenCheck`); `ILoginIpBlockService.IsBlocked` → 429; `InvalidRequest` → 400 (ohne Fehlversuchszählung); `InvalidCode` → 401 + `RegisterFailure`; Erfolg → `RegisterSuccess` + `PairingExchangeResponse` |

## `DeviceTokenService` (`internal sealed`)
Datei: `VideoWebPlayer/Services/DeviceTokenService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `IssueAsync(deviceName, createdByUserId, ct)` | Base64url(32B)-Token, SHA-256-Hash in `PairedDevices`, Fallback-Name „Geraet vom …" |
| `IsValidDeviceTokenAsync(token, ct)` | Hash-Lookup, `RevokedAtUtc == null`, setzt `LastUsedAtUtc` |
| `RevokeAsync(deviceId, ct)` | Setzt `RevokedAtUtc` (kennt keine Refresh-Tokens) |
| `RenameAsync(deviceId, newName, ct)` | Trim + 1–200 Zeichen |
| `GetDevicesAsync(ct)` | Alle Geräte, neueste zuerst |

## `AuthService` / `AuthorizationTokenService`
Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `AuthorizationTokenService.CreateToken(user)` | JWT (HmacSha256, Issuer=Audience `Jwt:Issuer`, Claims `Sub`/`NameIdentifier`/`Email`/`Name`/`IsAdmin`), **12 h, kein Refresh** → `AuthorizationToken { token, expires }` (Kleinschreibung!) |
| `AuthService.LoginAsync(request)` | `UserManager.FindByEmailAsync` + `CheckPasswordSignInAsync` → `CreateToken` |
| `AuthService.ImpersonateAsync(request)` | `CheckAdmin` → `FindByEmailAsync` → `CreateToken` — Muster „JWT ohne Passwort" |
| `AuthService.CurrentUser` | User aus HttpContext (Cookie-Auth) |

Registrierung: `AuthorizationTokenService` scoped in `ServiceCollectionExtensions` (Z. ~70); `Jwt:Key` → `SymmetricSecurityKey` scoped.

## `LoginIpBlockService` (Singleton)
Datei: `VideoWebPlayer/Services/LoginIpBlockService.cs`

`IsBlocked`/`RegisterFailure`/`RegisterSuccess`/`Unblock`/`GetBlockedIps`; `ConcurrentDictionary` + DB-Persistenz (`BlockedLoginIps`); Schwelle 5; `Unblock` setzt auch Sub-Schwellen-Zähler zurück. Kein benutzerbasiertes Rate-Limit vorhanden.

## `ApiTokenCheckAttribute`
Datei: `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`

Prüft `X-API-Key`; Scope `MauiOnly` akzeptiert `Jwt:ApiToken:Maui` **oder** gültiges Geräte-Token via `IDeviceTokenService.IsValidDeviceTokenAsync` (aus `RequestServices`). Scope `AnyClient` zusätzlich `Jwt:ApiToken`/`Jwt:ApiToken:Web`.

## `BearerTokenCheckAttribute`
Datei: `VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`

JWT-Validierung (Issuer/Audience/Lifetime/Signatur via `SymmetricSecurityKey` aus DI).

## `AuthController`
Datei: `VideoWebPlayer/Controllers/AuthController.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `POST api/auth/login` | `[ApiTokenCheck(MauiOnly)]` → `LoginAsync` → `AuthorizationToken` |
| `POST api/auth/impersonate` | `[ApiTokenCheck]` + `[BearerTokenCheck]` → `ImpersonateAsync` |

## `WhitelistIpMiddleware` / `FirstUserRedirectMiddleware` / `Host-Validierung`
`UseVideoWebPlayer` (`Extensions/WebApplicationExtensions.cs`): Host-Header-Validierung (400 bei ungültigem Host), `UseWhitelistIp`, Auth, `RestoreInProgressMiddleware`, `FirstUserRedirectMiddleware`, dann Razor-Components + `MapControllers`. **Keine** dedizierte „gate"-Middleware — das Gate ist der `ApiTokenCheckAttribute` pro Endpunkt; `exchange` (und künftig `bootstrap`) tragen bewusst kein Attribut.

## DI-Registrierung
`Extensions/ServiceCollectionExtensions.cs`: `ILoginIpBlockService` Singleton, `IDeviceTokenService`/`IPairingService` scoped, `AuthorizationTokenService` scoped, Policy `AdminOnly` (`IsAdmin`-Claim).
