# Enums und Konstanten

## PlaylistSortMode Enum

**Datei:** `VideoWebPlayer/Data/PlaylistSortMode.cs`

Definiert die beiden unterstützten Sortier-Modi für Playlists.

| Wert | Numerischer Wert | Verwendung |
|---|---|---|
| ByReleaseDate | 0 (implizit) | Einträge werden automatisch nach Veröffentlichungsdatum sortiert; PlaylistEntry.SortOrder bleibt null |
| Manual | 1 (implizit) | Benutzer kann manuelle Reihenfolge per Drag & Drop setzen; PlaylistEntry.SortOrder wird mit Ganzzahlen gefüllt |

**Konvertierung:** 
- Client sendet String ("ByReleaseDate" oder "Manual")
- Server parst mit `Enum.TryParse<PlaylistSortMode>(sortMode, ignoreCase: true)`
- PlaylistService.ParseSortModeOrThrow (Zeilen 283–289) wirft ArgumentException bei ungültigen Werten

## MediaTypeValues Konstanten

**Datei:** `VideoWebPlayer.Client/Models/MediaTypeValues.cs`

Zentrale String-Konstanten für die 5 unterstützten Medientypen als Playlist-Einträge.

```csharp
public static class MediaTypeValues
{
    public const string Movie = "Movie";
    public const string TVShowEpisode = "TVShowEpisode";
    public const string TVShowSeason = "TVShowSeason";
    public const string TVShow = "TVShow";
    public const string MovieCollection = "MovieCollection";
}
```

**Zweck:** 
- Zentrale Definition zum Vermeiden hardcodierter Strings
- Mirrort `VideoWebPlayer.Data.MediaType` Enum (serverseitig)
- Wird als Medientyp-Dropdown in PlaylistEntriesList.razor genutzt (Zeilen 22–28)

**Verwendung im aktuellen Code:**
```razor
<select class="form-control playlist-add-mediatype-select" @bind="newEntryMediaType">
    <option value="@MediaTypeValues.Movie">Film</option>
    <option value="@MediaTypeValues.TVShow">Serie</option>
    <option value="@MediaTypeValues.TVShowSeason">Staffel</option>
    <option value="@MediaTypeValues.TVShowEpisode">Episode</option>
    <option value="@MediaTypeValues.MovieCollection">Filmsammlung</option>
</select>
```

**Verwendung in neuer MediaSearchSelector-Komponente:**
Könnte diese Konstanten für Typ-Anzeige/Filterung nutzen.

## MediaType Enum (Serverseitig)

**Datei:** `VideoWebPlayer/Data/MediaType.cs` (vermutlich)

Entspricht den Client-Konstanten, aber als C#-Enum für serverseitige Typ-Sicherheit.

```csharp
public enum MediaType
{
    Movie,
    TVShowEpisode,
    TVShowSeason,
    TVShow,
    MovieCollection
}
```

**Konvertierung Client → Server:**
- `MediaHierarchyRegistry.ParseMediaType(string)` konvertiert den String zu diesem Enum
- String wird normalisiert via `.ToString()` zurück zu String für DB-Speicherung in PlaylistEntry.MediaType

**Kaskaden-Auflösung:**
PlaylistService.GetCascadeMediaIdsAsync (privat) nutzt den MediaType-Enum um festzustellen, ob Kaskaden-Einträge erforderlich sind:
- `MediaType.TVShow` → alle Episoden hinzufügen
- `MediaType.TVShowSeason` → alle Episoden dieser Staffel hinzufügen
- Andere → keine Kaskaden

## Verwandte Konstanten

### Playlist-Limits

**Datei:** `VideoWebPlayer/Configuration/PlaylistSettings.cs` (vermutlich)

Konfigurierbare Limits (aus PlaylistsController Nutzung):

| Setting | Zweck |
|---|---|
| MaxPlaylistsPerUser | Maximum Playlists pro Benutzer (optional) |
| MaxPlaylistItemCount | Maximum Einträge pro Playlist (optional) |
| DefaultPageSize | Standard-Seitengrößen für Pagination (Standard: 30?) |
| MaxPageSize | Maximale Seitengröße zum Abrufen (Standard: 500?) |

Werden in PlaylistsController.GetPlaylistEntriesPaged genutzt (Zeilen 274–280).

### Picture IDs

Keine Konstanten, aber wichtige Konvention:

- **PictureId** in MediaEntryDto, Playlist-Entry-DTOs, etc. ist die ID eines Picture-Eintrags
- **Zugriff:** `/api/pictures/{pictureId}?access_token={token}`
- **Fallback-Reihenfolge:** Poster → Banner → Fanart (implementiert in PlaylistEntryAccessResolver und MediaBaseEntryList)
- **Fehler:** Beim Laden wird `onerror="this.onerror=null;this.src='/images/placeholder.png';"` genutzt

## HTTP Status Codes (im Kontext der Playlist-API)

**In PlaylistEntriesList.razor Fehlerbehandlung:**

| Status | Bedeutung | Handler |
|---|---|---|
| 200 OK | Erfolg (implizit bei keinem Error) | Nachricht aus AddResult zeigen |
| 404 NotFound | Media nicht gefunden oder Eintrag nicht vorhanden | "Medieninhalt wurde nicht gefunden." |
| 403 Forbidden | Kein Zugriff auf Playlist (nicht Besitzer) | "Sie sind nicht Besitzer dieser Playlist." |
| 400 BadRequest | Ungültige Eingabe (z.B. mediaId ≤ 0) | Generische Fehlerbehandlung |
| 409 Conflict | Z.B. Playlist nicht im Manual-Mode bei Reorder | Generische Fehlerbehandlung |
| 500 Internal Server Error | Server-Fehler | Generische Fehlerbehandlung |

## HTTP Header und Query-Parameter

### Bearer Token Authorization

PlaylistsController ist mit `[BearerTokenCheck]` Attribute geschützt.
Token wird vom Client automatisch in HTTP Authorization Header gesetzt.

### Query-Parameter für ItemsController.Get (Such-Endpoint)

| Parameter | Typ | Standard | Beispiel |
|---|---|---|---|
| mediaSourceId | long | (optional) | `?mediaSourceId=5` |
| page | int | 0 | `?page=0` |
| size | int | 30 | `?size=30` |
| search | string | (optional) | `?search=Breaking` |
| genreId | long | (optional) | `?genreId=18` |

**Neue MediaSearchSelector-Komponente würde nutzen:**
```
GET /api/items?search={searchTerm}&page=0&size=30
```

Ohne mediaSourceId/genreId Filter (alle Quellen/Genres).

## Zusammenfassung für Implementierung

| Komponente | Verwendete Konstanten/Enums |
|---|---|
| PlaylistEntriesList.razor (aktuell) | MediaTypeValues (dropdown) |
| MediaSearchSelector.razor (neu) | MediaTypeValues (für Typ-Anzeige) |
| ItemsController.Get | MediaTypeValues (als DTO.Type gesetzt) |
| PlaylistService | MediaType Enum, PlaylistSortMode Enum |
| DtoAddMediaToPlaylistRequest | Erwartet MediaTypeValues-String |
| DtoPlaylistEntry | MediaType als String (aus DB) |

**Wichtig:** Alle Typ-Namen sind **Case-Sensitive** ("Movie" nicht "movie") gemäß MediaTypeValues Definitionen.
