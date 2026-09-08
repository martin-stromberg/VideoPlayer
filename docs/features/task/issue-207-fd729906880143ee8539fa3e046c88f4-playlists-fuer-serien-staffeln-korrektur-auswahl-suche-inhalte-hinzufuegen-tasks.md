# Tasks: Namenssuche für Playlist-Einträge statt Roher Medien-IDs

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Backend – Schnittstellen | `IUnlockedMediaService`: Methode `GetUnlockedMovieIdsForUserAsync(string userId)` hinzufügen | Offen | — |
| 2 | Backend – Schnittstellen | `IUnlockedMediaService`: Methode `GetUnlockedTVShowSeasonIdsForUserAsync(string userId)` hinzufügen | Offen | — |
| 3 | Backend – Schnittstellen | `IUnlockedMediaService`: Methode `GetUnlockedTVShowEpisodeIdsForUserAsync(string userId)` hinzufügen | Offen | — |
| 4 | Backend – Service | `UnlockedMediaService`: Implementierung von `GetUnlockedMovieIdsForUserAsync` | Offen | Unit Test (neue Tests) |
| 5 | Backend – Service | `UnlockedMediaService`: Implementierung von `GetUnlockedTVShowSeasonIdsForUserAsync` | Offen | Unit Test (neue Tests) |
| 6 | Backend – Service | `UnlockedMediaService`: Implementierung von `GetUnlockedTVShowEpisodeIdsForUserAsync` | Offen | Unit Test (neue Tests) |
| 7 | Backend – Controller | `ItemsController.Get`: Bugfix — Type für MovieCollections korrekt setzen ("MovieCollection" statt "Movie") | Offen | Unit Test (Bugfix-Test) |
| 8 | Backend – Controller | `ItemsController.Get`: Query für Movies hinzufügen (Name-Filter, MediaSource-Filter, Genre-Filter) | Offen | Unit Test (neue Tests) |
| 9 | Backend – Controller | `ItemsController.Get`: Query für TVShowSeasons hinzufügen | Offen | Unit Test (neue Tests) |
| 10 | Backend – Controller | `ItemsController.Get`: Query für TVShowEpisodes hinzufügen | Offen | Unit Test (neue Tests) |
| 11 | Backend – Controller | `ItemsController.Get`: Zugriffskontrolle für Movies integrieren (UnlockedMediaService-Methode nutzen) | Offen | Unit Test (neue Tests) |
| 12 | Backend – Controller | `ItemsController.Get`: Zugriffskontrolle für TVShowSeasons integrieren | Offen | Unit Test (neue Tests) |
| 13 | Backend – Controller | `ItemsController.Get`: Zugriffskontrolle für TVShowEpisodes integrieren | Offen | Unit Test (neue Tests) |
| 14 | Backend – Client | `VideoWebPlayerClient`: Optional: Methode `RequestItemsAsync(string search, int page, int size, CancellationToken)` hinzufügen (Wrapper um ItemsController.Get) | Offen | — |
| 15 | Frontend – Komponente | `MediaSearchSelector.razor`: Neu anlegen mit Eingabefeld, Debounce-Logik, HTTP-Call zu ItemsController.Get | Offen | Unit Test (Komponenten-Tests) |
| 16 | Frontend – Komponente | `MediaSearchSelector.razor`: Ergebnis-Rendering (MediaBox.razor für Suchergebnisse) | Offen | Unit Test (Komponenten-Tests) |
| 17 | Frontend – Komponente | `MediaSearchSelector.razor`: EventCallback `OnMediaSelected` (string mediaType, long mediaId) implementieren | Offen | Unit Test (Komponenten-Tests) |
| 18 | Frontend – Komponente | `MediaSearchSelector.razor`: Lade-Indikator und Fehlerbehandlung | Offen | Unit Test (Komponenten-Tests) |
| 19 | Frontend – Komponente | `PlaylistEntriesList.razor`: Ersetze Dropdown + Number-Input durch `<MediaSearchSelector />` (Zeilen 22–30) | Offen | E2E Test (betroffene Tests angepasst) |
| 20 | Frontend – Komponente | `PlaylistEntriesList.razor`: Neue Methode `OnMediaSelectedAsync(string mediaType, long mediaId)` hinzufügen | Offen | Unit Test (Komponenten-Tests) |
| 21 | Unit Tests | Tests für `UnlockedMediaService.GetUnlockedMovieIdsForUserAsync` | Offen | Test bestanden |
| 22 | Unit Tests | Tests für `UnlockedMediaService.GetUnlockedTVShowSeasonIdsForUserAsync` | Offen | Test bestanden |
| 23 | Unit Tests | Tests für `UnlockedMediaService.GetUnlockedTVShowEpisodeIdsForUserAsync` | Offen | Test bestanden |
| 24 | Unit Tests | Tests für `ItemsController.Get` – Movie-Suche | Offen | Test bestanden |
| 25 | Unit Tests | Tests für `ItemsController.Get` – TVShowSeason-Suche | Offen | Test bestanden |
| 26 | Unit Tests | Tests für `ItemsController.Get` – TVShowEpisode-Suche | Offen | Test bestanden |
| 27 | Unit Tests | Tests für `ItemsController.Get` – Type-Feld Bugfix (MovieCollection korrekt gesetzt) | Offen | Test bestanden |
| 28 | Unit Tests | Tests für `ItemsController.Get` – Zugriffskontrolle für alle 5 Typen | Offen | Test bestanden |
| 29 | Unit Tests | Tests für `MediaSearchSelector` – Debounce-Logik | Offen | Test bestanden |
| 30 | Unit Tests | Tests für `MediaSearchSelector` – EventCallback-Aufruf | Offen | Test bestanden |
| 31 | Unit Tests | Tests für `MediaSearchSelector` – HTTP-Call mit korrekter URL | Offen | Test bestanden |
| 32 | Unit Tests | Tests für `PlaylistEntriesList.OnMediaSelectedAsync` → AddEntryAsync-Aufruf | Offen | Test bestanden |
| 33 | E2E Tests | E2E: Film suchen, Auswahl treffen, zu Playlist hinzufügen | Offen | E2E Test bestanden |
| 34 | E2E Tests | E2E: Serie suchen und hinzufügen (mit Kaskaden-Auflösung) | Offen | E2E Test bestanden |
| 35 | E2E Tests | E2E: Staffel suchen und hinzufügen (mit Episoden-Cascade) | Offen | E2E Test bestanden |
| 36 | E2E Tests | E2E: Episode auswählen (kein Cascade) | Offen | E2E Test bestanden |
| 37 | E2E Tests | E2E: Filmsammlung suchen und hinzufügen | Offen | E2E Test bestanden |
| 38 | E2E Tests | E2E: Benutzer ohne Zugriff — Medium nicht in Suchergebnissen | Offen | E2E Test bestanden |
| 39 | E2E Tests | E2E: Leere Suchergebnisse — Nachricht angezeigt | Offen | E2E Test bestanden |
| 40 | E2E Tests | Anpassung betroffener E2E Test: `PlaylistDetailE2ETests` — Alter Dropdown+Input-Flow durch MediaSearchSelector-Flow ersetzen | Offen | E2E Test bestanden |
| 41 | E2E Tests | Anpassung betroffener E2E Test: `PlaylistEntriesE2ETests` — Alter Dropdown+Input-Flow durch MediaSearchSelector-Flow ersetzen | Offen | E2E Test bestanden |
| 42 | E2E Tests | Prüfung: `PlaylistReorderE2ETests` — Betroffenheit durch UI-Änderung prüfen | Offen | — |
| 43 | Dokumentation | `docs/help/playlists.md`: Dokumentation der neuen Namenssuche-Oberfläche | Offen | — |
| 44 | Dokumentation | `docs/help/playlists-api.md`: Dokumentation — ItemsController.Get unterstützt nun alle 5 Medientypen | Offen | — |
| 45 | Dokumentation | `README.md`: Optional — Erwähnung der neuen Suche-Funktionalität (falls relevent) | Offen | — |
