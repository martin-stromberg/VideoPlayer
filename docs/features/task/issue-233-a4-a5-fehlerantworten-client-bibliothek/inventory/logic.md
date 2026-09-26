# Bestandsaufnahme: Controller und Client-Logik

## A4 — ItemsController (Fehlerbehandlung)

### `ItemsController`
Datei: `VideoWebPlayer/Controllers/ItemsController.cs` (Lines 13-767)

Basisklasse: `ApiBaseController` (trägt `[BearerTokenCheck]` Attribut)

#### Kritische Methoden für A4

| Methode | Sichtbarkeit | Beschreibung | Status |
|---------|-------------|-------------|--------|
| `Get(string type, long id)` | public | Endpunkt `[HttpGet("{type}/{id}")]` für Media Details | ✓ Vorhanden |
| `FindEntry(string type, long id)` | private | Findet Datensatz nach Typ/ID oder wirft `RecordNotFoundException` | ✓ Vorhanden |
| `EnsureAccessAsync(MediaBaseEntry entry)` | private | Prüft Zugriff oder wirft `UnauthorizedAccessException` | ✓ Vorhanden |
| `FindMediaItemAsync(string type, long id)` | private | Kombiniert `FindEntry` + `EnsureAccessAsync`, wirft `RecordNotFoundException` für fehlende MediaItem | ✓ Vorhanden |
| `StreamMediaItem(string type, long id)` | public | Endpunkt `[HttpGet("{type}/{id}/stream")]` | ✓ Vorhanden |
| `Download(string type, long id)` | public | Endpunkt `[HttpGet("{type}/{id}/download")]` | ✓ Vorhanden |

#### Fehlerbehandlung (heute)

**Zeile 524-627 (Get-Endpunkt):**
```csharp
catch (UnauthorizedAccessException ex)
{
    return Unauthorized(ex.Message);  // ❌ 401 statt 403
}
catch (Exception ex)
{
    return StatusCode(500, "Internal server error");  // ❌ RecordNotFoundException nicht explizit
}
```

**Zeile 624-627 (StreamMediaItem):**
```csharp
catch (UnauthorizedAccessException ex)
{
    return Unauthorized(ex.Message);  // ❌ 401 statt 403
}
catch (Exception ex)
{
    return StatusCode(500, "Internal server error");  // ❌ RecordNotFoundException nicht explizit
}
```

**Zeile 661-664 (Download):**
```csharp
catch (UnauthorizedAccessException ex)
{
    return Unauthorized(ex.Message);  // ❌ 401 statt 403
}
catch (Exception ex)
{
    return StatusCode(500, "Internal server error");  // ❌ RecordNotFoundException nicht explizit
}
```

#### Vorbild: PlaylistsController.ExecuteAsync

Datei: `VideoWebPlayer/Controllers/PlaylistsController.cs` (Zeilen 52-116)

Zentrale Exception-Mapping zeigt korrektes Muster:

```csharp
catch (KeyNotFoundException ex)
{
    return NotFound(ex.Message);  // ✓ 404
}
catch (PlaylistAccessDeniedException ex)  // oder eigene Exception
{
    return Forbid(JwtBearerDefaults.AuthenticationScheme);  // ✓ 403
}
catch (Exception ex)
{
    return StatusCode(500, "Internal server error");
}
```

**A4 soll dieses Muster auf ItemsController anwenden.**

---

## A5 — VideoWebPlayerClient (Pairing, Bootstrap, Refresh)

### `VideoWebPlayerClient`
Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

Basisklasse für HTTP-API-Client. Ist auch partial (`VideoWebPlayerClient.Playlists.cs`).

#### Eigenschaften und Methoden (heute)

| Methode | Sichtbarkeit | Beschreibung | Status |
|---------|-------------|-------------|--------|
| `SetAuthorizationToken(AuthorizationToken token)` | public | Setzt Bearer Token auf HttpClient | ✓ Vorhanden |
| `AuthorizationToken` (Property) | public | Bearer Token auslesen | ✓ Vorhanden |
| `EnsureAuthorizationTokenAsync(ClaimsPrincipal?, CancellationToken)` | public virtual | Hook für Token-Setup (virtuelle Überschreibung) | ✓ Vorhanden |
| `HandleUnauthorized()` | protected virtual | Hook bei 401 (gibt heute immer `false` zurück) | ✓ Vorhanden |
| `AuthenticateAsync(string email, string password)` | public | Login mit E-Mail/Passwort | ✓ Vorhanden |
| `HttpGetAsync<T>(string)` | protected virtual | GET mit Reauth-Retry | ✓ Vorhanden |
| `HttpGetAsync<T>(string, CancellationToken)` | protected virtual | GET mit CancellationToken | ✓ Vorhanden |
| `HttpPostAsync<T>(string, HttpContent, bool)` | protected virtual | POST mit Reauth-Retry | ✓ Vorhanden |
| `HttpPostAsync(string, HttpContent, bool)` | protected virtual | POST ohne Response-Deserialisierung | ✓ Vorhanden |
| `HttpPutAsync<T>(string, HttpContent)` | protected virtual | PUT mit Response | ✓ Vorhanden |
| `HttpPutAsync(string, HttpContent)` | protected virtual | PUT ohne Response | ✓ Vorhanden |
| `HttpPatchAsync<T>(string, HttpContent)` | protected virtual | PATCH | ✓ Vorhanden |
| `HttpDeleteAsync<T>(string)` | protected virtual | DELETE mit Response | ✓ Vorhanden |
| `HttpDeleteAsync(string)` | protected virtual | DELETE ohne Response | ✓ Vorhanden |
| `SendWithReauthorizationAsync(...)` | private | Retry-Logik bei 401 | ✓ Vorhanden |

#### Fehlende Methoden und Eigenschaften für A5

| Item | Typ | Beschreibung | Status |
|------|-----|-------------|--------|
| `DeviceToken` | Property | get/set für Geräte-Token pro Client-Instanz | ❌ Fehlt |
| `PairingExchangeAsync(string code, string username, string password)` | public async | POST `/api/pairing/exchange` | ❌ Fehlt |
| `PairingBootstrapAsync(PairingBootstrapPayload payload)` | public async | POST `/api/pairing/bootstrap` mit verschlüsselter Payload | ❌ Fehlt |
| `RefreshAsync()` | public async | POST `/api/auth/refresh` mit aktuellem Refresh-Token | ❌ Fehlt |
| `LogoutAsync()` | public async | POST `/api/auth/logout` | ❌ Fehlt |
| `X-API-Key` Header-Setzung | Implementation | Bei allen Anfragen wenn `DeviceToken != null` | ❌ Fehlt |
| `HandleUnauthorized()` Override | Implementation | Bei 401: `RefreshAsync()` rufen, Anfrage wiederholen | ❌ Fehlt (nur `false` heute) |

#### Playlist-spezifische Methoden

Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.Playlists.cs` (Partial)

| Methode | Beschreibung | Betroffene HTTP-Method | Status |
|---------|-------------|----------------------|--------|
| `PostForOptionalPlaylistNavigationResultAsync` | Endpunkte: `play`, `play/next`, `play/previous`, `play/advance` | POST | ✓ Existiert, aber... |

**Problem:** `PostForOptionalPlaylistNavigationResultAsync` ruft `SendAndDeserializeAsync` direkt auf (umgeht `SendWithReauthorizationAsync`). In `InternalVideoWebPlayerClient` wird diese Methode nicht überschrieben → bei fehlendem Token schlägt Aufruf fehl, ohne dass Impersonierung greift.

---

## A5 — InternalVideoWebPlayerClient (Impersonierung)

### `InternalVideoWebPlayerClient`
Datei: `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs`

Basisklasse: `VideoWebPlayerClient`

**Zweck:** Circuit-scoped Client (Blazor Server) mit automatischer Token-Impersonierung für den aktuellen HTTP-Benutzer.

#### Übergeschriebene Methoden (3 von 11 HTTP-Methoden)

| Methode | Zeile | Beschreibung | Status |
|---------|------|-------------|--------|
| `HttpGetAsync<T>(string)` | 55 | Impersoniert vor GET | ✓ Überschrieben |
| `HttpPostAsync<T>(string, HttpContent, bool)` | 69 | Impersoniert vor POST (generic) | ✓ Überschrieben |
| `HttpPostAsync(string, HttpContent, bool)` | 82 | Impersoniert vor POST (non-generic) | ✓ Überschrieben |

#### Nicht übergeschriebene Methoden (8 von 11 HTTP-Methoden)

| Methode | In Basis | Betroffene Einsätze | Status |
|---------|----------|--------|---------|
| `HttpGetAsync<T>(string, CancellationToken)` | Zeile 140 | `RequestPlaylistEntriesPagedAsync` (Zeile 62-65 in Playlists.cs) | ❌ Nicht überschrieben |
| `HttpPutAsync<T>` | Zeile 167 | Playlist-Umbenennung, Genres, Ordnung, Öffentlich-Status | ❌ Nicht überschrieben |
| `HttpPutAsync` | Zeile 178 | Ähnlich wie `HttpPutAsync<T>` | ❌ Nicht überschrieben |
| `HttpPatchAsync<T>` | Zeile 202 | Sortiermodus-Änderung | ❌ Nicht überschrieben |
| `HttpDeleteAsync<T>` | Zeile 236 | Playlist-Löschung, Cover-Löschung | ❌ Nicht überschrieben |
| `HttpDeleteAsync` | Zeile 245 | Ähnlich wie `HttpDeleteAsync<T>` | ❌ Nicht überschrieben |
| `PostForOptionalPlaylistNavigationResultAsync` | Playlists.cs | `play`, `play/next`, `play/previous`, `play/advance` | ❌ Nicht überschrieben |

**Risiko:** Diese Methoden laufen ohne Impersonierung, wenn das Token vorher nicht gesetzt wurde (z. B. wenn eine Playlist-Operation vor `NavMenu` ausgelöst wird). Sie erhalten 401 und schlagen fehl, ohne Retry.

---

## ApiBaseController (Basisklasse)

Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

| Methode | Beschreibung |
|---------|-------------|
| `CheckLogedIn()` | Wirft Exception wenn nicht angemeldet |
| `CurrentUser` | Aktueller Benutzer aus Claims |

Wird von `ItemsController` und `PlaylistsController` erbt.
