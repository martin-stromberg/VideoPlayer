# DTO-Struktur und Mapping

## DTO-Speicherort

**Pfad:** `VideoWebPlayer.Client/Models/`

DTOs sind im separaten Client-Projekt definiert, nicht im Backend-Projekt. Dies ermöglicht Wiederverwendung auf Client-Seite (Blazor, etc.) und klare Separation von Domain-Model (Backend) und Transfer-Objekt (Client).

**Namenkonvention:** `Dto*` Präfix (z.B. `DtoFavoriteEntry`, `DtoMovie`)

## DtoFavoriteEntry (Referenz-DTO)

**Datei:** `VideoWebPlayer.Client/Models/FavoriteEntry.cs`

Namespace: `VideoWebPlayer.Data` (Achtung: DTOs sind im Data-Namespace, obwohl sie DTOs sind)

```csharp
public class DtoFavoriteEntry
{
    public long Id { get; set; }
    public DtoMediaEntry Entry { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
```

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Eindeutige ID des Favorite-Eintrags |
| `Entry` | `DtoMediaEntry` | Die favorisierte Media-Entität (lazy-loaded, daher null!) |
| `CreatedAt` | DateTime | Erstellungszeitstempel |

### Besonderheiten

- **Keine UserId:** DTOs exponieren keine UserId (Sicherheit: Client erfährt nicht die User-IDs anderer)
- **Null-Forgiving Operator:** `Entry` wird mit `= null!` initialisiert, da es im Service lazy-geladen wird
- **Polymorphe Entry:** `DtoMediaEntry` ist Basis-Klasse, `Entry` kann `DtoMovie`, `DtoTVShow`, `DtoTVShowEpisode`, etc. sein
- **Automatisches Mapping:** Wird via `Create<T>()` Reflection-Methode erzeugt

## DtoMediaEntry (Polymorphe Base)

**Datei:** `VideoWebPlayer.Client/Models/` (Teil der Model-Hierarchie)

`DtoMediaEntry` ist abstrakt oder sehr generisch und wird durch spezifische DTOs überschrieben:
- `DtoMovie`
- `DtoTVShow`
- `DtoTVShowEpisode`
- `DtoTVShowSeason`
- `DtoMovieCollection`
- `DtoMediaEntry` (generisch)

**Pattern:** Polymorphe Rückgabewerte ermöglichen Different Media-Typen in einem Response-Array.

## Mapping-Pattern: `Create<T>(source)`

**Location:** `ApiBaseController` und `FavoritesService` (identische Implementierung)

**Verwendung im Service:**
```csharp
var favDto = Create<DtoFavoriteEntry>(rec);  // rec ist FavoriteEntry
```

### Algorithmus

1. Erstelle neue Instanz: `Activator.CreateInstance<T>()`
2. Für jede Property in `T`:
   - Skip Properties mit `[IgnoreAssignProperty]` Attribut
   - Finde Property mit gleichem Namen in Source
   - Wenn vorhanden: Kopiere Wert via Reflection

### Limitations

- **Property-Namen müssen exakt passen:** `FavoriteEntry.Id` → `DtoFavoriteEntry.Id`
- **Keine Custom-Transformationen:** Nur Straight-Copy
- **Keine Hierarchie-Handling:** Nested Objects müssen manuell gemappt werden

**Workaround für Hierarchie:**
```csharp
var episodeDto = Create<DtoTVShowEpisode>(episodeEntity);
var seasonEntity = await _db.TVShowSeasons.FirstOrDefaultAsync(...);
episodeDto.Season = Create<DtoTVShowSeason>(seasonEntity);  // Manuell zuweisen
```

## IgnoreAssignPropertyAttribute

**Funktion:** Marker-Attribut, um Properties vom automatischen Mapping auszuschließen

**Verwendung:**
```csharp
public class DtoPlaylist
{
    public long Id { get; set; }
    
    [IgnoreAssignProperty]
    public string UserId { get; set; }  // Won't be copied
}
```

## Häufige DTO-Patterns im System

### Datums-Konvention

DTOs verwenden `DateTime` (nicht `DateTimeOffset`):
```csharp
public DateTime CreatedAt { get; set; }
public DateTime UpdatedAt { get; set; }
```

Speicherung erfolgt in UTC, Serialisierung ist ISO 8601.

### Nullable-Felder

Optionale Felder sind als nullable Types definiert:
```csharp
public string? Description { get; set; }
public long? TVShowSeasonId { get; set; }
```

### Request vs. Response DTOs

**Response (vom Server zum Client):**
- Enthält `Id` und Timestamps (`CreatedAt`, `UpdatedAt`)
- Größere Struktur mit assoziierten Entitäten

**Request (vom Client zum Server):**
- Keine `Id` (wird vom Server generiert)
- Keine Timestamps (werden vom Server gesetzt)
- Enthält nur Eingabe-Felder

**Beispiel Favorites:**
- Response: `DtoFavoriteEntry` mit `Id`, `CreatedAt`, `Entry`
- Request: `FavoriteEntry` (raw Entity, wird direkt akzeptiert)

## Serialisierung

**Format:** JSON (via ASP.NET Core ModelBinder)

**Conventions:**
- Property-Namen werden als camelCase serialisiert (z.B. `createdAt` in JSON)
- Null-Werte werden in JSON included (nicht omitted)
- DateTime wird ISO 8601 serialisiert

**Konfiguration:** Erfolgt via ASP.NET Core AddControllers() mit Standard-Optionen

## Zusammenfassung für Playlist-DTOs

Die Playlist-DTOs sollten folgendes Pattern folgen:

### `DtoPlaylist` (Response)
```csharp
public class DtoPlaylist
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string SortMode { get; set; } = "ByReleaseDate";  // "ByReleaseDate" oder "Manual"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    [IgnoreAssignProperty]
    public string UserId { get; set; }  // Nicht exposiert
}
```

### `DtoCreatePlaylistRequest` (Request)
```csharp
public class DtoCreatePlaylistRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? SortMode { get; set; } = "ByReleaseDate";
}
```

### `DtoUpdatePlaylistRequest` (Request)
```csharp
public class DtoUpdatePlaylistRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? SortMode { get; set; }
}
```

Diese werden in `VideoWebPlayer.Client/Models/` definiert.
