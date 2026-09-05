# Tasks: Playlists – Inhalte hinzufügen und entfernen (Schritt 2)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | MediaType-Konstanten definieren (Klasse `MediaTypeConstants` oder Enum) | Offen | — |
| 2 | Datenmodell | `PlaylistEntry`-Entity erstellen mit allen Eigenschaften | Offen | — |
| 3 | Datenmodell | `PlaylistEntry`-Konfiguration (EF Core) erstellen und Composite Unique Constraint definieren | Offen | — |
| 4 | Datenmodell | `Playlist`-Entity um Navigation `PlaylistEntries` erweitern | Offen | — |
| 5 | Datenmodell | EF Core Migration `AddPlaylistEntriesTable` erstellen | Offen | — |
| 6 | DTOs | `DtoPlaylistEntry` erstellen | Offen | — |
| 7 | DTOs | `DtoAddMediaToPlaylistRequest` erstellen | Offen | — |
| 8 | DTOs | `DtoRemoveMediaFromPlaylistRequest` erstellen | Offen | — |
| 9 | Abhängigkeiten | `IMediaService`-Methoden recherchieren und dokumentieren (oder Stubs erstellen) | Offen | — |
| 10 | Service | `PlaylistService.ValidateMediaType()` Hilfsmethode implementieren | Offen | Unit-Test |
| 11 | Service | `PlaylistService.CheckMediaExists()` Hilfsmethode implementieren | Offen | Unit-Test |
| 12 | Service | `PlaylistService.GetCascadeMediaIds()` Hilfsmethode implementieren | Offen | Unit-Test |
| 13 | Service | `PlaylistService.ToDto(playlistEntry, mediaTitle, parentMediaTitle)` Hilfsmethode implementieren | Offen | Unit-Test |
| 14 | Service | `IPlaylistService.AddMediaToPlaylistAsync()` zur Interface definieren | Offen | — |
| 15 | Service | `PlaylistService.AddMediaToPlaylistAsync()` implementieren (mit Berechtigungsprüfung, Validierung, Cascade, Duplikatsprüfung) | Offen | Unit-Tests |
| 16 | Service | `IPlaylistService.RemoveMediaFromPlaylistAsync()` zur Interface definieren | Offen | — |
| 17 | Service | `PlaylistService.RemoveMediaFromPlaylistAsync()` implementieren | Offen | Unit-Tests |
| 18 | Service | `IPlaylistService.GetPlaylistEntriesAsync()` zur Interface definieren | Offen | — |
| 19 | Service | `PlaylistService.GetPlaylistEntriesAsync()` implementieren | Offen | Unit-Tests |
| 20 | Konfiguration | `PlaylistSettings` um Eigenschaft `MaxPlaylistItemCount` erweitern | Offen | — |
| 21 | Controller | `PlaylistsController.AddMediaToPlaylistAsync()` Endpoint implementieren (POST `/api/playlists/{id}/entries`) | Offen | Integration-Test |
| 22 | Controller | `PlaylistsController.RemoveMediaFromPlaylistAsync()` Endpoint implementieren (DELETE `/api/playlists/{id}/entries/{mediaType}/{mediaId}`) | Offen | Integration-Test |
| 23 | Controller | `PlaylistsController.GetPlaylistEntriesAsync()` Endpoint implementieren (GET `/api/playlists/{id}/entries`) | Offen | Integration-Test |
| 24 | UI | `PlaylistDetail.razor` erweitern: GET-Aufruf für Einträge laden implementieren | Offen | E2E-Test |
| 25 | UI | `PlaylistDetail.razor` erweitern: Unsortierte Liste der Einträge anzeigen | Offen | E2E-Test |
| 26 | UI | `PlaylistDetail.razor` erweitern: Delete-Button für jeden Eintrag hinzufügen | Offen | E2E-Test |
| 27 | UI | `PlaylistDetail.razor` erweitern: Dropdown/Auswahl für Medientyp hinzufügen | Offen | E2E-Test |
| 28 | UI | `PlaylistDetail.razor` erweitern: Such-/Auswahlfeld für Medieninhalt hinzufügen | Offen | E2E-Test |
| 29 | UI | `PlaylistDetail.razor` erweitern: Button zum Hinzufügen von Inhalten hinzufügen | Offen | E2E-Test |
| 30 | UI | `PlaylistDetail.razor` erweitern: Toast/Alert-Feedback für Duplikate implementieren | Offen | E2E-Test |
| 31 | Tests | Testklasse `PlaylistServiceTests_AddMedia` erstellen mit Test-Methoden | Offen | — |
| 32 | Tests | Test `AddMedia_ValidMovie_Success` implementieren | Offen | Grüner Unit-Test |
| 33 | Tests | Test `AddMedia_Duplicate_Returns409` implementieren | Offen | Grüner Unit-Test |
| 34 | Tests | Test `AddMedia_InvalidMediaType_Throws400` implementieren | Offen | Grüner Unit-Test |
| 35 | Tests | Test `AddMedia_MediaNotFound_Throws404` implementieren | Offen | Grüner Unit-Test |
| 36 | Tests | Test `AddMedia_NotOwner_ThrowsForbidden` implementieren | Offen | Grüner Unit-Test |
| 37 | Tests | Test `AddMedia_TVShow_CascadesEpisodes` implementieren | Offen | Grüner Unit-Test |
| 38 | Tests | Test `AddMedia_TVShowSeason_CascadesEpisodes` implementieren | Offen | Grüner Unit-Test |
| 39 | Tests | Test `AddMedia_MovieCollection_CascadesMovies` implementieren | Offen | Grüner Unit-Test |
| 40 | Tests | Test `AddMedia_CascadeWithDuplicates_SkipsDuplicates` implementieren | Offen | Grüner Unit-Test |
| 41 | Tests | Testklasse `PlaylistServiceTests_RemoveMedia` erstellen | Offen | — |
| 42 | Tests | Test `RemoveMedia_ValidEntry_Success` implementieren | Offen | Grüner Unit-Test |
| 43 | Tests | Test `RemoveMedia_NotFound_Throws404` implementieren | Offen | Grüner Unit-Test |
| 44 | Tests | Test `RemoveMedia_NotOwner_ThrowsForbidden` implementieren | Offen | Grüner Unit-Test |
| 45 | Tests | Testklasse `PlaylistServiceTests_GetEntries` erstellen | Offen | — |
| 46 | Tests | Test `GetEntries_ReturnsAllEntries` implementieren | Offen | Grüner Unit-Test |
| 47 | Tests | Test `GetEntries_EmptyPlaylist_ReturnsEmpty` implementieren | Offen | Grüner Unit-Test |
| 48 | Tests | Test `GetEntries_NotOwner_ThrowsForbidden` implementieren | Offen | Grüner Unit-Test |
| 49 | Tests | Test `GetEntries_PlaylistNotFound_Throws404` implementieren | Offen | Grüner Unit-Test |
| 50 | Tests | Hilfsmethode `CreateTestPlaylistWithEntries()` zu `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 51 | Tests | Hilfsmethode `CreateTestMediaEntry()` zu `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 52 | Tests | Testklasse `PlaylistsControllerTests_Entries` erstellen (oder erweitern) | Offen | — |
| 53 | Tests | Integration-Test `POST_AddMedia_Success` implementieren | Offen | Grüner Integration-Test |
| 54 | Tests | Integration-Test `POST_AddMedia_Duplicate_Returns409` implementieren | Offen | Grüner Integration-Test |
| 55 | Tests | Integration-Test `POST_AddMedia_BadRequest_Returns400` implementieren | Offen | Grüner Integration-Test |
| 56 | Tests | Integration-Test `POST_AddMedia_NotFound_Returns404` implementieren | Offen | Grüner Integration-Test |
| 57 | Tests | Integration-Test `POST_AddMedia_Forbidden_Returns403` implementieren | Offen | Grüner Integration-Test |
| 58 | Tests | Integration-Test `DELETE_RemoveMedia_Success` implementieren | Offen | Grüner Integration-Test |
| 59 | Tests | Integration-Test `DELETE_RemoveMedia_NotFound_Returns404` implementieren | Offen | Grüner Integration-Test |
| 60 | Tests | Integration-Test `DELETE_RemoveMedia_Forbidden_Returns403` implementieren | Offen | Grüner Integration-Test |
| 61 | Tests | Integration-Test `GET_GetEntries_Success` implementieren | Offen | Grüner Integration-Test |
| 62 | Tests | Integration-Test `GET_GetEntries_EmptyArray` implementieren | Offen | Grüner Integration-Test |
| 63 | Tests | Integration-Test `GET_GetEntries_Forbidden_Returns403` implementieren | Offen | Grüner Integration-Test |
| 64 | E2E-Tests | E2E-Test `Happy_Path_AddMovie` implementieren (Film hinzufügen, in Liste prüfen) | Offen | Grüner E2E-Test |
| 65 | E2E-Tests | E2E-Test `Happy_Path_RemoveMovie` implementieren (Film entfernen, aus Liste verschwunden) | Offen | Grüner E2E-Test |
| 66 | E2E-Tests | E2E-Test `Duplicate_Feedback` implementieren (Duplikat-Versuch zeigt Fehlermeldung) | Offen | Grüner E2E-Test |
| 67 | E2E-Tests | E2E-Test `Cascade_AddTVShow` implementieren (Serie hinzufügen, alle Episoden in Liste) | Offen | Grüner E2E-Test |
| 68 | E2E-Tests | E2E-Test `Permission_Denied` implementieren (Nicht-Besitzer kann nicht hinzufügen) | Offen | Grüner E2E-Test |
| 69 | E2E-Tests | E2E-Test `Performance_LargeCascade` implementieren (große Serie, UI responsiv) | Offen | Grüner E2E-Test |
| 70 | E2E-Tests | Existierende `PlaylistDetailE2ETests` überprüfen und aktualisieren (falls neue UI-Elemente brechen) | Offen | Alle E2E-Tests grün |
| 71 | Dokumentation | XML-Kommentare für alle neuen Service-Methoden hinzufügen | Offen | — |
| 72 | Dokumentation | XML-Kommentare für alle neuen Controller-Methoden hinzufügen | Offen | — |
| 73 | Dokumentation | XML-Kommentare für alle neuen DTOs hinzufügen | Offen | — |
| 74 | Release-Notes | CHANGELOG-Eintrag für Schritt 2 hinzufügen | Offen | — |
| 75 | Code-Review | Code-Review vorbereiten und durchführen | Offen | Code-Review passed |

---

**Gesamtzahl Tasks:** 75  
**Stand:** 2026-09-05
