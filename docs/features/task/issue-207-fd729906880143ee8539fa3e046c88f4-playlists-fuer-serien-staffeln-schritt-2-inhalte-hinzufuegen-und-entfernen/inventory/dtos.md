# Bestandsaufnahme: Data Transfer Objects (DTOs)

## `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client\Models\DtoPlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Eintrag-ID aus DB |
| `PlaylistId` | `long` | Playlist-ID |
| `MediaType` | `string` | Medientyp (eines der Konstanten aus MediaTypeValues) |
| `MediaId` | `long` | Medien-ID |
| `MediaTitle` | `string` | Titel des Mediums (z.B. "The Matrix") |
| `ParentMediaType` | `string?` | Typ der übergeordneten Sammlung oder null |
| `ParentMediaId` | `long?` | ID der übergeordneten Sammlung oder null |
| `ParentMediaTitle` | `string?` | Titel der übergeordneten Sammlung oder null |
| `AddedAt` | `DateTime` | Zeitstempel wann hinzugefügt |

**Verwendung:**
- Response von `PlaylistService.AddMediaToPlaylistAsync` (aktuell einzeln)
- Array-Response von `PlaylistService.GetPlaylistEntriesAsync`
- Wird von `PlaylistsController` zu Client serialisiert (JSON)

**Problem für diese Anforderung:**
- ❌ Nur ein einzelner Eintrag wird zurückgegeben
- ❌ Keine Informationen über übersprungene Duplikate
- ❌ Keine Benutzer-freundliche Meldung

## `DtoAddMediaToPlaylistRequest`
Datei: `VideoWebPlayer.Client\Models\DtoAddMediaToPlaylistRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `MediaType` | `string` | Ein Wert aus MediaTypeValues (z.B. "Movie", "TVShow") |
| `MediaId` | `long` | ID des Mediums |

**Verwendung:**
- Request-Body für `POST /api/playlists/{id}/entries` (PlaylistsController.AddMediaToPlaylist)
- Wird von `PlaylistDetail.razor` in Zeile 193–197 konstruiert:
  ```csharp
  await Client.AddMediaToPlaylistAsync(Id, new DtoAddMediaToPlaylistRequest
  {
      MediaType = newEntryMediaType,
      MediaId = newEntryMediaId
  });
  ```

## `DtoPlaylist`
Datei: `VideoWebPlayer.Client\Models\DtoPlaylist.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Playlist-ID |
| `Name` | `string` | Name (max 255 Zeichen validiert) |
| `Description` | `string?` | Beschreibung (max 2000 Zeichen) |
| `SortMode` | `string` | "Manual" oder "ByReleaseDate" (wird als kanonischer String gespeichert) |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |
| `UpdatedAt` | `DateTime` | Letztes Update |

**Für diese Anforderung nicht direkt betroffen**, aber wird von `PlaylistDetail.razor` geladen und angezeigt.

## Neue DTO (erforderlich nach Anforderung)

### `DtoPlaylistAddResult` (geplant, noch nicht vorhanden)
Wird nach der Anforderung benötigt für die neue Response-Struktur:

```csharp
public class DtoPlaylistAddResult
{
    /// <summary>
    /// Der neu hinzugefügte Top-Level-Eintrag, oder null wenn bereits vorhanden.
    /// </summary>
    public DtoPlaylistEntry? TopLevelEntry { get; set; }

    /// <summary>
    /// Array aller neu hinzugefügten Einträge (Top-Level + Kaskaden).
    /// </summary>
    public DtoPlaylistEntry[] AddedEntries { get; set; } = Array.Empty<DtoPlaylistEntry>();

    /// <summary>
    /// Anzahl der übersprungenen Duplikate.
    /// </summary>
    public int SkippedDuplicateCount { get; set; }

    /// <summary>
    /// Benutzer-freundliche Meldung (Deutsch oder Englisch nach Konfiguration).
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
```

**Wird diese Anforderung eingeführt:**
- `VideoWebPlayerClient.AddMediaToPlaylistAsync` gibt `DtoPlaylistAddResult` zurück statt `DtoPlaylistEntry`
- `PlaylistsController.AddMediaToPlaylist` serialisiert `DtoPlaylistAddResult` zu JSON
- `PlaylistDetail.razor` muss die neue Struktur verarbeiten

## Exception Handling in Controller (aktuell)

**PlaylistsController.AddMediaToPlaylist (Zeile 234–238):**
```csharp
catch (InvalidOperationException ex)
{
    Logger.LogWarning(ex, "Fehler beim Hinzufuegen zur Playlist {PlaylistId}", id);
    return MapInvalidOperationException(ex, "bereits in dieser Playlist vorhanden");
}
```

**MapInvalidOperationException (Zeile 196–202):**
```csharp
private static IActionResult MapInvalidOperationException(InvalidOperationException ex, string conflictSubstring = "existiert bereits")
{
    if (ex.Message.Contains(conflictSubstring, StringComparison.OrdinalIgnoreCase))
        return new ConflictObjectResult(ex.Message);  // HTTP 409

    return new BadRequestObjectResult(ex.Message);  // HTTP 400
}
```

- Wenn die Exception "bereits in dieser Playlist vorhanden" enthält → HTTP 409 Conflict
- Dies ist für Top-Level-Duplikate der Fall (Zeile 162–163 in PlaylistService)
- Nach der Anforderung wird dies nicht mehr geworfen, daher wird dieser Code vereinfacht/entfernt
