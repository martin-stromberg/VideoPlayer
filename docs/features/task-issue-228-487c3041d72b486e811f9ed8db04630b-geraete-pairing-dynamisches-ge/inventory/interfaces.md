# Interfaces — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

Es existiert noch kein `IPairingService`/`IDeviceTokenService`. Relevante vorhandene Contracts:

## `ILoginIpBlockService`

Datei: `VideoWebPlayer/Services/LoginIpBlockService.cs` (Zeilen 12–47, Namespace `VideoWebPlayer.Services`; Implementierung `internal sealed class LoginIpBlockService` in derselben Datei)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsBlocked` | `IPAddress? ip` | `bool` | Prüft, ob die IP aktuell gesperrt ist (Cache + DB-Fallback) |
| `RegisterFailure` | `IPAddress? ip` | `void` | Zählt Fehlversuch; ab 5 (Konstante `Threshold`) persistente Sperre in `BlockedLoginIps` |
| `RegisterSuccess` | `IPAddress? ip` | `void` | Entfernt Zähler, sofern nicht gesperrt |
| `GetFailureCount` | `IPAddress? ip` | `int` | Anzahl gespeicherter Fehlversuche |
| `GetBlockedIps` | — | `IEnumerable<BlockedLoginIp>` | Gesperrte IPs für Admin-UI |
| `Unblock` | `string ip` | `bool` | Hebt Sperre auf (DB + Cache) |

DI: `AddSingleton<ILoginIpBlockService, LoginIpBlockService>()` (`ServiceCollectionExtensions.cs` Zeile 76). Muster: Singleton mit `ConcurrentDictionary` + Persistenz über `IServiceScopeFactory`.

## `IAuthService`

Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs` (Zeilen 14–33, Namespace `VideoWebPlayer.Services.Authentication`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CurrentUser` (Property) | — | `ApplicationUser?` | Aktuell authentifizierter Benutzer |
| `ImpersonateAsync` | `ImpersonateRequest request` | `Task<AuthorizationToken>` | Impersonation-Token (Admin) |
| `LoginAsync` | `AuthenticationRequest request` | `Task<AuthorizationToken>` | Login → JWT |

Implementierung `AuthService` (transient, `ServiceCollectionExtensions.cs` Zeile 267).

## Konventions-Referenzen (Service-Interfaces im Projekt)

Dateien unter `VideoWebPlayer/Services/`:

| Interface | Datei | Zweck (kurz) |
|-----------|-------|--------------|
| `IFavoritesService` | `IFavoritesService.cs` | Favoritenverwaltung pro Benutzer (scoped) |
| `IUnlockedMediaService` | `IUnlockedMediaService.cs` | Einzelfreischaltungen (scoped) |
| `IGenreService` | `IGenreService.cs` | Genre-Verwaltung (scoped) |
| `IMediaSourceReader` | `IMediaSourceReader.cs` | Quellenzugriff (Dispatcher, scoped) |
| `IMediaMetadataWriteCoordinator` | `IMediaMetadataWriteCoordinator.cs` | Schreibkoordination Metadaten (Singleton) |

Stil: `I`-Präfix, teils eigene Datei (`IFavoritesService`), teils Interface + Implementierung in einer Datei (`LoginIpBlockService.cs`). Async-Methoden mit `CancellationToken cancellationToken = default` sind üblich (vgl. `IFavoritesService`).

## Framework-Contracts im Kontext

- `IEntityTypeConfiguration<T>` (EF Core): Konvention für Entitätskonfigurationen unter `VideoWebPlayer/Data/Configurations/`, eingebunden via `ApplyConfigurationsFromAssembly` in `ApplicationDbContext.OnModelCreating` (Zeile 650).
