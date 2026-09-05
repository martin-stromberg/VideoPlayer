# Tasks: Duplikate und Normalisierung in Playlist-Inhalte

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `DtoPlaylistAddResult` Klasse anlegen (mit Properties: TopLevelEntry, AddedEntries[], SkippedDuplicateCount, Message) | Erledigt | Kein direkter Test (DTO-Struktur ist auf richtige Serialisierung geprüft via Integration-Tests) |
| 2 | Datenbank | Migration `NormalizePlaylistEntryMediaTypes` erstellen (SQL für case-insensitive MediaType-Normalisierung) | Erledigt | Kein direkter Test (Migration vorhanden, Ausführung via `dotnet ef database update`) |
| 3 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Rückgabetyp ändern: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_ValidMovie_Success |
| 4 | Logik | `PlaylistService.AddMediaToPlaylistAsync` MediaType-Normalisierung implementieren: `normalizedMediaType = parsedMediaType.ToString()` nach ParseMediaType-Aufruf | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection |
| 5 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Top-Level-Duplikat-Check überarbeiten: Exception entfernen, Zähler erhöhen | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_TopLevelDuplicate_SkipsAndReturnsCount |
| 6 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Cascade-Duplikat-Zähler implementieren: skippedCount für jedes übersprungene Cascade-Item erhöhen | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_CascadeWithDuplicates_SkipsDuplicates |
| 7 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Speicherung überarbeiten: Normalisierte MediaType verwenden (nicht roher Request-String) | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection |
| 8 | Logik | `PlaylistService.AddMediaToPlaylistAsync` Response-DTO konstruieren: Message basierend auf Counts, AddedEntries[] bauen, TopLevelEntry optional setzen | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_TopLevelDuplicate_SkipsAndReturnsCount, AddMedia_PartialDuplicates_AddedNewAndSkipped, AddMedia_AllDuplicates_ReturnsZeroAddedCount |
| 9 | Interface | `IPlaylistService.AddMediaToPlaylistAsync` Rückgabetyp-Signatur aktualisieren: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` | Erledigt | Kompiliert (keine Compiler-Fehler) |
| 10 | API | `PlaylistsController.AddMediaToPlaylist` Response-Mapping anpassen: `DtoPlaylistAddResult` direkt zurückgeben | Erledigt | Kein direkter Test (würde durch Integration-Test abgedeckt) |
| 11 | API | `PlaylistsController.AddMediaToPlaylist` Exception-Handling vereinfachen: HTTP 409 Catch-Block für Duplikate nicht mehr nötig | Erledigt | Kein direkter Test (würde durch Integration-Test abgedeckt) |
| 12 | API | `VideoWebPlayerClient.AddMediaToPlaylistAsync` Rückgabetyp aktualisieren: `Task<DtoPlaylistEntry>` → `Task<DtoPlaylistAddResult>` und generischen Typ in HttpPostAsync ändern | Erledigt | Kein direkter Test (würde durch Integration-Test abgedeckt) |
| 13 | UI | `PlaylistDetail.razor.AddEntryAsync` HTTP 409 Catch-Block entfernen (wird nicht mehr geworfen) | Erledigt | Kein direkter Test (würde durch Component/E2E-Test abgedeckt) |
| 14 | UI | `PlaylistDetail.razor.AddEntryAsync` Response-Verarbeitung erweitern: `result.Message` aus DtoPlaylistAddResult anzeigen | Erledigt | Kein direkter Test (würde durch Component/E2E-Test abgedeckt) |
| 15 | UI | `PlaylistDetail.razor` Meldungs-CSS-Klasse anpassen: `alert-success` oder `alert-info` verwenden statt `alert-warning` | Erledigt | Kein direkter Test (würde durch Component-Test abgedeckt) |
| 16 | Tests | Test `AddMedia_Duplicate_ThrowsInvalidOperationException` umstrukturieren zu `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`: Verifies DtoPlaylistAddResult mit SkippedCount | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_TopLevelDuplicate_SkipsAndReturnsCount (grün) |
| 17 | Tests | Test `AddMedia_CascadeWithDuplicates_SkipsDuplicates` anpassen: Assertions für neue DtoPlaylistAddResult-Struktur mit SkippedCount-Zähler | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_CascadeWithDuplicates_SkipsDuplicates (grün) |
| 18 | Tests | Neuer Test `AddMedia_PartialDuplicates_AddedNewAndSkipped`: Serie mit teilweisen Cascade-Duplikaten | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_PartialDuplicates_AddedNewAndSkipped (grün) |
| 19 | Tests | Neuer Test `AddMedia_AllDuplicates_ReturnsZeroAddedCount`: Alle Titel bereits vorhanden | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_AllDuplicates_ReturnsZeroAddedCount (grün) |
| 20 | Tests | Neuer Test `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection`: "Movie" vs "movie" Duplikat-Erkennung | Erledigt | PlaylistServiceTests_AddMedia.AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection (grün) |
| 21 | Tests | Integrations-Test: Serie zweimal hinzufügen (E2E Cascade-Duplikat-Handling) | Erledigt | PlaylistsControllerTests_Entries.AddMediaToPlaylist_TVShowAddedTwice_SecondCallSkipsAllAsDuplicates |
| 22 | Tests | Integrations-Test: Serie entfernen, Serie erneut hinzufügen (Episode wird wieder hinzugefügt) | Erledigt | PlaylistsControllerTests_Entries.AddMediaToPlaylist_AfterRemovingEpisode_ReAddingShowRestoresEpisode |
| 23 | Tests | Integrations-Test: MediaType-Normalisierung über API ("Movie" vs "movie" → Duplikat erkannt) | Erledigt | PlaylistsControllerTests_Entries.AddMediaToPlaylist_DifferentCasingSameMedia_SecondCallDetectsDuplicate |
| 24 | Tests | Component-Test: `PlaylistDetail.razor.AddEntryAsync` zeigt Duplikat-Message korrekt an | Erledigt | PlaylistEntriesE2ETests.AddMovie_Duplicate_ShowsSuccessMessage |
| 25 | Tests | Component-Test: `PlaylistDetail.razor` nutzt `alert-success` für Duplikat-Message (nicht `alert-danger`) | Erledigt | PlaylistEntriesE2ETests.AddMovie_Duplicate_ShowsSuccessMessage (Zeile 81: prüft alert-success Klasse) |
| 26 | Datenbank | Datenbankmigrationen anwenden: `dotnet ef database update` | Offen | — |
| 27 | Dokumentation | `docs/help/playlists-api.md` aktualisieren: Neue Response-Struktur dokumentieren, Beispiele für HTTP 200 bei Duplikaten | Offen | — |
| 28 | Dokumentation | `docs/help/playlists-business-rules.md` aktualisieren: Vereinheitlichte Duplikat-Regel und MediaType-Normalisierung dokumentieren | Offen | — |
| 29 | Dokumentation | `docs/API.md` (falls vorhanden) aktualisieren: HTTP 409 nicht mehr möglich für Duplikate | Offen | — |

---

## Zusammenfassung

- **Erledigt:** 25 von 29 Aufgaben (86%)
- **Offen:** 4 Aufgaben (Dokumentation)
- **Blockiert:** Keine

### Hinweis: Tests bereits vorhanden

Die Aufgaben 21–25 (Integrationstests und Component-Tests) wurden bereits implementiert und sind erledigt:
- **Aufgabe 21–23:** Tests in `PlaylistsControllerTests_Entries.cs` vorhanden (Integrations-Ebene)
- **Aufgabe 24–25:** Tests in `PlaylistEntriesE2ETests.cs` vorhanden (Browser-E2E mit Playwright)

Ein vorheriger Review-Durchlauf (review.1.md) hatte diese Tests übersehen, da sie unter anderen Namen vorhanden sind. Die vorliegende Überprüfung hat diese Tests durch sorgfältige Suche nach inhaltlich äquivalenten Implementierungen gefunden.

### Kritischer Pfad: Dokumentation

Die Dokumentations-Updates (Aufgaben 27–29) sind notwendig vor Release, da sie ein **Breaking Change** für externe API-Consumer darstellen:
- HTTP 409 Conflict wird nicht mehr zurückgegeben
- Response-Struktur hat sich geändert (neue `DtoPlaylistAddResult` statt `DtoPlaylistEntry`)
- Neue `Message`-Feld mit Duplikat-Info muss dokumentiert werden

