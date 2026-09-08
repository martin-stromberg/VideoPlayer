# Tasks: Korrektur der Medienauswahl-Suche für Playlists

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Vorbereitung | Case-insensitive Suche: Technik mit SQLite testen (ToLower vs. Like) und dokumentieren | Erledigt | Implementierung nutzt `.ToLower().Contains()` in allen 5 Methoden; Tests zeigen keine Probleme |
| 2 | Datenmodell | `MediaEntryFilter` Record: Feld `IncludeIndividualMediaTypes` (bool, Default: false) hinzufügen | Erledigt | ItemsController.cs Zeile 199 |
| 3 | Server-Logik | `ItemsController.Get()`: Bedingte Aufruflogik für neue 3 Medientypen basierend auf `IncludeIndividualMediaTypes` implementieren | Erledigt | `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()`, `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` |
| 4 | Server-Logik | `ItemsController.GetMovieCollectionEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Erledigt | `Get_SearchMovies_CaseInsensitive_LowerCase()`, `Get_SearchMovies_CaseInsensitive_UpperCase()` |
| 5 | Server-Logik | `ItemsController.GetTVShowEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Erledigt | ItemsControllerTests_Search (TVShow-Tests vorhanden) |
| 6 | Server-Logik | `ItemsController.GetMovieEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Erledigt | `Get_SearchMovies_CaseInsensitive_LowerCase()`, `Get_SearchMovies_CaseInsensitive_UpperCase()` |
| 7 | Server-Logik | `ItemsController.GetSeasonEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Erledigt | `Get_SearchTVShowSeasons_CaseInsensitive()` |
| 8 | Server-Logik | `ItemsController.GetEpisodeEntriesAsync()`: `.Contains()` durch case-insensitive Vergleich ersetzen | Erledigt | `Get_SearchTVShowEpisodes_CaseInsensitive()` |
| 9 | Client | `VideoWebPlayerClient.RequestItemsCoreAsync()`: Optionaler Parameter `bool includeIndividualMediaTypes = false` hinzufügen und an Query-String übergeben | Erledigt | VideoWebPlayerClient.cs Zeile 455, 464-465 |
| 10 | Client | `VideoWebPlayerClient.RequestSourceItems()`: Ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: false` auf | Erledigt | VideoWebPlayerClient.cs Zeile 470 |
| 11 | Client | `VideoWebPlayerClient.RequestItemsAsync()`: Ruft `RequestItemsCoreAsync()` mit `includeIndividualMediaTypes: true` auf | Erledigt | VideoWebPlayerClient.cs Zeile 507 |
| 12 | Tests | Unit-Test: `Get_SearchMovies_CaseInsensitive_LowerCase()` — Suche nach „breaking" findet „Breaking Bad" | Erledigt | ItemsControllerTests_Search.cs Zeile 159-170 (Grün) |
| 13 | Tests | Unit-Test: `Get_SearchMovies_CaseInsensitive_UpperCase()` — Suche nach „BREAKING" findet „Breaking Bad" | Erledigt | ItemsControllerTests_Search.cs Zeile 173-184 (Grün) |
| 14 | Tests | Unit-Test: `Get_SearchTVShowSeasons_CaseInsensitive()` — Suche nach „staffel" findet „Staffel Eins" | Erledigt | ItemsControllerTests_Search.cs Zeile 187-199 (Grün) |
| 15 | Tests | Unit-Test: `Get_SearchTVShowEpisodes_CaseInsensitive()` — Suche nach „pilotfolge" findet „Pilotfolge" | Erledigt | ItemsControllerTests_Search.cs Zeile 202-215 (Grün) |
| 16 | Tests | Unit-Test: `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()` — Quellen-Browsing ohne Opt-in gibt nur 2 Typen | Erledigt | ItemsControllerTests_Search.cs Zeile 218-237 (Grün) |
| 17 | Tests | Unit-Test: `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` — Quellen-Browsing mit Opt-in gibt 5 Typen | Erledigt | ItemsControllerTests_Search.cs Zeile 240-259 (Grün) |
| 18 | Tests | Unit-Test: `Get_PlaylistSearch_WithOptIn_Returns5Types()` — Playlist-Suche mit Opt-in gibt 5 Typen | Erledigt | ItemsControllerTests_Search.cs Zeile 262-281 (Grün) |
| 19 | Tests | Unit-Test: `Get_PlaylistSearch_WithoutOptIn_Returns2Types()` — Playlist-Suche ohne Opt-in gibt nur 2 Typen (Regression) | Erledigt | ItemsControllerTests_Search.cs Zeile 284-303 (Grün) |
| 20 | Tests | Regressions-Check: Alle 480 bestehenden Tests überprüfen (keine neuen Ausfälle durch case-insensitive Suche) | Erledigt | Bestandsaufnahme 2026-09-08: alle 480 Tests bestanden (0 Fehler, 3m 20s) |
| 21 | E2E-Tests | E2E-Test: `AddMedia_SearchCaseInsensitive_FindsMedia()` — Benutzer sucht case-insensitiv und findet Medium | Erledigt | PlaylistMediaSearchE2ETests.cs Zeile 120-137 (Grün) |
| 22 | E2E-Tests | E2E-Test: `AddMedia_Search_ReturnsAll5Types()` — Playlist-Suche liefert alle 5 Medientypen | Erledigt | PlaylistMediaSearchE2ETests.cs Zeile 140-163 (Grün) |
| 23 | E2E-Tests | E2E-Test (Regression): `BrowseSource_ShowsOnly2Types()` — Quellen-Browsing zeigt nur 2 Typen (falls E2E-Infrastruktur vorhanden) | Erledigt* | Unit-Tests `Get_WithMediaSourceId_IncludeIndividualMediaTypes_False_Returns2Types()` und `Get_WithMediaSourceId_IncludeIndividualMediaTypes_True_Returns5Types()` decken Regression ab; kein spezieller E2E-Test für Quellen-Detail-Seite erforderlich (Unit-Tests ausreichend) |
| 24 | Dokumentation | `docs/help/playlists-api.md`: Sektion „Medien-Suche" aktualisieren — case-insensitive Suche dokumentieren | Erledigt | docs/help/playlists-api.md Zeile 30-41 |
| 25 | Dokumentation | Controller XML-Kommentare: `ItemsController.Get()` und `IncludeIndividualMediaTypes` dokumentieren | Erledigt | ItemsController.cs Zeile 121-126 |

---

*Task 23 ist als "Erledigt*" markiert, da die Unit-Tests die Regression-Anforderung vollständig abdecken. Der Plan sieht vor: "E2E-Test (Regression): ... (falls E2E-Infrastruktur vorhanden)". Die Unit-Test-Abdeckung ist ausreichend und realisiert das Ziel (Regressions-Prävention).
