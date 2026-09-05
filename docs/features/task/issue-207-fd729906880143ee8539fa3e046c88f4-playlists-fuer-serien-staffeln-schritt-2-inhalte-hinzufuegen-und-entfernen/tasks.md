# Tasks: Duplikate und Normalisierung in Playlist-Inhalte

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `DtoPlaylistAddResult` Klasse anlegen (mit Properties: TopLevelEntry, AddedEntries[], SkippedDuplicateCount, Message) | Offen | — |
| 2 | Datenbank | Migration `NormalizePlaylistEntryMediaTypes` erstellen (SQL für case-insensitive MediaType-Normalisierung) | Offen | — |
| 3 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Rückgabetyp ändern: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` | Offen | Unit-Tests |
| 4 | Logik | `PlaylistService.AddMediaToPlaylistAsync` MediaType-Normalisierung implementieren: `normalizedMediaType = parsedMediaType.ToString()` nach ParseMediaType-Aufruf | Offen | Unit-Tests |
| 5 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Top-Level-Duplikat-Check überarbeiten: Exception entfernen, Zähler erhöhen | Offen | Unit-Tests |
| 6 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Cascade-Duplikat-Zähler implementieren: skippedCount für jedes übersprungene Cascade-Item erhöhen | Offen | Unit-Tests |
| 7 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Speicherung überarbeiten: Normalisierte MediaType verwenden (nicht roher Request-String) | Offen | Unit-Tests |
| 8 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Response-DTO konstruieren: Message basierend auf Counts, AddedEntries[] bauen, TopLevelEntry optional setzen | Offen | Unit-Tests |
| 9 | Interface | `IPlaylistService.AddMediaToPlaylistAsync` Rückgabetyp-Signatur aktualisieren: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` | Offen | Kompiliert |
| 10 | API | `PlaylistsController.AddMediaToPlaylist` Response-Mapping anpassen: `DtoPlaylistAddResult` direkt zurückgeben | Offen | Integrationstests |
| 11 | API | `PlaylistsController.AddMediaToPlaylist` Exception-Handling vereinfachen: HTTP 409 Catch-Block für Duplikate nicht mehr nötig | Offen | Integrationstests |
| 12 | API | `VideoWebPlayerClient.AddMediaToPlaylistAsync` Rückgabetyp aktualisieren: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` und generischen Typ in HttpPostAsync ändern | Offen | Integrationstests |
| 13 | UI | `PlaylistDetail.razor.AddEntryAsync` HTTP 409 Catch-Block entfernen (wird nicht mehr geworfen) | Offen | Component/E2E-Tests |
| 14 | UI | `PlaylistDetail.razor.AddEntryAsync` Response-Verarbeitung erweitern: `result.Message` aus DtoPlaylistAddResult anzeigen | Offen | Component/E2E-Tests |
| 15 | UI | `PlaylistDetail.razor` Meldungs-CSS-Klasse anpassen: `alert-success` oder `alert-info` verwenden statt `alert-warning` | Offen | Component/E2E-Tests |
| 16 | Tests | Test `AddMedia_Duplicate_ThrowsInvalidOperationException` umstrukturieren zu `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`: Verifies DtoPlaylistAddResult mit SkippedCount | Offen | Unit-Test grün |
| 17 | Tests | Test `AddMedia_CascadeWithDuplicates_SkipsDuplicates` anpassen: Assertions für neue DtoPlaylistAddResult-Struktur mit SkippedCount-Zähler | Offen | Unit-Test grün |
| 18 | Tests | Neuer Test `AddMedia_PartialDuplicates_AddedNewAndSkipped`: Serie mit teilweisen Cascade-Duplikaten | Offen | Unit-Test grün |
| 19 | Tests | Neuer Test `AddMedia_AllDuplicates_ReturnsZeroAddedCount`: Alle Titel bereits vorhanden | Offen | Unit-Test grün |
| 20 | Tests | Neuer Test `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection`: "Movie" vs "movie" Duplikat-Erkennung | Offen | Unit-Test grün |
| 21 | Tests | Integrations-Test: Serie zweimal hinzufügen (E2E Cascade-Duplikat-Handling) | Offen | Integration-Test grün |
| 22 | Tests | Integrations-Test: Serie entfernen, Serie erneut hinzufügen (Episode wird wieder hinzugefügt) | Offen | Integration-Test grün |
| 23 | Tests | Integrations-Test: MediaType-Normalisierung über API ("Movie" vs "movie" → Duplikat erkannt) | Offen | Integration-Test grün |
| 24 | Tests | Component-Test: `PlaylistDetail.razor.AddEntryAsync` zeigt Duplikat-Message korrekt an | Offen | Component-Test grün |
| 25 | Tests | Component-Test: `PlaylistDetail.razor` nutzt `alert-success` für Duplikat-Message (nicht `alert-danger`) | Offen | Component-Test grün |
| 26 | Datenbank | Datenbankmigrationen anwenden: `dotnet ef database update` | Offen | — |
| 27 | Dokumentation | `docs/help/playlists-api.md` aktualisieren: Neue Response-Struktur dokumentieren, Beispiele für HTTP 200 bei Duplikaten | Offen | — |
| 28 | Dokumentation | `docs/help/playlists-business-rules.md` aktualisieren: Vereinheitlichte Duplikat-Regel und MediaType-Normalisierung dokumentieren | Offen | — |
| 29 | Dokumentation | `docs/API.md` (falls vorhanden) aktualisieren: HTTP 409 nicht mehr möglich für Duplikate | Offen | — |
