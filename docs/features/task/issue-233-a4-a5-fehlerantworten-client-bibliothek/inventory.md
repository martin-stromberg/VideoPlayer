# Bestandsaufnahme: A4 und A5 – Fehlerantworten und Client-Bibliothek

Diese Bestandsaufnahme erfasst den bestehenden Produktivcode und die Testlandschaft bezogen auf die Anforderungen A4 (Fehlerabbildung in der Medien-API) und A5 (Client-Bibliothek um Gerätekopplung, QR-Bootstrap und Sitzungserneuerung).

## Zusammenfassung

### A4 — Fehlerantworten (Status quo)

**Vorhanden:**
- `ItemsController` mit `EnsureAccessAsync`, `FindEntry`, `FindMediaItemAsync` Methoden
- `RecordNotFoundException` und `UnauthorizedAccessException` werden bereits in Code geworfen
- Fehlerbehandlung mit try/catch in jedem Endpunkt
- `PlaylistsController` zeigt Vorbild für zentrale Exception-Mapping (ExecuteAsync)
- Tests existieren, die heute `UnauthorizedObjectResult` (401) und implizite `StatusCode(500)` erwarten

**Fehlt / Abweichungen:**
- `ItemsController` wirft `UnauthorizedAccessException` für fehlende Berechtigung → wird auf `Unauthorized(401)` abgebildet statt `Forbid(403)`
- `RecordNotFoundException` wird nicht explizit auf `NotFound(404)` abgebildet, sondern fällt in generischen Exception-Handler → `StatusCode(500)`
- Keine zentrale Exception-Mapping wie in `PlaylistsController.ExecuteAsync`
- Tests müssen nach A4-Umsetzung auf `ForbidResult(403)` und `NotFoundResult(404)` angepasst werden

### A5 — Client-Bibliothek (Status quo)

**Vorhanden:**
- Alle DTOs bereits vorhanden: `PairingExchangeRequest/Response`, `PairingBootstrapRequest/Response/Payload`, `RefreshTokenRequest/Response`
- `VideoWebPlayerClient` mit Basis-HTTP-Methoden (`HttpGetAsync`, `HttpPostAsync`, `HttpPutAsync`, `HttpPatchAsync`, `HttpDeleteAsync`)
- `HandleUnauthorized()` als virtueller Schutzpunkt (gibt heute immer `false` zurück)
- `SendWithReauthorizationAsync` für 401-Retry bereits vorhanden
- `SetAuthorizationToken` und `AuthorizationToken` Eigenschaften existieren
- `InternalVideoWebPlayerClient` mit selektiven Overrides für Impersonierung
- Test-Hilfsmethoden: `PairingWebApplicationFactory`, `PairingTestDb`, `PairingCryptoHelper`

**Fehlt / Abweichungen:**
- Keine `DeviceToken` Eigenschaft in `VideoWebPlayerClient`
- Keine Methoden: `PairingExchangeAsync`, `PairingBootstrapAsync`, `RefreshAsync`, `LogoutAsync`
- Keine Setzung des `X-API-Key`-Headers bei Anfragen
- `InternalVideoWebPlayerClient` überschreibt nur drei Methoden:
  - ✓ `HttpGetAsync<T>(string)`
  - ✓ `HttpPostAsync<T>(string, HttpContent, bool)`
  - ✓ `HttpPostAsync(string, HttpContent, bool)`
  - ✗ `HttpGetAsync<T>(string, CancellationToken)` fehlt
  - ✗ `HttpPutAsync<T>` fehlt
  - ✗ `HttpPutAsync` fehlt
  - ✗ `HttpPatchAsync<T>` fehlt
  - ✗ `HttpDeleteAsync<T>` fehlt
  - ✗ `HttpDeleteAsync` fehlt
  - ✗ `PostForOptionalPlaylistNavigationResultAsync` fehlt (wird in Playlist-Partial verwendet)

### Test-Ausgangszustand

**Testlauf vor Änderungen:**
- Befehl: `dotnet build VideoPlayer.sln -c Release` → **0 Fehler, 0 Warnungen** ✓
- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build` → **1512 erfolgreich, 1 Fehlschlag** (flackernd, bekannt)
- **Anzahl bekannter Testfehler:** 1 (Playwright-Timeout in `PlaylistDragDropFeedbackE2ETests`, nicht A4/A5-relevant)
- **Tests, die A4-Änderungen abfangen:** In `ItemsControllerAccessTests` erwarten mehrere Tests `UnauthorizedObjectResult(401)` statt künftig `ForbidResult(403)`

Vollständige Nachweise unter [Tests](inventory/tests.md).

## Details

- [Datenmodelle (DTOs)](inventory/models.md)
- [Controller und Client-Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests und Testausgangszustand](inventory/tests.md)
