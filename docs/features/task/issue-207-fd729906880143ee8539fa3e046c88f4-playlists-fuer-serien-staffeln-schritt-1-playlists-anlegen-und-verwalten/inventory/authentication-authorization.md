# Authentifizierung und Authorization

## Authentifizierungs-Flow

**Unterstützte Mechanismen:**
1. **Cookie-basiert** (für Blazor/Browser-Sessions)
2. **Bearer-Token/JWT** (für API-Clients, MAUI-App, etc.)

### JWT-Konfiguration

**Datei:** `appsettings.json`

```json
{
  "Jwt": {
    "Key": "Base64-EncodedSymmetricKey",
    "ApiToken": {
      "Web": "Web-Client-API-Token",
      "Maui": "MAUI-Client-API-Token"
    },
    "Issuer": "VideoWebPlayer"
  }
}
```

**Registrierung in ServiceCollectionExtensions.cs:**
```csharp
var jwtKey = configuration["Jwt:Key"];
var apiKey = configuration["Jwt:ApiToken:Web"] ?? configuration["Jwt:ApiToken"];
var mauiApiKey = configuration["Jwt:ApiToken:Maui"];
var issuer = configuration["Jwt:Issuer"] ?? "VideoWebPlayer";

services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

authenticationBuilder.AddIdentityCookies();
authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = issuer,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtKey)),
        NameClaimType = ClaimTypes.NameIdentifier
    };
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // SignalR WebSocket Token-Handling
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/mediaupdate"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
```

## IAuthService

**Datei:** `VideoWebPlayer/Services/Authentication/IAuthService.cs`

Interface für Authentifizierungs-Operationen.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `CurrentUser` | `ApplicationUser?` | Aktuell angemeldeter Benutzer (nullable) |

### Methoden (Annahme)

- `LoginAsync(request)` — Login durchführen
- `ImpersonateAsync(request)` — Benutzer impersonieren
- `LogoutAsync()` — Logout durchführen

## Authentifizierung im Controller

### BearerTokenCheck-Attribut

**Verwendung:**
```csharp
[ApiController]
[Route("api/favorites")]
[BearerTokenCheck]
public class FavoritesController : ApiBaseController
```

**Effekt:** Erzwingt Bearer-Token-Authentifizierung auf allen öffentlichen Methoden des Controllers.

**Alternativer Ort:** `VideoWebPlayer/Controllers/Attributes/`

**Hinweis:** Das Attribut wird vermutlich als Custom-Attribute implementiert, das mit `[Authorize]` oder Custom-Middleware arbeitet.

### CurrentUser Eigenschaft

**Definition in ApiBaseController:**
```csharp
protected ApplicationUser? CurrentUser => _authService.CurrentUser;
```

**Verwendung im Controller:**
```csharp
CheckLogedIn();  // Throws wenn CurrentUser == null
var result = await _favoritesService.GetFavoritesAsync(CurrentUser!.Id, HttpContext.RequestAborted);
```

**Pattern:**
- `CheckLogedIn()` wirft `UnauthorizedAccessException`, wenn `CurrentUser` ist null
- `CurrentUser!` nutzt Non-Null-Assertion nach erfolgreichem Check
- `CurrentUser.Id` ist die `UserId` für Datenbankzugriffe

## Ownership-Checks

**Implementierung im Service:**

Alle Datenbank-Zugriffe filtern nach `UserId`:

```csharp
public async Task<DtoFavoriteEntry[]> GetFavoritesAsync(string userId, CancellationToken cancellationToken = default)
{
    var favorites = await _db.FavoriteEntries
        .AsNoTracking()
        .Where(f => f.UserId == userId)  // ← Ownership-Check
        .ToListAsync(cancellationToken);
    // ...
}
```

**RemoveFavoriteAsync:**
```csharp
var fav = await _db.FavoriteEntries.FirstOrDefaultAsync(
    f => f.UserId == userId &&  // ← Ownership-Check
         (conditions...),
    cancellationToken);

if (fav != null)
{
    _db.FavoriteEntries.Remove(fav);
    await _db.SaveChangesAsync(cancellationToken);
}
```

**Exception für Violations:**
```csharp
// Implicit: Wenn eine Entity mit falscher UserId abgefragt wird, gibt Service null zurück
// Controller sollte 404 zurückgeben, nicht 403
// ODER Service throws UnauthorizedAccessException für explizite Violations
```

**HTTP-Status-Codes für Violations:**
- `404 Not Found` — Ressource nicht gefunden (auch wenn existiert, aber falsche Ownership)
- `403 Forbidden` — Zugriff verweigert (nur wenn der Benutzer bewusst davon erfährt, dass die Ressource existiert)

Im FavoritesController wird dies via `UnauthorizedAccessException` → `401 Unauthorized` gemappt (etwas missleading, aber etabliert).

## Authorization Policy

**In ServiceCollectionExtensions.cs:**
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("IsAdmin", "True"));
});
```

**Verwendung:**
```csharp
[Authorize(Policy = "AdminOnly")]
public class AdminController : ApiBaseController
{
    // Nur Admins haben Zugriff
}
```

**Für Playlists:** Wahrscheinlich keine Admin-Policy nötig, nur Ownership-Checks.

## ApplicationUser Klasse

**Datei:** `VideoWebPlayer/Data/ApplicationUser.cs`

```csharp
public class ApplicationUser : IdentityUser
{
}
```

**Basis:** Erbt von ASP.NET Core Identity `IdentityUser`

**Properties von IdentityUser (Basis):**
- `Id` (string) — Eindeutige Benutzer-ID
- `UserName` (string) — Anmelde-Name
- `Email` (string?) — E-Mail-Adresse
- `PasswordHash` (string?) — Hash des Passworts
- `EmailConfirmed` (bool) — E-Mail bestätigt?
- Weitere Standard-Identity-Properties

**Erweiterung:** Derzeit keine zusätzlichen Properties, aber kann erweitert werden mit Playlist-bezogenen Daten (z.B. `MaxPlaylistsPerUser`).

## SignalR Authentication

**Pattern für WebSocket-Token:**
```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/mediaupdate"))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }
};
```

**Verwendung für Playlists:**
Falls eine SignalR-Integration für Playlist-Updates gewünscht ist (z.B. Echtzeit-Sync mehrerer Clients), kann der selbe Pattern verwendet werden:
```csharp
if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/playlists"))
{
    context.Token = accessToken;
}
```

## Zusammenfassung für Playlist-Implementierung

1. **Controller-Schutz:** `[BearerTokenCheck]` auf `PlaylistsController`
2. **User-Ermittlung:** `CurrentUser` via `ApiBaseController`
3. **Ownership-Checks:** Service filtert alle Abfragen nach `userId`
4. **Error-Handling:** Service throws `UnauthorizedAccessException` bei Violations
5. **HTTP-Mapping:** Controller fängt `UnauthorizedAccessException` und gibt `401 Unauthorized` zurück
6. **CancellationToken:** `HttpContext.RequestAborted` wird an async Service-Methoden übergeben

Diese Patterns sollten konsistent mit der bestehenden FavoritesController-Implementierung sein.
