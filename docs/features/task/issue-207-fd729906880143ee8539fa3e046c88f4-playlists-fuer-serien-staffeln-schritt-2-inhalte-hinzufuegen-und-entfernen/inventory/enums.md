# Bestandsaufnahme: Enums und Konstanten

## `MediaType` Enum
Datei: `VideoWebPlayer\Data\MediaType.cs`

| Wert | Bedeutung | Hat Kaskaden? |
|------|-----------|--------------|
| `Movie` | Ein einzelner Film | Nein (Leaf-Node) |
| `TVShowEpisode` | Eine einzelne TV-Show-Episode | Nein (Leaf-Node) |
| `TVShowSeason` | Eine TV-Show-Staffel | Ja — kaskadiert zu ihren Episoden |
| `TVShow` | Eine komplette TV-Show | Ja — kaskadiert zu Staffeln und deren Episoden |
| `MovieCollection` | Eine Filmsammlung | Ja — kaskadiert zu ihren Filmen |

**Zusammenhang mit AddMediaToPlaylistAsync:**
- Die `MediaTypeHandler` Dictionary (Zeile 338–380 in PlaylistService.cs) definiert für jeden Enum-Wert, ob er Cascade-Logik hat
- Wenn `LoadCascadeChildrenAsync` für einen Typ vorhanden ist, werden Kinder automatisch hinzugefügt
- Neue Einträge verwenden den kanonischen Enum-Wert via `ToString()`

## `MediaTypeValues` Konstanten
Datei: `VideoWebPlayer.Client.Models\MediaTypeValues.cs`

| Konstante | Wert | Verwendung |
|-----------|------|-----------|
| `MediaTypeValues.Movie` | `"Movie"` | Client-seitige Konstante, wird in DTOs und Razor-Komponenten verwendet |
| `MediaTypeValues.TVShowEpisode` | `"TVShowEpisode"` | Für UI-Dropdowns und API-Requests |
| `MediaTypeValues.TVShowSeason` | `"TVShowSeason"` | |
| `MediaTypeValues.TVShow` | `"TVShow"` | |
| `MediaTypeValues.MovieCollection` | `"MovieCollection"` | |

**Verwendung in PlaylistDetail.razor (Zeile 68–74):**
```razor
<select class="form-control playlist-add-mediatype-select" @bind="newEntryMediaType">
    <option value="@MediaTypeValues.Movie">Film</option>
    <option value="@MediaTypeValues.TVShow">Serie</option>
    <option value="@MediaTypeValues.TVShowSeason">Staffel</option>
    <option value="@MediaTypeValues.TVShowEpisode">Episode</option>
    <option value="@MediaTypeValues.MovieCollection">Filmsammlung</option>
</select>
```

**Beobachtung:**
- Die UI sendet nur diese konstanten Werte (immer mit korrektem Casing)
- Aber die REST-API akzeptiert jeden String (da `ParseMediaType` case-insensitiv ist)
- Über API können "movie", "MOVIE", "Movie" gesendet werden, was zu Normalisierungsproblemen führt

## Beziehung zwischen MediaType Enum und MediaTypeValues

| Enum-Wert | ToString() | MediaTypeValues Konstante | Match? |
|-----------|-----------|--------------------------|--------|
| `MediaType.Movie` | `"Movie"` | `MediaTypeValues.Movie` = `"Movie"` | ✅ Ja |
| `MediaType.TVShowEpisode` | `"TVShowEpisode"` | `MediaTypeValues.TVShowEpisode` = `"TVShowEpisode"` | ✅ Ja |
| `MediaType.TVShowSeason` | `"TVShowSeason"` | `MediaTypeValues.TVShowSeason` = `"TVShowSeason"` | ✅ Ja |
| `MediaType.TVShow` | `"TVShow"` | `MediaTypeValues.TVShow` = `"TVShow"` | ✅ Ja |
| `MediaType.MovieCollection` | `"MovieCollection"` | `MediaTypeValues.MovieCollection` = `"MovieCollection"` | ✅ Ja |

Die Konstanten spiegeln die kanonischen Enum-Werte korrekt wider.
