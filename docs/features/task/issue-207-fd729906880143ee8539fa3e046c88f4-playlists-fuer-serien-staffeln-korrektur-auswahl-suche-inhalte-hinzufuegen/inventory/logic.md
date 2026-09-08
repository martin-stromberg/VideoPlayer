# Logikklassen für Unicode-korrekte Namenssuche

## `ItemsController`
Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

Controller für Media-Browsing und Streaming-Endpoints.

### Relevant für die Anforderung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Get(long? mediaSourceId, int page, int size, string? search, long? genreId, bool includeIndividualMediaTypes)` | public async | **Haupt-Such-Endpoint** (HTTP GET `/api/items`). Nutzt `ApplySearchFilter<T>()` zur case-insensitiven Namenssuche auf allen fünf Medientypen. Dies ist der Einstiegspunkt für die Anforderung. |
| `ApplySearchFilter<T>(IQueryable<T> query, string? search)` | private static | **Zentrale Such-Filter-Methode** (Zeile 209–216). Implementiert die case-insensitive Substring-Suche auf der `Name`-Eigenschaft. Aktuell: `query.Where(e => e.Name.ToLower().Contains(lowered))`. Dies ist die Stelle, die Unicode-Probleme hat. |
| `GetMovieCollectionEntriesAsync(...)` | private async | Hilfsmethode für MovieCollection-Suche. Ruft `ApplySearchFilter()` auf (Zeile 224). |
| `GetTVShowEntriesAsync(...)` | private async | Hilfsmethode für TVShow-Suche. Ruft `ApplySearchFilter()` auf (Zeile 261). |
| `GetMovieEntriesAsync(...)` | private async | Hilfsmethode für Movie-Suche. Ruft `ApplySearchFilter()` auf (Zeile 293). |
| `GetSeasonEntriesAsync(...)` | private async | Hilfsmethode für TVShowSeason-Suche. Ruft `ApplySearchFilter()` auf (Zeile 325). |
| `GetEpisodeEntriesAsync(...)` | private async | Hilfsmethode für TVShowEpisode-Suche. Ruft `ApplySearchFilter()` auf (Zeile 357). |

### Andere relevante Methoden (Kontext)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGenres()` | public async | Abrufen von Genre-Optionen (nicht sucherrelevant) |
| `UpdateMetadata(...)` | public async | Metadaten-Aktualisierung (nicht sucherrelevant) |
| `GetRecent()` | public async | Kürzlich angesehene Medien (nicht sucherrelevant) |
| `StreamMediaItem(...)` | public async | Streaming-Endpoint (nicht sucherrelevant) |
| `Download(...)` | public async | Download-Endpoint (nicht sucherrelevant) |
| `Get(string type, long id)` | public async | Detail-Abruf (nicht sucherrelevant) |

### Event-Abos und Publishings

Keine direkt relevanten Event-Abos oder Publishings. Der Controller ist Konsument des `ApplicationDbContext`.

### Private Records / Hilfstypen

| Typ | Beschreibung |
|-----|-------------|
| `MediaEntryFilter` (record struct) | Bündelt Such-/Filter-Parameter für alle fünf `Get*EntriesAsync`-Hilfsmethoden (Zeile 199). Enthält `MediaSourceId`, `Search`, `GenreId`, `Page`, `Size`, `IncludeIndividualMediaTypes`. |

**Wichtig:** Die `ApplySearchFilter<T>()` Methode wird von allen fünf `Get*EntriesAsync`-Hilfsmethoden aufgerufen. Eine Änderung dort wirkt sich auf alle fünf Medientypen aus, was den Anforderungen entspricht.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

Entity Framework Core DbContext für die Anwendung.

### Relevant für die Anforderung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnModelCreating(ModelBuilder modelBuilder)` | protected override | Konfiguriert das EF Core Modell (Zeile 654–659). Lädt Konfigurationen aus der Assembly mit `ApplyConfigurationsFromAssembly()`. Dies ist die Stelle, wo ggf. benutzerdefinierte Datenbankfunktionen registriert werden könnten. |
| `OnConfiguring(DbContextOptionsBuilder optionsBuilder)` | protected override | *Nicht gelesen, aber wahrscheinlich vorhanden* — Würde Datenbank-Verbindung konfigurieren und auch für die Registrierung von SQL-Funktionen via `SqliteConnection.CreateFunction()` zuständig sein. |

### DbSets (relevant für Such-Szenarien)

| DbSet | Typ | Beschreibung |
|-------|-----|-------------|
| `MovieCollections` | DbSet<MovieCollection> | Eine der fünf Such-Quellen |
| `Movies` | DbSet<Movie> | Eine der fünf Such-Quellen |
| `TVShows` | DbSet<TVShow> | Eine der fünf Such-Quellen |
| `TVShowSeasons` | DbSet<TVShowSeason> | Eine der fünf Such-Quellen |
| `TVShowEpisodes` | DbSet<TVShowEpisode> | Eine der fünf Such-Quellen |

### Weitere DbSets (Kontext)

| DbSet | Typ | Beschreibung |
|-------|-----|-------------|
| `MediaSources` | DbSet<MediaSource> | Medienquellen (z. B. SFTP-Ordner) |
| `MediaSourceUsers` | DbSet<MediaSourceUser> | Benutzer-Berechtigungen für Quellen |
| `UnlockedMediaEntries` | DbSet<UnlockedMediaEntry> | Freigeschaltete Medien für Benutzer |
| `Genres` | DbSet<Genre> | Genre-Verwaltung |
| `MovieGenres` / `TVShowGenres` | DbSet<...> | Genre-Zuordnungen |
| `Pictures` | DbSet<Picture> | Bilder (Poster, Banner, Fanart) |
| (weitere) | - | User, Favorites, WatchedEntries, etc. |

### Abhängigkeiten für die Anforderung

- `ItemsController` nutzt `ApplicationDbContext` um die Abfragen über `DbSet<T>` zu konstruieren.
- Die Such-Filter in `ItemsController.ApplySearchFilter<T>()` werden als LINQ-Expressions zu EF Core DbSet-Abfragen hinzugefügt.
- EF Core übersetzt diese dann zu SQL (konkret: SQLite-SQL).
- Das Problem: EF Core übersetzt `.ToLower()` zu SQLites `lower()`-Funktion, die ohne ICU-Erweiterung unzureichend für Umlaute ist.

### Lösung im Kontext des DbContext

Eine Lösung könnte sein:
- **Option A:** Registrierung einer benutzerdefinierten Datenbankfunktion in `OnConfiguring()` via `SqliteConnection.CreateFunction()`, die `.ToLowerInvariant()` implementiert.
- **Option B:** Definition einer EF Core `DbFunction` (Attribut) mit `ModelBuilder.HasDbFunction()` in `OnModelCreating()`.
- **Option C:** Implementierung einer benutzerdefinierten Funktion als statische Klasse, die in `ApplySearchFilter<T>()` genutzt wird.

**Aktueller Status:** Weder `OnConfiguring` noch andere Function-Registrierungen wurden gescannt; `OnModelCreating` ruft nur `ApplyConfigurationsFromAssembly()` auf.
