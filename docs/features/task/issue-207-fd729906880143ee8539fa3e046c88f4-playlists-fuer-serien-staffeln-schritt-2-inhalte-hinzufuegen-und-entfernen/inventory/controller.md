# Bestandsaufnahme: API-Controller

## `PlaylistsController`
Datei: `VideoWebPlayer\Controllers\PlaylistsController.cs`

### Klasse-Metainformation
- **Attribut:** `[ApiController]` — Standard REST-Controller
- **Route:** `[Route("api/playlists")]` — Basis-Pfad
- **Auth:** `[BearerTokenCheck]` — Token-Validierung auf allen Endpoints
- **Basis:** Erbt von `ApiBaseController` (bietet `CheckLogedIn()`, `CurrentUser`, `Logger`)

### Endpoints für diese Anforderung

#### `POST /api/playlists/{id}/entries` — AddMediaToPlaylist
Datei: Zeile 209–244

**Methode-Signatur:**
```csharp
[HttpPost("{id}/entries")]
public async Task<IActionResult> AddMediaToPlaylist(long id, [FromBody] DtoAddMediaToPlaylistRequest request)
```

**Request:**
- Route-Parameter: `id` (Playlist-ID)
- Body: `DtoAddMediaToPlaylistRequest` mit `MediaType` und `MediaId`

**Aktuelle Implementierung (Probleme):**

1. **Zeile 214–217:** Service-Aufruf
   ```csharp
   CheckLogedIn();
   var result = await _playlistService.AddMediaToPlaylistAsync(
       id, CurrentUser!.Id, request.MediaType, request.MediaId, HttpContext.RequestAborted);
   return Ok(result);
   ```
   - ✅ Ruft Service auf
   - ❌ Gibt `DtoPlaylistEntry` zurück (einzeln)
   - Nach Anforderung: Wird `DtoPlaylistAddResult` sein

2. **Zeile 234–238:** Exception Handling
   ```csharp
   catch (InvalidOperationException ex)
   {
       Logger.LogWarning(ex, "Fehler beim Hinzufuegen zur Playlist {PlaylistId}", id);
       return MapInvalidOperationException(ex, "bereits in dieser Playlist vorhanden");
   }
   ```
   - ❌ Mappt auf HTTP 409 (via `MapInvalidOperationException`)
   - Nach Anforderung: Diese Exception wird nicht mehr geworfen

3. **Zeile 219–233:** Fehler-Handling für echte Fehler
   - ✅ KeyNotFoundException → 404 (Medieninhalt/Playlist nicht gefunden)
   - ✅ PlaylistAccessDeniedException → 403 (Zugriff verweigert)
   - ✅ Andere Exceptions → 500 (Server-Fehler)
   - Diese bleiben nach der Anforderung bestehen

**Hilfsmethode: `MapInvalidOperationException` (Zeile 196–202)**
```csharp
private static IActionResult MapInvalidOperationException(InvalidOperationException ex, string conflictSubstring = "existiert bereits")
{
    if (ex.Message.Contains(conflictSubstring, StringComparison.OrdinalIgnoreCase))
        return new ConflictObjectResult(ex.Message);  // HTTP 409

    return new BadRequestObjectResult(ex.Message);  // HTTP 400
}
```
- ❌ Wird nach Anforderung nicht mehr für Duplikate benötigt
- ✅ Aber noch für andere InvalidOperationException Fälle benutzt (z.B. MaxItemCount überschritten)
- Anpassung: Substring-Check ändert sich von "bereits in dieser Playlist vorhanden" zu etwas anderem (oder entfernen)

#### `GET /api/playlists/{id}/entries` — GetPlaylistEntries (Zeile 287–316)
- ✅ Unverändert für diese Anforderung
- Wird nach `AddMediaToPlaylist` von `PlaylistDetail.razor` aufgerufen

#### `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` — RemoveMediaFromPlaylist (Zeile 252–281)
- ✅ Unverändert für diese Anforderung

### HTTP-Status-Codes (Änderungen nach Anforderung)

| Szenario | Aktuell | Nach Anforderung |
|----------|---------|------------------|
| Top-Level-Duplikat | HTTP 409 Conflict | HTTP 200 OK + Message |
| Cascade-Duplikat (alle) | HTTP 200 OK (stumm) | HTTP 200 OK + Message |
| Teilweise Duplikate | HTTP 200 OK (stumm) | HTTP 200 OK + Message |
| Medieninhalt nicht gefunden | HTTP 404 Not Found | HTTP 404 Not Found (unverändert) |
| Playlist nicht gefunden | HTTP 404 Not Found | HTTP 404 Not Found (unverändert) |
| Max-Items überschritten | HTTP 400/409 Bad Request | HTTP 400 Bad Request (unverändert) |
| Zugriff verweigert | HTTP 403 Forbidden | HTTP 403 Forbidden (unverändert) |

### Response-Format (Änderungen nach Anforderung)

**Aktuell (HTTP 200 OK):**
```json
{
  "id": 10,
  "playlistId": 1,
  "mediaType": "Movie",
  "mediaId": 5,
  "mediaTitle": "The Matrix",
  "parentMediaType": null,
  "parentMediaId": null,
  "parentMediaTitle": null,
  "addedAt": "2026-09-05T14:30:00Z"
}
```

**Nach Anforderung (HTTP 200 OK):**
```json
{
  "topLevelEntry": {
    "id": 10,
    "playlistId": 1,
    "mediaType": "Movie",
    "mediaId": 5,
    "mediaTitle": "The Matrix",
    "parentMediaType": null,
    "parentMediaId": null,
    "parentMediaTitle": null,
    "addedAt": "2026-09-05T14:30:00Z"
  },
  "addedEntries": [
    { "id": 10, ... }
  ],
  "skippedDuplicateCount": 0,
  "message": "1 Titel hinzugefügt."
}
```

### VideoWebPlayerClient Verbraucher
Datei: `VideoWebPlayer.Client\VideoWebPlayerClient.cs` Zeile 450–455

```csharp
public async Task<DtoPlaylistEntry> AddMediaToPlaylistAsync(long playlistId, DtoAddMediaToPlaylistRequest request)
{
    var json = JsonSerializer.Serialize(request);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    return await HttpPostAsync<DtoPlaylistEntry>($"api/playlists/{playlistId}/entries", content);
}
```

- ❌ Rückgabetype: `Task<DtoPlaylistEntry>` (wird zu `Task<DtoPlaylistAddResult>`)
- ✅ Endpoint-Route korrekt
- Nach Anforderung: Der generische Typ in `HttpPostAsync<T>` muss geändert werden
