# Tasks: Korrektur der Medienauswahl-Suche für Playlists

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Vorbereitung | Case-insensitive Suche: Technik mit SQLite testen (ToLower vs. Like) und dokumentieren | Offen | — |
| 2 | Datenmodell | `MediaEntryFilter` Record: Feld `IncludeIndividualMediaTypes` (bool, Default: false) hinzufügen | Offen | — |
| 3 | Server-Logik | `ItemsController.Get()`: Bedingte Aufruflogik für neue 3 Medientypen basierend auf `IncludeIndividualMediaTypes` implementieren | Offen | Unit-Test |
| 4 | Server-Logik | `ItemsController.GetMovieCollectionEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Offen | Unit-Test |
| 5 | Server-Logik | `ItemsController.GetTVShowEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Offen | Unit-Test |
| 6 | Server-Logik | `ItemsController.GetMovieEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Offen | Unit-Test |
| 7 | Server-Logik | `ItemsController.GetSeasonEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Offen | Unit-Test |
| 8 | Server-Logik | `ItemsController.GetEpisodeEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Offen | Unit-Test |
| 9 | Client | `VideoWebPlayerClient.RequestItemsCoreAsync()`: Optionaler Parameter `bool includeIndividualMediaTypes = false` hinzufügen und an Query-String übergeben | Offen | — |
| 10 | Client | `VideoWebPlayerClient.RequestSourceItems()`: Ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: false` auf | Offen | — |
| 11 | Client | `VideoWebPlayerClient.RequestItemsAsync()`: Ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: true` auf | Offen | — |
| 12 | Tests | Unit-Test: `Get_SearchMovies_CaseInsensitive_LowerCase()` — Suche nach „breaking" findet „Breaking Bad" | Offen | Grün |
| 13 | Tests | Unit-Test: `Get_SearchMovies_CaseInsensitive_UpperCase()` — Suche nach „BREAKING" findet „Breaking Bad" | Offen | Grün |
| 14 | Tests | Unit-Test: `Get_SearchTVShowSeasons_CaseInsensitive()` — Suche nach „staffel" findet „Staffel Eins" | Offen | Grün |
| 15 | Tests | Unit-Test: `Get_SearchTVShowEpisodes_CaseInsensitive()` — Suche nach „pilotfolge" findet „Pilotfolge" | Offen | Grün |
| 16 | Tests | Unit-Test: `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()` — Quellen-Browsing ohne Opt-in gibt nur 2 Typen | Offen | Grün |
| 17 | Tests | Unit-Test: `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` — Quellen-Browsing mit Opt-in gibt 5 Typen | Offen | Grün |
| 18 | Tests | Unit-Test: `Get_PlaylistSearch_WithOptIn_Returns5Types()` — Playlist-Suche mit Opt-in gibt 5 Typen | Offen | Grün |
| 19 | Tests | Unit-Test: `Get_PlaylistSearch_WithoutOptIn_Returns2Types()` — Playlist-Suche ohne Opt-in gibt nur 2 Typen (Regression) | Offen | Grün |
| 20 | Tests | Regressions-Check: Alle 480 bestehenden Tests überprüfen (keine neuen Ausfälle durch case-insensitive Suche) | Offen | Grün |
| 21 | E2E-Tests | E2E-Test: `AddMedia_SearchCaseInsensitive_FindsMedia()` — Benutzer sucht case-insensitiv und findet Medium | Offen | Grün |
| 22 | E2E-Tests | E2E-Test: `AddMedia_Search_ReturnsAll5Types()` — Playlist-Suche liefert alle 5 Medientypen | Offen | Grün |
| 23 | E2E-Tests | E2E-Test (Regression): `BrowseSource_ShowsOnly2Types()` — Quellen-Browsing zeigt nur 2 Typen (falls E2E-Infrastruktur vorhanden) | Offen | Grün |
| 24 | Dokumentation | `docs/help/playlists-api.md`: Sektion „Medien-Suche" aktualisieren — case-insensitive Suche dokumentieren | Offen | — |
| 25 | Dokumentation | Controller XML-Kommentare: `ItemsController.Get()` und `IncludeIndividualMediaTypes` dokumentieren | Offen | — |

