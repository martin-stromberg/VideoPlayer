# Interfaces — Bestandsaufnahme

## `IPairingService`
Datei: `VideoWebPlayer/Services/PairingService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreatePairingCodeAsync` | `string? createdByUserId`, `CancellationToken` | `Task<CreatedPairingCode>` | Einmal-Code erzeugen (Admin-Flow) |
| `ExchangeAsync` | `PairingExchangeRequest`, `CancellationToken` | `Task<PairingExchangeResult>` | Code → verschlüsseltes Geräte-Token |
| `GetActiveCodesAsync` | `CancellationToken` | `Task<IReadOnlyList<PairingCodeInfo>>` | Aktive Codes (Metadaten) |

## `IDeviceTokenService`
Datei: `VideoWebPlayer/Services/DeviceTokenService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IssueAsync` | `string? deviceName`, `string? createdByUserId`, `CancellationToken` | `Task<string>` | Geräte-Token ausstellen (Klartext einmalig) |
| `IsValidDeviceTokenAsync` | `string token`, `CancellationToken` | `Task<bool>` | Gate-Prüfung, setzt `LastUsedAtUtc` |
| `RevokeAsync` | `int deviceId`, `CancellationToken` | `Task<bool>` | Gerät widerrufen |
| `RenameAsync` | `int deviceId`, `string newName`, `CancellationToken` | `Task<bool>` | Gerät umbenennen |
| `GetDevicesAsync` | `CancellationToken` | `Task<IReadOnlyList<PairedDevice>>` | Geräteliste |

## `IAuthService`
Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CurrentUser` (Property) | — | `ApplicationUser?` | Eingeloggter Benutzer (Cookie-Kontext) |
| `LoginAsync` | `AuthenticationRequest` | `Task<AuthorizationToken>` | Passwort-Login → JWT |
| `ImpersonateAsync` | `ImpersonateRequest` | `Task<AuthorizationToken>` | Admin-Impersonation → JWT |

## `ILoginIpBlockService`
Datei: `VideoWebPlayer/Services/LoginIpBlockService.cs`

| Methode | Zweck |
|---------|-------|
| `IsBlocked(IPAddress?)` | IP gesperrt? |
| `RegisterFailure(IPAddress?)` | Fehlversuch zählen (ab 5 → Sperre + DB) |
| `RegisterSuccess(IPAddress?)` | Zähler zurücksetzen (nicht bei aktiver Sperre) |
| `GetFailureCount(IPAddress?)` | Fehlerzähler |
| `GetBlockedIps()` | Gesperrte IPs |
| `Unblock(string ip)` | Sperre + Zähler aufheben |

## `IRefreshTokenService` — **nicht vorhanden**, neu anzulegen.
