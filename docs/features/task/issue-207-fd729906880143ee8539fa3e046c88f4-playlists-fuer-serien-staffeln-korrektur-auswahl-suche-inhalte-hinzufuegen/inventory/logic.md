# Logik-Klassen

Analyse der Logik-Komponenten, die von der Anforderung betroffen sind.

## `ItemsController`

Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

### Öffentliche Methode

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Get(long? mediaSourceId, int page, int size, string? search, long? genreId)` | public async | **Problematisch:** Ruft alle fünf Hilfsmethoden auf; Endpunkt wird von zwei unterschiedlichen Use Cases verwendet (Playlist-Suche und Quellen-Browsing) |

### Private Hilfsmethoden (Search-Filter)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetMovieCollectionEntriesAsync(filter, mediaSourceIds, unlockedIds)` | private async | Ruft MovieCollections aus der DB ab; **Case-sensitives Filter:** `.Where(e => e.Name.Contains(filter.Search))` |
| `GetTVShowEntriesAsync(filter, mediaSourceIds, unlockedIds)` | private async | Ruft TVShows ab; **Case-sensitives Filter:** `.Where(e => e.Name.Contains(filter.Search))` |
| `GetMovieEntriesAsync(filter, mediaSourceIds, unlockedIds)` | private async | Ruft Movies ab (neu in Anforderung); **Case-sensitives Filter:** `.Where(e => e.Name.Contains(filter.Search))` |
| `GetSeasonEntriesAsync(filter, mediaSourceIds, unlockedIds)` | private async | Ruft TVShowSeasons ab (neu in Anforderung); **Case-sensitives Filter:** `.Where(e => e.Name.Contains(filter.Search))` |
| `GetEpisodeEntriesAsync(filter, mediaSourceIds, unlockedIds)` | private async | Ruft TVShowEpisodes ab (neu in Anforderung); **Case-sensitives Filter:** `.Where(e => e.Name.Contains(filter.Search))` |

### Problem 1: Case-sensitive Suche
Alle fünf `Get*EntriesAsync()`-Methoden verwenden das gleiche Muster:
```csharp
if (!string.IsNullOrWhiteSpace(filter.Search))
    query = query.Where(e => e.Name.Contains(filter.Search));
```

**Auswirkung:** Der C#-Operator `.Contains()` wird vom SQLite EF Core Provider zu `instr(...)` übersetzt, was case-sensitiv ist. Eine Suche nach "breaking bad" findet kein Element mit dem Namen "Breaking Bad".

**Betroffene Zeilen:**
- `GetMovieCollectionEntriesAsync`: Zeile 188
- `GetTVShowEntriesAsync`: Zeile 226
- `GetMovieEntriesAsync`: Zeile 259
- `GetSeasonEntriesAsync`: Zeile 292
- `GetEpisodeEntriesAsync`: Zeile 325

### Problem 2: Ungewollte Vergrößerung des Quellen-Browsing-Ergebnissets
Die `Get()`-Methode ruft alle fünf Hilfsmethoden immer auf:
```csharp
var movieCollections = await GetMovieCollectionEntriesAsync(...);
var tvShows = await GetTVShowEntriesAsync(...);
var movies = await GetMovieEntriesAsync(...);        // NEU
var seasons = await GetSeasonEntriesAsync(...);      // NEU
var episodes = await GetEpisodeEntriesAsync(...);    // NEU

var entries = movieCollections
    .Concat(tvShows)
    .Concat(movies)      // NEU - bricht bisheriges Quellen-Browsing
    .Concat(seasons)     // NEU
    .Concat(episodes)    // NEU
    .OrderBy(e => e.Title)
    .Skip(page * size)
    .Take(size)
    .ToList();
```

**Auswirkung:** Wenn `mediaSourceId` gesetzt ist (Quellen-Browsing-Use-Case), werden jetzt 5 Medientypen statt ursprünglich 2 zurückgegeben, was die Detailseite einer Medienquelle mit redundanten Einträgen überflutet.

**Lösungsansatz (nach Anforderung):** Option A ist empfohlen - Opt-in-Parameter `includeIndividualMediaTypes`, der standardmäßig `false` ist. Nur wenn dieser Parameter `true` ist, werden `GetMovieEntriesAsync`, `GetSeasonEntriesAsync` und `GetEpisodeEntriesAsync` aufgerufen.

## `VideoWebPlayerClient`

Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

### Relevante Methoden zur API-Kommunikation

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RequestItemsCoreAsync(mediaSourceId?, page, size, search, genreId, cancellationToken)` | private async | **Zentrale Methode:** Baut URL zum `/api/items`-Endpunkt und führt Request aus |
| `RequestSourceItems(mediaSourceId, page?, pageSize?, searchText?, genreId?)` | public | Wrapper für Quellen-Browsing (ruft `RequestItemsCoreAsync` auf) |
| `RequestItemsAsync(search?, page?, size?, cancellationToken?)` | public | Wrapper für Playlist-Mediensuche (ruft `RequestItemsCoreAsync` mit `mediaSourceId=null` auf) |

**Abhängigkeit:** Beide öffentlichen Methoden verwenden die gleiche `RequestItemsCoreAsync()`-Methode. Ein neuer Opt-in-Parameter müsste dort hinzugefügt werden.

**Zeilen:**
- `RequestItemsCoreAsync`: 455-465
- `RequestSourceItems`: 467-468
- `RequestItemsAsync`: 504-505

## `MediaSearchSelector.razor` (Razor-Komponente)

Datei: `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor`

### Zentrale Methode

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SearchAsync(cancellationToken)` | private async | Ruft auf Zeile 105: `Client.RequestItemsAsync(searchTerm, 0, PageSize, cancellationToken)` |

**Bedeutung:** Diese Komponente würde den neuen Opt-in-Parameter setzen müssen (falls Option A gewählt wird), um alle 5 Medientypen einzuschließen.

## `MediaSourceDetailsViewModel.cs`

Datei: `VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs`

### Zentrale Methode

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadNextPageAsync(sourceId)` | public async | Ruft auf Zeile 100: `_client.RequestSourceItems(sourceId, page, PageSize, searchText, selectedGenreId ?? 0)` |

**Bedeutung:** Dieses ViewModel wird für Quellen-Browsing verwendet. Es sollte NICHT den Opt-in-Parameter setzen (bzw. setzen ihn auf `false`), um das bisherige Verhalten (nur 2 Typen) beizubehalten.

Zeile 105 in der Anforderung erwähnt diese ViewModel als betroffene Komponente, die derzeit "nur 2 Ergebnistypen erwartet" und nach der Änderung aber "5 Typen bekommt" - was das Ziel dieser Korrektur ist zu verhindern.

## Zusammenfassung der Code-Auswirkungen

**Zu korrigieren:**
1. Case-sensitive `.Contains()` → Case-insensitive Variante in allen 5 `Get*EntriesAsync()`-Methoden
2. Opt-in-Parameter hinzufügen, um individuellen Medientypen nur bei Playlist-Suche zu aktivieren
3. `RequestItemsCoreAsync` aktualisieren, um neuen Parameter zu unterstützen
4. `MediaSearchSelector.razor` aktualisieren, um den neuen Parameter zu setzen
5. `MediaSourceDetailsViewModel` sicherstellen, dass der Parameter nicht gesetzt wird (oder explizit auf `false`)
