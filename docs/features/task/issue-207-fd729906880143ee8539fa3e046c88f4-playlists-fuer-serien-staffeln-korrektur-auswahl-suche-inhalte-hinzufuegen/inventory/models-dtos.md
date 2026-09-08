# Datenmodelle und DTOs

## Datenbank-Modelle

### `Playlist`
**Datei:** `VideoWebPlayer/Data/Playlist.cs`

Repräsentiert eine Benutzer-Playlist.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Primärschlüssel |
| UserId | string | FK zu ApplicationUser; Besitzer der Playlist |
| Name | string | Playlist-Name (max. 255 Zeichen) |
| Description | string | Optionale Beschreibung (max. 2000 Zeichen) |
| SortMode | PlaylistSortMode | ByReleaseDate oder Manual |
| CreatedAt | DateTime | Erstellungszeitpunkt (UTC) |
| UpdatedAt | DateTime | Aktualisierungszeitpunkt (UTC) |
| PlaylistEntries | ICollection<PlaylistEntry> | Enthaltene Einträge |

### `PlaylistEntry`
**Datei:** `VideoWebPlayer/Data/PlaylistEntry.cs`

Repräsentiert einen einzelnen Media-Eintrag in einer Playlist.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Primärschlüssel |
| PlaylistId | long | FK zu Playlist |
| Playlist | Playlist | Navigations-Eigenschaft |
| MediaType | string | Eine der Werte aus MediaTypeValues (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection) |
| MediaId | long | Identifikator des referenzierten Media-Inhalts |
| ParentMediaType | string | Nullable; MediaType der Kollektion, über die dieser Eintrag hinzugefügt wurde (z.B. TVShow für Cascade-Episoden) |
| ParentMediaId | long | Nullable; ID der Kollektion (z.B. TVShow-ID für Cascade-Episoden) |
| AddedAt | DateTime | Zeitpunkt, wann dieser Eintrag hinzugefügt wurde |
| SortOrder | long | Nullable; Manuelle Sortierungsreihenfolge (null wenn SortMode != Manual) |

## Transfer-Objekte (DTOs)

### `MediaEntryDto`
**Datei:** `VideoWebPlayer.Client/Models/MediaEntryDto.cs`

Verwendung für Suchergebnisse vom ItemsController.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Type | string | Medientyp als String (z.B. "MovieCollection", "TVShow", "Movie", "TVShowSeason", "TVShowEpisode") |
| Id | long | Medien-ID |
| Title | string | Titel oder Name des Mediums |
| Description | string | Optionale Beschreibung |
| Url | string | Navigations-URL |
| CreatedAt | DateTime | Erstellungsdatum |
| PictureId | long | Nullable; Poster/Thumbnail-ID für die Anzeige |
| ItemCount | int | Anzahl der Elemente (für Collections) |
| WatchedAt | DateTime | Nullable; Zuletzt angesehen (für Anzeigezwecke) |

**Verwendung:** Wird vom ItemsController.Get als Suchergebnis zurückgegeben; perfekt für Playlist-Suche geeignet.

### `DtoPlaylistEntry`
**Datei:** `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`

Repräsentation eines Playlist-Eintrags für den Client.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Playlist-Entry-ID |
| PlaylistId | long | Playlist-ID |
| MediaType | string | Medientyp (z.B. "Movie", "TVShowEpisode") |
| MediaId | long | Medien-ID |
| MediaTitle | string | Titel des referenzierten Media |
| ParentMediaType | string | Nullable; Typ der übergeordneten Kollektion (falls Cascade-Entry) |
| ParentMediaId | long | Nullable; ID der übergeordneten Kollektion |
| ParentMediaTitle | string | Nullable; Titel der übergeordneten Kollektion |
| AddedAt | DateTime | Zeitpunkt des Hinzufügens |
| ResolvedPictureId | long | Nullable; Server-seitig aufgelöste Bild-ID (PosterPictureId mit Fallback zu Banner/Fanart) |
| IsAccessible | bool | Ob der aktuelle Benutzer Zugriff auf diesen Eintrag hat |
| SortOrder | long | Nullable; Manuelle Sortierungsreihenfolge (nur wenn Playlist im Manual-Mode ist) |

**Verwendung:** Wird von PlaylistService zur Anzeige der Einträge in der UI erstellt.

### `DtoAddMediaToPlaylistRequest`
**Datei:** `VideoWebPlayer.Client/Models/DtoAddMediaToPlaylistRequest.cs`

Request-Körper für das Hinzufügen von Media zu einer Playlist.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| MediaType | string | Medientyp (z.B. "Movie", "TVShow") |
| MediaId | long | Medien-ID |

**Verwendung:** Wird von PlaylistEntriesList.razor beim Hinzufügen verwendet. **HINWEIS:** Mit der neuen MediaSearchSelector-Komponente würde diese DTO unverändert bleiben – nur die Art, wie MediaType/MediaId ermittelt werden, ändert sich.

### Verwandte Medien-Datenmodelle

#### `Movie`
**Datei:** `VideoWebPlayer/Data/Movie.cs`

| Relevante Eigenschaften | |
|---|---|
| Id | Primärschlüssel |
| Name | Titel |
| MovieCollectionId | FK zu MovieCollection |
| PosterPictureId | Nullable; Poster-Bild |
| BannerPictureId | Nullable; Banner-Bild |
| FanartPictureId | Nullable; Fanart-Bild |
| MediaSourceId | FK zu MediaSource |

#### `TVShow`
**Datei:** `VideoWebPlayer/Data/TVShow.cs`

| Relevante Eigenschaften | |
|---|---|
| Id | Primärschlüssel |
| Name | Serientitel |
| PosterPictureId | Nullable; Poster-Bild |
| Plot | Serienplot |
| MediaSourceId | FK zu MediaSource |

#### `TVShowSeason`
**Datei:** `VideoWebPlayer/Data/TVShowSeason.cs`

| Relevante Eigenschaften | |
|---|---|
| Id | Primärschlüssel |
| TVShowId | FK zu TVShow |
| Name | Staffel-Name (z.B. "Season 1") |
| PosterPictureId | Nullable |
| MediaSourceId | FK zu MediaSource |

#### `TVShowEpisode`
**Datei:** `VideoWebPlayer/Data/TVShowEpisode.cs`

| Relevante Eigenschaften | |
|---|---|
| Id | Primärschlüssel |
| TVShowSeasonId | FK zu TVShowSeason |
| Name | Episodentitel |
| PosterPictureId | Nullable |
| MediaSourceId | FK zu MediaSource |

#### `MovieCollection`
**Datei:** `VideoWebPlayer/Data/MovieCollection.cs`

| Relevante Eigenschaften | |
|---|---|
| Id | Primärschlüssel |
| Name | Sammlungstitel |
| PosterPictureId | Nullable |
| MediaSourceId | FK zu MediaSource |

#### `MediaBaseEntry`
**Datei:** `VideoWebPlayer/Data/MediaBaseEntry.cs`

Abstrakte Basisklasse für alle Medientypen; alle Media-Klassen erben von ihr.

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| Id | long | Primärschlüssel |
| Name | string | Titel |
| MediaSourceId | long | FK zu MediaSource |
| PosterPictureId | long | Nullable |
| BannerPictureId | long | Nullable |
| FanartPictureId | long | Nullable |

**Methode zum Abruf von Bild-URL:**
```csharp
// In MediaBaseEntryList.razor gezeigt:
// Fallback-Reihenfolge: Poster → Banner → Fanart → Placeholder
```

## Zugriffskontrolle (IUnlockedMediaService)

### Interface
**Datei:** `VideoWebPlayer/Services/IUnlockedMediaService.cs`

| Methode | Parameter | Rückgabe | Zweck |
|---|---|---|---|
| IsUnlockedAsync | DtoMediaEntry entry, CancellationToken | Task<bool> | Prüft, ob das Medium für den aktuellen Benutzer entsperrt ist |
| IsAccessible | bool hasSourceAccess, bool isUnlocked | bool | Kanonische Zugriffsregel: true wenn der Benutzer Zugriff auf die Quelle hat ODER das Medium entsperrt ist |
| GetUnlockedMovieCollectionIdsForUserAsync | string userId | Task<long[]> | Gibt alle IDs von MovieCollections zurück, die für den Benutzer entsperrt sind |
| GetUnlockedTVShowIdsForUserAsync | string userId | Task<long[]> | Gibt alle IDs von TVShows zurück, die für den Benutzer entsperrt sind |

**Verwendung in PlaylistService:**
- `PlaylistEntryAccessResolver` nutzt `IUnlockedMediaService` zur Bestimmung der IsAccessible-Eigenschaft für jeden Playlist-Eintrag
- Wird auch in `ItemsController.Get` genutzt zur Filterung von Such-Ergebnissen basierend auf Benutzer-Zugriff

## Konstanten und Enums

### `PlaylistSortMode` Enum
**Datei:** `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|---|---|
| ByReleaseDate | Einträge werden nach Veröffentlichungsdatum sortiert (SortOrder bleibt null) |
| Manual | Einträge werden manuell sortiert (SortOrder wird mit Wert gefüllt) |

### `MediaTypeValues` Konstanten
**Datei:** `VideoWebPlayer.Client/Models/MediaTypeValues.cs`

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

**Verwendung:** Von der PlaylistEntriesList.razor zur Befüllung des Medientyp-Dropdowns (Zeilen 22–27); würde bei neuer Komponente für Typ-Anzeige/Verarbeitung genutzt.
