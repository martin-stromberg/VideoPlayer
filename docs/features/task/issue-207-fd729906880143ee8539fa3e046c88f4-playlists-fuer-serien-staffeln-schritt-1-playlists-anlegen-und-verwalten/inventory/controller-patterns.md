# Controller und API-Patterns

## ApiBaseController (Basis-Klasse)

**Datei:** `VideoWebPlayer/Controllers/ApiBaseController.cs`

Die Basis-Klasse für alle API-Controller, bietet gemeinsame Funktionalität für Authentifizierung und Utilities.

### Abhängigkeiten

| Abhängigkeit | Typ | Zweck |
|--------------|-----|-------|
| `_authService` | `IAuthService` | Authentifizierung und Benutzerermittlung |
| `_logger` | `ILogger` | Logging |

### Geschützte Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Logger` | `ILogger` | Logger-Instanz für Subklassen |
| `CurrentUser` | `ApplicationUser?` | Aktuell authentifizierter Benutzer (nullable) |

### Geschützte Methoden

| Methode | Signatur | Zweck |
|---------|----------|-------|
| `CheckLoggedIn()` | `void` | Wirft `UnauthorizedAccessException`, wenn kein Benutzer angemeldet ist |
| `CheckLogedIn()` | `void` | Legacy-Alias für `CheckLoggedIn()` (mit Typo) |
| `GetPlaceholderBytesAsync()` | `static async Task<byte[]?>` | Lädt Placeholder-Image aus wwwroot mit Caching |
| `Create<T>()` | `protected T` | Reflection-basiertes DTO-Mapping (Entity → DTO) |
| `LoginAsync()` | `internal async Task<AuthorizationToken>` | Login-Durchführung |
| `Impersonate()` | `internal async Task<AuthorizationToken>` | User-Impersonation |

### DTO-Mapping: `Create<T>(object)`

```csharp
protected T Create<T>(object ms)
{
    var sourceType = ms.GetType();
    var record = Activator.CreateInstance<T>();
    foreach (var prop in typeof(T).GetProperties().Where(p => 
        !p.GetCustomAttributes(typeof(IgnoreAssignPropertyAttribute), false).Any()))
    {
        var sourceProp = sourceType.GetProperty(prop.Name);
        if (sourceProp != null && sourceProp.CanRead)
        {
            var value = sourceProp.GetValue(ms);
            prop.SetValue(record, value);
        }
    }
    return record;
}
```

**Funktionsweise:**
- Reflection-basiert, kopiert Properties mit gleichem Namen
- Respektiert `[IgnoreAssignProperty]` Attribut auf DTO-Properties
- Erstellt neue Instanz via `Activator.CreateInstance<T>()`

## FavoritesController (Referenz-Implementation)

**Datei:** `VideoWebPlayer/Controllers/FavoritesController.cs`

REST-API-Controller für Favoriten-Management. Ist als Referenz für `PlaylistsController` gedacht.

### Klassendefinition

```csharp
[ApiController]
[Route("api/favorites")]
[BearerTokenCheck]
public class FavoritesController : ApiBaseController
```

### Attribute

| Attribut | Zweck |
|----------|-------|
| `[ApiController]` | Aktiviert API-spezifische Verhalten (automatisches Binding, Modell-Validierung) |
| `[Route("api/favorites")]` | Definiert Basis-Route für alle Endpoints |
| `[BearerTokenCheck]` | Custom-Attribut, erzwingt Bearer-Token-Authentifizierung auf allen Methoden |

### Abhängigkeiten

| Parameter | Typ | Zweck |
|-----------|-----|-------|
| `_favoritesService` | `IFavoritesService` | Service für Favorites-CRUD |
| `authService` | `IAuthService` | Authentifizierung (via Base-Klasse) |
| `logger` | `ILogger<FavoritesController>` | Logging (via Base-Klasse) |

### Endpunkte

**`GET /api/favorites`**
- **Authentifizierung:** Erzwingt `CheckLogedIn()`
- **Rückgabe:** `IActionResult` mit `DtoFavoriteEntry[]` im Ok()-Response
- **Error-Handling:**
  - `UnauthorizedAccessException` → `401 Unauthorized`
  - Allgemeine Exceptions → `500 Internal Server Error`

**`POST /api/favorites/add`**
- **Request-Body:** `FavoriteEntry`
- **Authentifizierung:** `CheckLogedIn()`
- **Rückgabe:** `200 Ok`
- **Error-Handling:** Wie oben

**`POST /api/favorites/remove`**
- **Request-Body:** `FavoriteEntry`
- **Authentifizierung:** `CheckLogedIn()`
- **Rückgabe:** `200 Ok`
- **Error-Handling:** Wie oben

**`POST /api/favorites/toggle`**
- **Request-Body:** `DtoMediaEntry`
- **Authentifizierung:** `CheckLogedIn()`
- **Rückgabe:** `200 Ok` mit `bool` (true = jetzt favorisiert, false = nicht favorisiert)
- **Error-Handling:** Wie oben

## Error-Handling-Pattern

Das Controller-Pattern verwendet Try-Catch für Exception-Mapping:

```csharp
try
{
    CheckLogedIn();
    var result = await _favoritesService.GetFavoritesAsync(CurrentUser!.Id, HttpContext.RequestAborted);
    return Ok(result);
}
catch (UnauthorizedAccessException ex)
{
    Logger.LogWarning(ex, "Beschreibung");
    return Unauthorized(ex.Message);
}
catch (Exception ex)
{
    Logger.LogError(ex, "Beschreibung");
    return StatusCode(500, "Internal server error");
}
```

**Pattern:**
- Alle Operationen in Try-Block
- `UnauthorizedAccessException` → `401 Unauthorized`
- Andere Exceptions → `500 Internal Server Error`
- Jeweils explizites Logging (Warning für Auth, Error für Server-Fehler)

## Ownership-Checks

**Pattern im Service:**
```csharp
await _favoritesService.GetFavoritesAsync(CurrentUser!.Id, HttpContext.RequestAborted);
```

**Implementierung:**
- Controller extrahiert `CurrentUser!.Id` (Non-null-Assertion)
- Übergibt `userId` als Parameter an Service
- Service filtert alle Abfragen nach `Where(f => f.UserId == userId)`
- Service throws `UnauthorizedAccessException`, wenn Ownership-Violation erkannt

**Konvention:**
- Ownership-Checks erfolgen im Service, nicht im Controller
- Exceptions werden vom Controller in HTTP-Status-Codes übersetzt

## BearerTokenCheck Attribut

**Funktion:** Custom-Authentifizierungs-Attribut

Wird auf der Klasse angewendet:
```csharp
[BearerTokenCheck]
public class FavoritesController : ApiBaseController
```

**Effekt:** Erzwingt Bearer-Token-Authentifizierung auf allen Methoden des Controllers (wird via Middleware oder Attribute-Verarbeitung durchgesetzt).

**Alternativer Ort:** In `VideoWebPlayer/Controllers/Attributes/`

## Konstruktor-Pattern

```csharp
public FavoritesController(IFavoritesService favoritesService, IAuthService authService, ILogger<FavoritesController> logger)
    : base(authService, logger)
{
    _favoritesService = favoritesService;
}
```

**Pattern:**
- Alle Dependencies über Konstruktor-Parameter
- Auth- und Logger werden an Base-Klasse weitergegeben
- Service wird in Private-Field gespeichert

## HTTP-Status-Codes

| Status | Situation | Beispiel |
|--------|-----------|---------|
| `200 Ok` | Erfolgreiche Datenbeschaffung oder Änderung | GET /favorites, POST /favorites/add |
| `400 Bad Request` | Ungültige Request-Parameter | (wenn Model-Validation fehlschlägt) |
| `401 Unauthorized` | Benutzer nicht angemeldet oder `UnauthorizedAccessException` | Fehlender/Ungültiger Token |
| `403 Forbidden` | Ownership-Violation (normaler Teil von Unauthorized-Handling) | (über `UnauthorizedAccessException` gemappt) |
| `404 Not Found` | Ressource nicht gefunden | (nicht explizit im Favorites-Controller, aber etablierte Konvention) |
| `500 Internal Server Error` | Unerwarteter Server-Fehler | Datenbankfehler, etc. |

## Zusammenhang zu HttpContext

Controller nutzt:
- `HttpContext.RequestAborted`: CancellationToken für async Operationen
- `CurrentUser`: Aus `_authService.CurrentUser`

Diese ermöglichen Request-spezifische Kontextverwaltung.
