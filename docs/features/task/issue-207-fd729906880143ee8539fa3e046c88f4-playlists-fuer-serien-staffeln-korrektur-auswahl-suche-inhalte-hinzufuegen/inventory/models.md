# Datenmodelle

Analyse der Datenmodellklassen, die von der Anforderung betroffen sind. Die Anforderung betrifft vor allem die Datenabfragen in den Get-Methoden des ItemsControllers, nicht die Modell-Struktur selbst.

## Betroffene Client-DTOs

### `MediaEntryDto`
Datei: `VideoWebPlayer.Client/Models/MediaEntryDto.cs`

DTO für die Darstellung von Medienergebnissen vom Server.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Type | string | Medientyp (z.B. "Movie", "TVShow", "TVShowSeason", "TVShowEpisode", "MovieCollection") |
| Id | long | Eindeutige Kennung des Medieneintrags |
| Title | string | Name/Titel des Medieneintrags |
| Description | string? | Beschreibung (z.B. Plot) |
| Url | string? | URL zur Detail-Seite |
| CreatedAt | DateTime | Erstellungsdatum |
| PictureId | long? | ID für Poster/Vorschaubild |
| ItemCount | int | Anzahl von Elementen (z.B. Movies in einer Collection) |
| WatchedAt | DateTime? | Zeitstempel des letzten Ansehens |

Keine Änderungen am DTO selbst erforderlich; die case-insensitive Suche und der Opt-in-Parameter sind Server-seitige Konzepte und müssen nicht im DTO sichtbar sein.

## Betroffene Server-Datenmodelle (Datenbankklassen)

Die Anforderung betrifft die **Abfragen** dieser Modelle, nicht ihre Struktur:

- **`MovieCollection`** - Name-Feld wird mit `.Contains()` durchsucht
- **`TVShow`** - Name-Feld wird mit `.Contains()` durchsucht
- **`Movie`** - Name-Feld wird mit `.Contains()` durchsucht
- **`TVShowSeason`** - Name-Feld wird mit `.Contains()` durchsucht
- **`TVShowEpisode`** - Name-Feld wird mit `.Contains()` durchsucht

Alle fünf Modelle werden in der `ItemsController.Get()`-Methode durchsucht. Der problematische Code befindet sich in den `Get*EntriesAsync()`-Hilfsmethoden, wo `.Where(e => e.Name.Contains(filter.Search))` case-sensitiv auf SQLite-ebene zu `instr()` übersetzt wird.

## `MediaEntryFilter` Record

Datei: `VideoWebPlayer/Controllers/ItemsController.cs` (Zeile 179)

```csharp
private readonly record struct MediaEntryFilter(
    long? MediaSourceId,
    string? Search,
    long? GenreId,
    int Page,
    int Size);
```

Dieser Record wird als Parameter-Bundle an alle fünf `Get*EntriesAsync()`-Methoden weitergeleitet. Eine Erweiterung um einen Parameter für den Opt-in für individuelle Medientypen könnte hier erfolgen, wenn Opt-in-Parameter (Option A der Anforderung) gewählt wird.
