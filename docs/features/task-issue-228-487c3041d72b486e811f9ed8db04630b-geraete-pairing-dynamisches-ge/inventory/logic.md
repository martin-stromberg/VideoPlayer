# Logik / Services / Controller — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

Hinweis zur Codestruktur: Die Klassen `ApiTokenCheckAttribute`, `BearerTokenCheckAttribute`, `ConnectionCheckAttribute`, `WhitelistIpMiddleware`, `WhitelistIpMiddlewareExtensions` sowie `AuthController`, `HealthController` und die im `AuthController` definierte `ImpersonateRequest` liegen **im globalen Namespace** (keine `namespace`-Deklaration in diesen Dateien). Services liegen unter `VideoWebPlayer.Services` bzw. `VideoWebPlayer.Services.*`.

## `ApiTokenCheckAttribute` (zu erweiterndes Gate)

Datei: `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApiTokenCheckAttribute(ApiTokenScope scope = ApiTokenScope.AnyClient)` | public | Konstruktor, speichert Scope in `_scope` (Zeile 17–20) |
| `OnActionExecuting(ActionExecutingContext)` | public override | Validiert Header `X-API-Key` gegen konfigurierte Tokens (Zeile 26–85) |

Details:
- Header-Name `X-API-Key` als `private const string HeaderName` (Zeile 11).
- Löst aus `RequestServices` nur `ILogger<ApiTokenCheckAttribute>` und `IConfiguration` auf (Zeilen 28–29) — aktuell **kein** Datenbank-/Service-Zugriff.
- Entfernt optionalen `Bearer `-Präfix aus dem Headerwert (Zeile 46–47).
- Baut `validTokens` als `HashSet<string>` (Ordinal); lokale Funktion `AddTokensFromConfig` splittet Konfigurationswerte an `,`/`;` — Mehrfach-Tokens pro Key möglich (Zeilen 50–64).
- `MauiOnly`: nur `Jwt:ApiToken:Maui` (Zeile 66–69). `AnyClient`: `Jwt:ApiToken`, `Jwt:ApiToken:Web`, `Jwt:ApiToken:Maui` (Zeilen 71–75).
- Bei fehlendem Header oder ungültigem Token `UnauthorizedResult`; Log-Meldung enthält bewusst nicht den Token-Wert (abgesichert durch Test `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue`).

Verwendet von: `AuthController.Login` (`[ApiTokenCheck(ApiTokenScope.MauiOnly)]`, Zeile 42) und `AuthController.Impersonate` (`[ApiTokenCheck()]`, Zeile 62). Kein anderer Controller nutzt das Attribut.

## `BearerTokenCheckAttribute`

Datei: `VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnActionExecuting(ActionExecutingContext)` | public override | Validiert `Authorization: Bearer`-JWT (auch `access_token`-Query) gegen `SymmetricSecurityKey` aus DI und `Jwt:Issuer` (Zeile 24–65) |

Verwendet auf Klassenebene von nahezu allen API-Controllern (`ItemsController`, `SourcesController`, `SourceGenresController`, `SourceIconsController`, `PicturesController`, `FavoritesController`, `EpisodesController`, `ContinueWatchingController`, `UnlockedMediaController`, `AdminSourcesController`, `ActorsController`) sowie auf `AuthController.Impersonate`.

## `ConnectionCheckAttribute`

Datei: `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnActionExecuting(ActionExecutingContext)` | public override | Erlaubt nur Requests mit internem User-Agent oder zuvor freigeschalteter IP (`InternalConnectionService`); sonst `UnauthorizedResult` (Zeile 19–35) |

Wird derzeit von **keinem** Controller verwendet (Suche nach `[ConnectionCheck` ohne Treffer außerhalb der Definition).

## `AuthController`

Datei: `VideoWebPlayer/Controllers/AuthController.cs`, `[ApiController]`, Route `api/[controller]` → `api/auth`, erbt `ApiBaseController`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Login(AuthenticationRequest)` | public | `POST api/auth/login`, `[ApiTokenCheck(ApiTokenScope.MauiOnly)]` (Zeile 41–53) — einziger MauiOnly-Endpunkt |
| `Impersonate(ImpersonateRequest)` | public new | `POST api/auth/impersonate`, `[ApiTokenCheck()]` + `[BearerTokenCheck()]` (Zeile 61–74) |

Konstruktor: `IAuthService`, `ApplicationDbContext` (in `_db`, derzeit ungenutzt im gezeigten Code), `ILogger<AuthController>`.

## `ApiBaseController`

Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CurrentUser` | protected Property | Aktueller Benutzer via `IAuthService` |
| `CheckLoggedIn()` / `CheckLogedIn()` | protected | Wirft `UnauthorizedAccessException` ohne Benutzer |
| `GetPlaceholderBytesAsync(IMemoryCache)` | protected static | Platzhalterbild mit MemoryCache |
| `Create<T>(object)` | protected static | Reflection-basierter DTO-Mapper |
| `LoginAsync(AuthenticationRequest)` | internal | Delegiert an `IAuthService.LoginAsync` |
| `Impersonate(ImpersonateRequest)` | internal | Delegiert an `IAuthService.ImpersonateAsync` |

## `HealthController` (Vorbild öffentlicher Endpunkt)

Datei: `VideoWebPlayer/Controllers/HealthController.cs` — `[ApiController]`, Route `api/health`, `GET` liefert `200 OK` mit `"OK"`. Trägt **keine** Gate-Attribute; zeigt das Muster für den geplanten öffentlichen `PairingController`.

## `ILoginIpBlockService` / `LoginIpBlockService` (Rate-Limit-/Sperr-Muster)

Datei: `VideoWebPlayer/Services/LoginIpBlockService.cs` (Interface und Implementierung in derselben Datei; Implementierung `internal sealed`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsBlocked(IPAddress?)` | public | Prüft In-Memory-Cache, fällt auf `BlockedLoginIps`-Tabelle zurück (Zeile 87–109) |
| `GetFailureCount(IPAddress?)` | public | Anzahl Fehlversuche aus Cache (Zeile 111–115) |
| `RegisterFailure(IPAddress?)` | public | Zählt Fehlversuch in `ConcurrentDictionary`; ab `Threshold = 5` Sperrung + Persistenz via `PersistBlock` (Zeile 117–141) |
| `RegisterSuccess(IPAddress?)` | public | Entfernt Eintrag, sofern nicht gesperrt (Zeile 143–150) |
| `GetBlockedIps()` | public | Liste gesperrter IPs aus DB, absteigend nach `BlockedAtUtc` (Zeile 166–174) |
| `Unblock(string)` | public | Entfernt Sperre aus DB und Cache (Zeile 176–187) |
| `Normalize(IPAddress)` | private static | IPv4-Mapping-Normalisierung (Zeile 189–193) |
| `LoadBlockedIps()` / `PersistBlock(string, int)` | private | Startladen (`Interlocked`-Guard) bzw. Persistieren über `IServiceScopeFactory` + `ApplicationDbContext` |

Registrierung: `services.AddSingleton<ILoginIpBlockService, LoginIpBlockService>()` (`ServiceCollectionExtensions.cs` Zeile 76). Verwendet von `Security.razor`, `NavMenu.razor` und `IdentityComponentsEndpointRouteBuilderExtensions` (`/Account/LoginProcess`, Zeilen 45–91: `IsBlocked` vor Login, `RegisterFailure` bei Fehlschlag; `RegisterSuccess` wird dort **nicht** aufgerufen).

Keine dedizierten Unit-Tests für `LoginIpBlockService` vorhanden.

## `InternalConnectionService`

Datei: `VideoWebPlayer/Services/InternalConnectionService.cs` — Singleton (`ServiceCollectionExtensions.cs` Zeile 75).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetUserAgent()` | public | Liefert instanzspezifischen User-Agent `VideoWebPlayer/1.0 Instance/{ConnectionId}` |
| `IsAllowed(IPAddress?)` | public | IP erlaubt, wenn letzter `Allow`-Eintrag < 5 Minuten alt |
| `Allow(IPAddress?)` | public | Merkt IP mit Zeitstempel in `ConcurrentDictionary` |

## `WhitelistIpMiddleware` / `UseWhitelistIp`

Dateien: `VideoWebPlayer/Services/WhitelistIpMiddleware.cs`, `VideoWebPlayer/Services/WhitelistIpMiddlewareExtensions.cs` — Middleware ruft bei authentifizierten Requests `InternalConnectionService.Allow(RemoteIpAddress)` auf (Zeile 27–32); registriert früh in der Pipeline (`UseVideoWebPlayer`, Zeile 68, vor `UseAuthentication`).

## `UdpDiscoveryListener`

Datei: `VideoWebPlayer/Services/UdpDiscoveryListener.cs` — UDP-Broadcast-Discovery (Port 5001): antwortet auf `VIDEOWEBPLAYER_DISCOVERY` mit `VIDEOWEBPLAYER_SERVER:{serverAddress}`. Wird in `Program.cs` Zeilen 49–55 gestartet (nicht in Umgebung `Testing`). Relevant als bestehender Server-Discovery-Kanal für die App.

## `IAuthService` / `AuthService` / `AuthorizationTokenService`

Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs` (alle drei Typen in einer Datei).

| Typ | Rolle |
|-----|-------|
| `IAuthService` | `CurrentUser`-Property, `ImpersonateAsync(ImpersonateRequest)`, `LoginAsync(AuthenticationRequest)` → `AuthorizationToken` |
| `AuthorizationTokenService` | `CreateToken(ApplicationUser)` — JWT mit `Jwt:Issuer`, Claims inkl. `"IsAdmin"`, HmacSha256, 12 h Gültigkeit; benötigt `SymmetricSecurityKey` + `IConfiguration` |
| `AuthService` | `LoginAsync` via `UserManager.FindByEmailAsync` + `SignInManager.CheckPasswordSignInAsync`; `ImpersonateAsync` mit Admin-Prüfung (`CheckAdmin`) |

Registrierung: `AddScoped<AuthorizationTokenService>()` (Zeile 70), `AddTransient<IAuthService, AuthService>()` (Zeile 267).

## `VideoWebPlayerClient` / `InternalVideoWebPlayerClient`

- `VideoWebPlayer.Client/VideoWebPlayerClient.cs`: HTTP-Client für API-Aufrufe. `AuthenticateAsync(email, password)` POSTet `api/auth/login` und setzt das Bearer-Token (`SetAuthorizationToken`, Zeile 165–182). Die Klasse setzt selbst **keinen** `X-API-Key`-Header — dieser kommt beim serverseitigen Einsatz aus dem `Internal`-HttpClient (`ServiceCollectionExtensions.cs` Zeilen 166–172, Header `X-API-Key` = `Jwt:ApiToken:Web` ?? `Jwt:ApiToken`).
- `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs`: erweitert `VideoWebPlayerClient`; impersoniert den aktuellen HTTP-Benutzer automatisch (`ImpersonateAsync`) und ergänzt Bearer-Token für GET/POST.

## `ServiceCollectionExtensions.AddVideoWebPlayerServices` (Erweiterungspunkt DI)

Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (Zeilen 40–290).

- Liest `Jwt:Key`, `Jwt:ApiToken:Web` ?? `Jwt:ApiToken`, `Jwt:ApiToken:Maui`, `Jwt:Issuer` (Zeilen 47–51).
- Produktions-Pflichtcheck (Zeilen 54–59): wirft `InvalidOperationException` bei fehlendem `Jwt:Key`, `Jwt:ApiToken`/`Jwt:ApiToken:Web` **und `Jwt:ApiToken:Maui`** — relevant, falls der Maui-Token entfallen soll (siehe Anforderung „Konfiguration").
- Registriert u. a. `ILoginIpBlockService` als Singleton (Zeile 76), `AddDbContext<ApplicationDbContext>` mit SQLite (Zeile 157), `AddControllers()` (Zeile 270), Hosted Services `MediaSourceScanService`, `ContinueWatchingWorker`, `ActorBackfillWorker` (Zeilen 268, 274, 275), `TimeProvider.System` als Singleton (Zeile 237).
- Registriert `SymmetricSecurityKey` als Singleton, sofern `Jwt:Key` gesetzt (Zeilen 284–287) — wird von `BearerTokenCheckAttribute` aus DI bezogen.
- Kein `Microsoft.AspNetCore.RateLimiting`/`AddRateLimiter` im Projekt (verifiziert per Suche; nur Framework-Referenzen in `project.assets.json`).

## `WebApplicationExtensions` (Pipeline)

Datei: `VideoWebPlayer/Extensions/WebApplicationExtensions.cs`

- `MigrateDatabase()` (Zeilen 23–32): wendet EF-Migrationen beim Start an (`db.Database.Migrate()`).
- `UseVideoWebPlayer()` (Zeilen 38–93): Host-Header-Check, ExceptionHandler/HSTS, `UseHttpsRedirection`, `UseWhitelistIp`, `UseStaticFiles`, `UseAuthentication`/`UseAuthorization`, `UseBackups`, `RestoreInProgressMiddleware`, `FirstUserRedirectMiddleware`, `UseAntiforgery`, `MapRazorComponents<App>()`, `MapControllers()` (Zeile 83), SignalR-Hub `/hubs/mediaupdate` (JWT), `MapAdditionalIdentityEndpoints()`.

## `Program.cs` (Server-Startup)

Datei: `VideoWebPlayer/Program.cs` — `AddVideoWebPlayerServices()`, `AddVideoWebPlayerAutoUpdate()`, Kestrel-Limit `MaxRequestBodySize`, `MigrateDatabase()`, `UseVideoWebPlayer()`, `UdpDiscoveryListener` auf Port 5001 (außer in Umgebung `Testing`).

## Weitere referenzierte Bausteine

- `ProgramSettingsService` (`VideoWebPlayer/Services/ProgramSettingsService.cs`, `sealed`, scoped): `GetOrCreateSetupAsync` pflegt die `Setup`-Singleton-Zeile inkl. Default-Korrekturen — Vorbild für DB-basierte Einstellungen.
- `MediaSourceScanService`, `ContinueWatchingWorker`, `ActorBackfillWorker`: `BackgroundService`-Muster mit `IServiceScopeFactory`/`IServiceProvider` für DB-Zugriffe (HostedService-Vorbild für einen optionalen Pairing-Code-Aufräumlauf).
- `EventManager` (`VideoWebPlayer/Services/EventManager.cs`, Singleton): einfaches Publish/Subscribe (`Subscribe<TEvent>`, `SubscribeDisposable<TEvent>`, `Unsubscribe<TEvent>`, `Publish`); vorhandene Events unter `VideoWebPlayer/Events/` (`MediaSourceCreatedEvent`, `MediaSourceUpdatedEvent`, `MediaSourceDeletedEvent`, `BackgroundProcessingStatusEvent`).
- `IdentityComponentsEndpointRouteBuilderExtensions` (`VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`): mappt `/Account/*`-Endpunkte; `/Account/LoginProcess` (Zeilen 45–91) zeigt die Verwendung von `ILoginIpBlockService` als Bruteforce-Schutz-Muster.
