# Tasks: Weiterschauen mit Playlist-Bezug (Schritt 6)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Migrations | Migration `AddPlaylistIdToContinueWatchingEntry` erstellen (Spalte, FK, UC) | Offen | — |
| 2 | Datenmodell | `ContinueWatchingEntry.PlaylistId` (long?) Eigenschaft hinzufügen | Offen | — |
| 3 | Datenmodell | `ContinueWatchingEntry.Playlist` Navigation Eigenschaft hinzufügen | Offen | — |
| 4 | DTO | `ContinueWatchingDto.PlaylistId` (long?) Eigenschaft hinzufügen | Offen | — |
| 5 | DTO | `ContinueWatchingDto.PlaylistName` (string?) Eigenschaft hinzufügen | Offen | — |
| 6 | Service | `ContinueWatchingService.ReportProgressAsync()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 7 | Service | `ContinueWatchingService.ProcessBufferedEntryAsync()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 8 | Service | `ContinueWatchingService.RemoveExistingTVShowEntry()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 9 | Service | `ContinueWatchingService.RemoveExtsingMovieCollectionEntry()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 10 | Service | `ContinueWatchingService.HideAsync()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 11 | Service | `ContinueWatchingService.SkipAsync()` Signatur um `playlistId?` Parameter erweitern | Offen | — |
| 12 | Service | `ContinueWatchingService.RemoveExistingTVShowEntry()` Deduplizierungs-Logik PlaylistId-Filter implementieren | Offen | — |
| 13 | Service | `ContinueWatchingService.RemoveExtsingMovieCollectionEntry()` Deduplizierungs-Logik PlaylistId-Filter implementieren | Offen | — |
| 14 | Service | `ContinueWatchingService.GetListAsync()` LINQ-Query um LEFT JOIN mit Playlist-Tabelle erweitern | Offen | — |
| 15 | Service | `ContinueWatchingService.GetListAsync()` DTO-Befüllung mit PlaylistId und PlaylistName erweitern | Offen | — |
| 16 | Service | `ContinueWatchingService.ValidatePlaylistOwnershipAsync()` Methode neu implementieren | Offen | — |
| 17 | Service | `ContinueWatchingService.ReportProgressAsync()` Validierung für PlaylistId hinzufügen | Offen | — |
| 18 | Service | `ContinueWatchingService.ProcessBufferedEntryAsync()` Validierung für PlaylistId hinzufügen | Offen | — |
| 19 | API | `ProgressRequest` Record um `PlaylistId` (long?) Eigenschaft erweitern | Offen | — |
| 20 | API | `ContinueWatchingActionRequest` Record um `PlaylistId` (long?) Eigenschaft erweitern | Offen | — |
| 21 | API | `ContinueWatchingController.PostProgress()` um Übergabe von `PlaylistId` an Service erweitern | Offen | — |
| 22 | API | `ContinueWatchingController.PostHide()` um Übergabe von `PlaylistId` an Service erweitern | Offen | — |
| 23 | API | `ContinueWatchingController.PostSkip()` um Übergabe von `PlaylistId` an Service erweitern | Offen | — |
| 24 | API | Error Handling für Playlist-Validierungsfehler in Controller hinzufügen (400/403/404 Responses) | Offen | — |
| 25 | UI-Razor | `VideoPlayer.razor.OnAfterRenderAsync()` um Übergabe von `PlaylistContext?.PlaylistId` an `continueWatching.attach()` erweitern | Offen | — |
| 26 | UI-Razor | `VideoPlayer.razor` Property `currentPlaylistId` hinzufügen zum Speichern der aktuellen PlaylistId | Offen | — |
| 27 | JavaScript | `continueWatching.js.attach()` Signatur um `playlistId` Parameter erweitern | Offen | — |
| 28 | JavaScript | `continueWatching.js` POST-Payload um `playlistId` Feld erweitern | Offen | — |
| 29 | UI-Razor | `ContinueWatchingList.razor` Anzeige-Logik um Playlist-Badge/Info erweitern | Offen | — |
| 30 | UI-Razor | `ContinueWatchingList.razor` Klick-Handler prüft auf PlaylistId und startet Playlist-Rekonstruktion | Offen | — |
| 31 | Tests | Testklasse `ContinueWatchingServicePlaylistTests` mit Deduplizierungs-Tests erstellen | Offen | — |
| 32 | Tests | Test: `Playlist_CreateEntry_WithSamePlaylistId_UpdatesExisting` | Offen | — |
| 33 | Tests | Test: `Playlist_CreateEntry_WithDifferentPlaylistIds_AllowsBoth` | Offen | — |
| 34 | Tests | Test: `Playlist_CreateEntry_WithAndWithoutPlaylistId_Independent` | Offen | — |
| 35 | Tests | Test: `Playlist_ValidateOwnership_NonOwner_Throws` | Offen | — |
| 36 | Tests | Testklasse `ContinueWatchingServiceRemovalTests` mit Removal-Filter-Tests erstellen | Offen | — |
| 37 | Tests | Test: `Playlist_RemoveExistingTVShow_OnlyRemovesMatchingPlaylistId` | Offen | — |
| 38 | Tests | Test: `Playlist_RemoveExistingMovie_OnlyRemovesMatchingPlaylistId` | Offen | — |
| 39 | Tests | Testklasse `ContinueWatchingServiceMultipleEntriesTests` mit Multi-Varianten-Tests erstellen | Offen | — |
| 40 | Tests | Test: `MultipleEntries_SameVideoThreePlaylists_AllExist` | Offen | — |
| 41 | Tests | Test: `MultipleEntries_SameVideoWithAndWithoutPlaylist_Separate` | Offen | — |
| 42 | Tests | Test: `MultipleEntries_MarkWatched_DeletesAllVariants` | Offen | — |
| 43 | Tests | Testklasse `ContinueWatchingDtoTests` mit DTO-Befüllung-Tests erstellen | Offen | — |
| 44 | Tests | Test: `DTO_PlaylistIdAndName_PopulatedFromDb` | Offen | — |
| 45 | Tests | Test: `DTO_PlaylistIdNull_NameNull` | Offen | — |
| 46 | Tests | Hilfsmethode `CreateTestPlaylist(db, userId, name)` in TestBase hinzufügen | Offen | — |
| 47 | Tests | Hilfsmethode `CreateTestPlaylistEntry(db, playlistId, mediaId, mediaType)` in TestBase hinzufügen | Offen | — |
| 48 | E2E-Tests | E2E-Test: `E2E_Playlist_CreateAndReportProgress_EntryCreated` (Happy Path) | Offen | — |
| 49 | E2E-Tests | E2E-Test: `E2E_MultipleEntriesPlaylist_AllThreeVariantsVisible` (Mehrfaches Vorkommen) | Offen | — |
| 50 | E2E-Tests | E2E-Test: `E2E_MarkWatchedPlaylist_RemovesAllVariants` (Gesehen-Markierung) | Offen | — |
| 51 | E2E-Tests | E2E-Test: `E2E_ResumeFromPlaylistEntry_PlaylistContextRecovered` (Rekonstruktion) | Offen | — |
| 52 | E2E-Tests | E2E-Test: `E2E_HideWithPlaylistId_OnlyRemovesMatching` (Hide-Filter) | Offen | — |
| 53 | E2E-Tests | E2E-Test: `E2E_SkipWithPlaylistId_NextWithSamePlaylistId` (Skip-Filter) | Offen | — |
| 54 | E2E-Tests | E2E-Test: `E2E_PlaylistDeleted_EntryBecomesFree` (Fehlerfall: gelöschte Playlist) | Offen | — |
| 55 | Regression | Bestehende Tests `ContinueWatchingServiceGetNextEpisodeTests` auf Compatibility prüfen | Offen | — |
| 56 | Regression | Bestehende Tests `ContinueWatchingContextMenuActionTests` auf Compatibility prüfen | Offen | — |
| 57 | Regression | Bestehende Tests `ContinueWatchingServiceSignalRTests` auf Compatibility prüfen | Offen | — |
| 58 | Regression | Bestehende Tests `ContinueWatchingWatchedStatusTests` auf Compatibility prüfen und ggf. erweitern | Offen | — |
| 59 | Regression | Bestehende E2E-Tests `ContinueWatchingE2ETests` auf Compatibility prüfen | Offen | — |
