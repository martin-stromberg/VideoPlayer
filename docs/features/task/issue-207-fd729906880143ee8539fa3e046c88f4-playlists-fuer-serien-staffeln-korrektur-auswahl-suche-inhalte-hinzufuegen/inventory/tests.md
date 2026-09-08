# Tests für Unicode-korrekte Namenssuche

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-08 13:41 UTC
- **Branch und Commit-ID:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-auswahl-suche-inhalte-hinzufuegen, Commit e3238cb (fix: git-hooks korrigieren)
- **Uncommittete Änderungen im getesteten Stand:** Dokumentation für die neue Anforderung hinzugefügt (requirement.md, todo.md)
- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET 10.0.11
  - xunit.v3 Version 3.2.2
  - Microsoft.EntityFrameworkCore.Sqlite Version 10.0.10
  - SQLitePCLRaw.lib.e_sqlite3 Version 3.53.3
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~ItemsControllerTests_Search"`
  - Arbeitsverzeichnis: D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4

## Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen |
|------|--------|------------------|-----------|-------------|----------------|--------------|
| 1 | dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~ItemsControllerTests_Search" | D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4 | 0 | 16 | 0 | 0 |

## Nachgewiesene bestehende Testfehler

Keine. Alle 16 Testfälle in `ItemsControllerTests_Search` werden erfolgreich bestanden.

## Testlücken und Ausführungsprobleme

- **Keine Test-Fälle mit Unicode-Umlauten:** Die Test-Suite enthält keine Testfälle mit deutschen Umlauten (Ä, Ö, Ü, ß).
  - Fehlende Testfälle für zentrale Anforderungs-Szenarien:
    - Suche nach "Über" für Titel "Über den Wolken"
    - Suche nach "ÜBER" (Großbuchstaben mit Umlaut)
    - Suche nach "ärzte" für Titel "Ärzte"
    - Suche nach "mörder" für Titel "Der Mörder ist da"
    - Suche nach "eszett" für Titel "Das Eszett ß Zeichen"
- **ASCII-only Tests:** Alle bestehenden Tests verwenden nur ASCII-Titeln und funktionieren mit der aktuellen `.ToLower()`-Implementierung einwandfrei, da SQLite für ASCII-Zeichen keine Unicode-Faltungsprobleme hat.
- **Keine Tests für das zentrale Problem:** Keine Tests vorhanden für das Kern-Szenario der Anforderung: dass `lower()` in SQLite ohne ICU-Erweiterung deutsche Umlaute nicht korrekt verarbeitet.

## Testklassen

### `ItemsControllerTests_Search`
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerTests_Search.cs`

| Testmethode | Was wird getestet? |
|--------|-------------------|
| `Get_SearchMovies_ReturnsMatchingMovies()` | Suche nach Movie mit Substring-Match (ASCII) |
| `Get_SearchTVShowSeasons_ReturnsMatchingSeasons()` | Suche nach TVShowSeason mit Substring-Match (ASCII) |
| `Get_SearchTVShowEpisodes_ReturnsMatchingEpisodes()` | Suche nach TVShowEpisode mit Substring-Match (ASCII) |
| `Get_TypeFieldCorrect_MovieCollectionType()` | Validierung des Type-Feldes "MovieCollection" |
| `Get_Movie_UserHasNoAccess_NotInResults()` | Zugriffskontrolle - Benutzer ohne Berechtigung sieht kein Ergebnis |
| `Get_Movie_UserHasUnlockedMovieCollection_IncludedInResults()` | Zugriffskontrolle - Benutzer mit Freischaltung sieht Movie |
| `Get_TVShowSeasonAndEpisode_UserHasUnlockedTVShow_IncludedInResults()` | Zugriffskontrolle - Freischaltung gilt für TVShow-Inhalte |
| `Get_AllTypes_UserNoAccess_ReturnEmptyList()` | Zugriffskontrolle - Keine Ergebnisse ohne Berechtigung |
| `Get_SearchMovies_CaseInsensitive_LowerCase()` | Case-Insensitive Suche mit Kleinbuchstaben (ASCII) |
| `Get_SearchMovies_CaseInsensitive_UpperCase()` | Case-Insensitive Suche mit Großbuchstaben (ASCII) |
| `Get_SearchTVShowSeasons_CaseInsensitive()` | Case-Insensitive Suche für Seasons (ASCII) |
| `Get_SearchTVShowEpisodes_CaseInsensitive()` | Case-Insensitive Suche für Episodes (ASCII) |
| `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()` | Filter auf MovieCollection und TVShow nur |
| `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` | Filter auf alle 5 Medientypen |
| `Get_PlaylistSearch_WithOptIn_Returns5Types()` | Playlist-Such-Modus mit all 5 Typen |
| `Get_PlaylistSearch_WithoutOptIn_Returns2Types()` | Playlist-Such-Modus mit 2 eingeschränkten Typen |

## Hilfsmethoden

### `ItemsControllerTestFactory`
Datei: `VideoWebPlayer.Tests/Helpers/ItemsControllerTestFactory.cs`

- `CreateAsync()` — Erstellt einen ItemsController mit Testdatenbank und authentifiziertem Benutzer

### Helper in `ItemsControllerTests_Search`

| Hilfsmethode | Was wird bereitgestellt? |
|---------|------------------------|
| `CreateControllerAsync()` | Erstellt einen ItemsController mit In-Memory SQLite-DB und Testbenutzer |
| `CreateMovieAsync()` | Erstellt ein Movie-Testobjekt in der Datenbank |
| `CreateMovieInCollectionAsync()` | Erstellt ein Movie in einer MovieCollection |
| `CreateTVShowAsync()` | Erstellt ein TVShow-Testobjekt in der Datenbank |
| `CreateSeasonAsync()` | Erstellt ein TVShowSeason-Testobjekt in der Datenbank |
| `CreateEpisodeAsync()` | Erstellt ein TVShowEpisode-Testobjekt in der Datenbank |
| `SeedAllFiveMediaTypesAsync()` | Erstellt einen Beispiel-Datensatz mit allen 5 Medientypen |
| `GrantSourceAccessAsync()` | Erteilt einem Benutzer Zugriff auf eine MediaSource |
| `GetOkValue()` | Extrahiert den List<MediaEntryDto>-Wert aus einem OK-ActionResult |
