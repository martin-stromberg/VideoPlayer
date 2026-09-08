# Tests und Testausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-08 08:18-08:20 UTC+2
- **Branch und Commit-ID:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-3-automatische-sortierung-anzeige`, Commit `e185cdf35bb9cc7b4606bc1a57156ea1ef562652` (feat: Namenssuche statt Medien-Id beim Hinzufuegen zu Playlists)
- **Uncommittete Änderungen im getesteten Stand:** Einige Dateien waren modified (gemäß `git status`):
  - README.md
  - VideoWebPlayer.Client/VideoWebPlayerClient.cs
  - Mehrere Test- und Service-Dateien
  - Dokumentationsdateien
  
  Diese Änderungen beeinflussen den Testlauf möglich; wurde mit `--no-build` getestet (Binaries aus Debug-Build verwendet)

- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET Version: 10.0.11 (aus Test-Output erkennbar)
  - Test-Framework: xUnit.net VSTest Adapter v3.1.5+1b188a7b0a
  - Platform: Windows 11 Pro (64-bit)
  - Datenbank: SQLite In-Memory (shared-cache, per Test-Klasse isoliert)

- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Testprojekt: `VideoWebPlayer.Tests` (VideoWebPlayer.Tests.csproj)
  - Test-Befehl: `dotnet test VideoWebPlayer.Tests --no-build --logger "console;verbosity=minimal"`
  - Arbeitsverzeichnis: `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests --no-build --logger "console;verbosity=minimal"` | D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4 | 0 | 480 | 0 | 0 | [bj70g4kql.output](test-results/testlauf-1-minimal.log) |

### Nachgewiesene bestehende Testfehler

**Keine Testfehler nachgewiesen.** Alle 480 Tests bestanden erfolgreich im Ausgangszustand.

Die Tests, die die betroffenen Funktionen abdecken, funktionieren derzeit korrekt mit den bestehenden Eingabedaten. Diese Tests verwenden allerdings alle **genau passendes Case** bei der Suche (z.B. "Breaking" für "Breaking Point", nicht "breaking"), sodass sie das case-sensitive-Problem nicht aufdecken.

### Testlücken und Ausführungsprobleme

**Keine Build-, Setup- oder Infrastrukturfehler.**

**Vorhandene Testlücken zur Anforderung:**

1. **Case-insensitive Suche nicht getestet:** Die bestehenden Tests in `ItemsControllerTests_Search.cs` verwenden durchweg exakte oder Groß-/Kleinschreibung-Matching. Es gibt keine Tests, die eine Suche mit abweichender Groß-/Kleinschreibung abdecken (z.B. "breaking bad" findet "Breaking Bad").

2. **Quellen-Browsing-Regression nicht getestet:** Es gibt keine Tests, die explizit sicherstellen, dass Quellen-Browsing (mit `mediaSourceId` gesetzt) **nur** 2 Medientypen (MovieCollections, TVShows) zurückgibt und nicht die neuen Typen (Movies, TVShowSeasons, TVShowEpisodes) einschließt.

3. **Opt-in-Parameter nicht vorhanden:** Der Opt-in-Parameter `includeIndividualMediaTypes` existiert noch nicht, daher können keine Tests dafür geschrieben werden.

## Testklassen

### `ItemsControllerTests_Search`
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerTests_Search.cs`

Tests für die Medien-Suche und das Type-Feld:

- `Get_SearchMovies_ReturnsMatchingMovies()` — Sucht nach "Breaking" in Movies, findet "Breaking Point" (Case-sensitiv-Suche, exakte Groß-/Kleinschreibung)
- `Get_SearchTVShowSeasons_ReturnsMatchingSeasons()` — Sucht nach "Staffel Eins", findet die passende Staffel (exakte Groß-/Kleinschreibung)
- `Get_SearchTVShowEpisodes_ReturnsMatchingEpisodes()` — Sucht nach "Pilotfolge", findet die passende Episode (exakte Groß-/Kleinschreibung)
- `Get_TypeFieldCorrect_MovieCollectionType()` — Verifiziert, dass der Type-Feld "MovieCollection" und nicht "Movie" ist
- `Get_Movie_UserHasNoAccess_NotInResults()` — Verifiziert Zugriffskontrolle für Movies
- `Get_Movie_UserHasUnlockedMovieCollection_IncludedInResults()` — Verifiziert Freischaltungsmechanismus für Movies
- `Get_TVShowSeasonAndEpisode_UserHasUnlockedTVShow_IncludedInResults()` — Verifiziert Zugriffskontrolle für Seasons und Episodes
- `Get_AllTypes_UserNoAccess_ReturnEmptyList()` — Verifiziert, dass keine Inhalte ohne Zugriff zurückgegeben werden

**Kritik zu bestehenden Tests:** Alle Tests verwenden exakte oder passende Groß-/Kleinschreibung bei der Suche. Tests mit abweichender Groß-/Kleinschreibung (z.B. "breaking" vs. "Breaking Point") fehlen und würden den case-sensitive-Bug aufdecken.

### `ItemsControllerAccessTests`
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerAccessTests.cs`

Tests für Zugriffskontrolle auf Detail-Endpunkte (nicht der Such-Endpunkt):

- `Get_MovieCollection_Without_Access_Returns_Unauthorized()` — Verifiziert 401 ohne Zugriff
- `Get_MovieCollection_Unlocked_Returns_Ok()` — Verifiziert erfolgreicher Abruf mit Freischaltung
- `Get_TVShow_Without_Access_Returns_Unauthorized()` — Verifiziert 401 ohne Zugriff
- `Get_TVShow_Unlocked_Returns_Ok()` — Verifiziert erfolgreicher Abruf mit Freischaltung
- `Get_TVShowEpisode_Without_Access_Returns_Unauthorized()` — Verifiziert 401 ohne Zugriff
- `Get_TVShowEpisode_Unlocked_Returns_Ok()` — Verifiziert erfolgreicher Abruf mit Freischaltung
- `Get_Movie_Without_Access_Returns_Unauthorized()` — Verifiziert 401 ohne Zugriff
- `Get_Movie_From_Unlocked_Collection_Returns_Ok()` — Verifiziert erfolgreicher Abruf mit Collection-Freischaltung

**Relevanz zur Anforderung:** Diese Tests sind nicht direkt relevant; sie testen den Detail-Endpunkt (z.B. `/api/items/movie/123`), nicht die Such-/Listen-Endpunkt (GET `/api/items`).

### `MediaSearchSelectorTests`
Datei: `VideoWebPlayer.Tests/Components/MediaSearchSelectorTests.cs`

Tests für die Razor-Komponente `MediaSearchSelector`:

- `SearchTermInput_DebounceWorks_RespectsDelay()` — Verifiziert, dass Suche mit Debounce verzögert wird (400ms)
- `Search_NoResults_ShowsEmptyMessage()` — Verifiziert "Keine Ergebnisse gefunden"-Nachricht
- `Search_HttpRequestFails_LogsAndShowsDistinctErrorState()` — Verifiziert Fehlerbehandlung
- `Dispose_CancelsPendingDebouncedSearch_WithoutThrowing()` — Verifiziert Cleanup bei Disposal

**Relevanz zur Anforderung:** Diese Tests verwenden die `RequestItemsAsync()`-Methode des Clients, aber testen nicht den case-sensitive-Fehler oder den Opt-in-Parameter.

## Hilfsmethoden

### `ItemsControllerTestFactory`
Datei: `VideoWebPlayer.Tests/Helpers/ItemsControllerTestFactory.cs`

- `CreateAsync(connectionString, userName, logger?, cancellationToken)` — Erstellt einen neu initialisierten `ItemsController` mit:
  - Shared-cache In-Memory SQLite Datenbank
  - Authentifizierten `ApplicationUser`
  - Initialized `ApplicationDbContext`
  - Alle erforderlichen Services (EventManager, UnlockedMediaService, RecentEntryService, etc.)

Diese Hilfsmethode wird von `ItemsControllerTests_Search` und `ItemsControllerAccessTests` verwendet.

### Hilfsmethoden in `ItemsControllerTests_Search`

- `GrantSourceAccessAsync(db, user, source)` — Gewährt einem Benutzer Zugriff auf eine Medienquelle
- `CreateMovieAsync(db, source, name)` — Erstellt einen Test-Movie in einer Quelle
- `CreateMovieInCollectionAsync(db, source, collection, name)` — Erstellt einen Test-Movie in einer Collection
- `CreateTVShowAsync(db, source, name)` — Erstellt eine Test-TV-Serie
- `CreateSeasonAsync(db, source, show, name)` — Erstellt eine Test-Staffel
- `CreateEpisodeAsync(db, source, season, name)` — Erstellt eine Test-Episode

Diese Hilfsmethoden werden verwendet, um Testdaten zu erstellen.
