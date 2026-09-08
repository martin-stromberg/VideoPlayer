# VideoWebPlayerClient und IPlaylistApiClient

## VideoWebPlayerClient

**Datei:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

Facade über die Server-API. Playlist-Operationen sind in einer partiellen Datei implementiert (vermutlich `VideoWebPlayerClient.Playlists.cs`).

### Basisstruktur

**HTTP-Helfer-Methoden (Zeilen 105–150):**
- `HttpGetAsync<T>(endPoint)` – GET mit Deserialisierung
- `HttpPostAsync<T>(endPoint, args)` – POST mit Deserialisierung
- `HttpPutAsync<T>(endPoint, args)` – PUT mit Deserialisierung
- `HttpPatchAsync<T>(endPoint, args)` – PATCH mit Deserialisierung

**Reauthorisierungs-Logik:**
- `SendWithReauthorizationAsync` – Retry bei 401 Unauthorized via `HandleUnauthorized()`
- Falls Reauth fehlschlägt, wird HttpRequestException mit StatusCode 401 geworfen

**Fehlerbehandlung:**
```csharp
private async Task<T> SendAndDeserializeAsync<T>(...)
{
    var response = await SendWithReauthorizationAsync(...);
    
    var content = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException(content, null, response.StatusCode);
    
    return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    }) ?? throw new InvalidOperationException("Deserialization returned null.");
}
```

## IPlaylistApiClient (Interface)

**Bekannte Methoden** (aus PlaylistEntriesList.razor Nutzung):

| Methode | Parameter | Rückgabe | Zweck |
|---|---|---|---|
| AddMediaToPlaylistAsync | long playlistId, DtoAddMediaToPlaylistRequest | Task<DtoPlaylistAddResult> | Medien-Eintrag hinzufügen |
| RemoveMediaFromPlaylistAsync | long playlistId, string mediaType, long mediaId | Task | Medien-Eintrag entfernen |
| RequestPlaylistEntriesPagedAsync | long playlistId, int pageNumber, int pageSize, CancellationToken | Task<PagedResult<DtoPlaylistEntry>> | Paginierte Einträge abrufen |
| RequestMaxSortOrderAsync | long playlistId | Task<long?> | Maximale SortOrder abrufen (für Move-to-End) |
| MoveEntryToBeginningAsync | long playlistId, long entryId | Task<DtoPlaylistAddResult> | Eintrag an Anfang verschieben |
| ReorderPlaylistEntryAsync | long playlistId, long entryId, DtoReorderPlaylistEntryRequest | Task | Eintrag neu ordnen |
| MoveEntryBetweenAsync | long playlistId, long entryId, DtoReorderPlaylistEntryRequest | Task | Eintrag Drag & Drop verschieben |

**LÜCKE:** Keine Suchmethode für Media-Suche vorhanden!

### Mögliche Erweiterungen

**Option 1 - Neue IPlaylistApiClient-Methode:**
```csharp
Task<List<MediaEntryDto>> SearchMediaForPlaylistAsync(
    string searchTerm, 
    CancellationToken cancellationToken = default);
```

**Option 2 - Nutze bestehende ItemsController.Get-Methode:**
```csharp
// In VideoWebPlayerClient (nicht Playlist-spezifisch):
public async Task<List<MediaEntryDto>> RequestItemsAsync(
    string? search = null, 
    int page = 0, 
    int size = 30, 
    CancellationToken cancellationToken = default)
{
    return await HttpGetAsync<List<MediaEntryDto>>(
        $"/api/items?search={Uri.EscapeDataString(search ?? "")}&page={page}&size={size}",
        cancellationToken);
}
```

**Option 3 - Neue Methode auf PlaylistsController (nicht auf Items):**
```csharp
[HttpGet("{id}/search-media")]
public Task<IActionResult> SearchMediaForPlaylist(long id, [FromQuery] string search)
```

Würde ItemsController.Get intern nutzen, aber Zugriff auf PlaylistId-Kontext haben.

## VideoWebPlayerClient.Playlists.cs (Vermutete Partielle Datei)

**Datei:** `VideoWebPlayer.Client/VideoWebPlayerClient.Playlists.cs` (Vermutung basiert auf Kommentar in VideoWebPlayerClient.cs Zeile 14–20)

Implementiert IPlaylistApiClient-Methoden. Detaillierte Implementierung nicht im eingesehenen Code, aber Muster orientiert sich an:

```csharp
public partial class VideoWebPlayerClient : IPlaylistApiClient
{
    // Beispiel-Muster:
    public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(
        long playlistId, 
        DtoAddMediaToPlaylistRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await HttpPostAsync<DtoPlaylistAddResult>(
            $"/api/playlists/{playlistId}/entries",
            content);
    }
}
```

## Bestehende Media-Suchmethoden (Referenz)

### Actors-Suche (aus VideoWebPlayerClient)

```csharp
public async Task<ActorDto[]> RequestActorsAsync(
    string? searchTerm = null,
    string sortMode = "alphabetical",
    string? filter = null,
    int offset = 0,
    int limit = 50)
{
    var url = $"/api/actors?offset={offset}&limit={limit}";
    if (!string.IsNullOrEmpty(searchTerm))
        url += $"&search={Uri.EscapeDataString(searchTerm)}";
    if (!string.IsNullOrEmpty(sortMode))
        url += $"&sort={Uri.EscapeDataString(sortMode)}";
    if (!string.IsNullOrEmpty(filter))
        url += $"&filter={Uri.EscapeDataString(filter)}";
    
    return await HttpGetAsync<ActorDto[]>(url);
}
```

**Lern-Punkte:**
- Query-Parameter werden per Uri.EscapeDataString() escaped
- Optionale Parameter per Bedingung hinzufügen
- Array-Rückgabe ist möglich (statt List)

## Fehlerbehandlung in Client-Code

**Beispiel aus PlaylistEntriesList.razor (Zeilen 254–269):**

```csharp
}, "Fehler beim Hinzufuegen", ex =>
{
    if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
    {
        entriesStatusIsError = true;
        entriesStatusMessage = "Medieninhalt wurde nicht gefunden.";
        return true;
    }
    if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden })
    {
        entriesStatusIsError = true;
        entriesStatusMessage = "Sie sind nicht Besitzer dieser Playlist.";
        return true;
    }
    return false;
});
```

**Pattern:** 
- Spezielle StatusCode-Behandlung für 404/403
- Generische Fehler fallen durch und werden von RunEntryActionAsync als "Internal Error" angezeigt

## Impersonation und Authorization

**VideoWebPlayerClient** erbt `InternalVideoWebPlayerClient` (nicht eingesehen), die wahrscheinlich Token-Handling und Impersonation-Logik bereitstellt.

**PlaylistEntriesList.razor nutzt `Client.AuthorizationToken`** (Zeile 280) für Bild-URL-Params:
```csharp
$"/api/pictures/{entry.ResolvedPictureId}?access_token={Client.AuthorizationToken}"
```

## Zusammenfassung – Fehlende Komponenten

| Fehlend | Empfohlene Implementierung | Details |
|---|---|---|
| Playlist-Medien-Suchmethode | Option 2: Nutze bestehende ItemsController.Get | Einfachste Variante, minimale Duplikation |
| | Option 1: Neue IPlaylistApiClient-Methode | Sauberer, aber mehr Code |
| | Option 3: Neue PlaylistsController-Route | Am meisten Kontrolle, aber komplexer |
