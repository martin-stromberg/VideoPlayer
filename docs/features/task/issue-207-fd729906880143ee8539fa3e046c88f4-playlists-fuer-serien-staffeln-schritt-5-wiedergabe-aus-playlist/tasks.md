# Tasks: Korrektionen Playlist-Wiedergabe (Schritt 5, Runde 2)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Direction` optionales Feld zu `DtoPlaylistNavigationResult.cs` hinzufügen | Offen | — |
| 2 | Service | `PlaylistService.ResolveExplicitStartEntry` um `PlaylistEntryMediaTypeResolver.IsPlayable` Prüfung erweitern | Offen | — |
| 3 | Service | Fehlerbehandlung: `InvalidOperationException` aus `ResolveExplicitStartEntry` auf HTTP `400 Bad Request` in API-Controller mappen | Offen | — |
| 4 | Komponente | `PlaylistEntriesList.IsPlayableEntry` Methode um `entry.IsAccessible` Prüfung erweitern | Offen | — |
| 5 | Komponente | `PlaylistEntriesList.razor` `@ondblclick` conditional machen (nur auf abspielbaren Zeilen aktiv) | Offen | — |
| 6 | Komponente | `PlaylistDetail.razor` neues Feld `playbackError` (string?, private) hinzufügen | Offen | — |
| 7 | Komponente | `PlaylistDetail.StartPlaybackAsync` Error-Handling anpassen: `playbackError` statt `loadError` setzen, `NavigateTo` nicht aufrufen bei Fehler | Offen | — |
| 8 | Komponente | `PlaylistDetail.razor` Fehler-Rendering strukturieren: `playbackError` inline unter Playlist-Details anzeigen | Offen | — |
| 9 | Komponente | `PlaylistDetail.ClosePlayerAsync` (falls vorhanden) um `playbackError = null` Zeile erweitern | Offen | — |
| 10 | Komponente | `VideoPlayer.razor` `ApplyPlaylistNavigationResultAsync` um Navigationsrichtungs-Kontext erweitern: `playlistEndReached` nur bei Vorwärts-Ende auf `true` | Offen | — |
| 11 | Komponente | `VideoPlayer.razor` `OnNextPlaylistEntryAsync` anpassen: `Direction = "forward"` vor `ApplyPlaylistNavigationResultAsync` aufrufen | Offen | — |
| 12 | Komponente | `VideoPlayer.razor` `OnPreviousPlaylistEntryAsync` anpassen: `Direction = "backward"` vor `ApplyPlaylistNavigationResultAsync` aufrufen | Offen | — |
| 13 | Komponente | `VideoPlayer.razor` Auto-Advance (`OnMediaEndAsync`) anpassen: `Direction = "forward"` setzen | Offen | — |
| 14 | Komponente | `VideoPlayer.razor` (optional) "Vorheriger"/"Nächster"-Buttons mit `disabled`-CSS-Klasse steuern, wenn Grenzen erreicht | Offen | — |
| 15 | Test (Unit) | `PlaylistServiceTests_Playback.cs`: Test `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShow_ThrowsInvalidOperationException` hinzufügen | Offen | — |
| 16 | Test (Unit) | `PlaylistServiceTests_Playback.cs`: Test `StartPlaylistAsync_ExplicitEntryNotPlayable_TVShowSeason_ThrowsInvalidOperationException` hinzufügen | Offen | — |
| 17 | Test (Unit) | `PlaylistServiceTests_Playback.cs`: Test `StartPlaylistAsync_ExplicitEntryNotPlayable_MovieCollection_ThrowsInvalidOperationException` hinzufügen | Offen | — |
| 18 | Test (Unit) | `PlaylistEntriesListTests.cs`: Test `IsPlayableEntry_WithNotAccessibleEntry_ReturnsFalse` hinzufügen | Offen | — |
| 19 | Test (Unit) | `PlaylistEntriesListTests.cs`: Test `IsPlayableEntry_WithNotAccessibleEntryAndNonPlayableMediaType_ReturnsFalse` hinzufügen | Offen | — |
| 20 | Test (Unit) | `PlaylistDetailTests.cs` anlegen oder erweitern: Test `StartPlaybackAsync_WithHttpError403_SetPlaybackError` hinzufügen | Offen | — |
| 21 | Test (Unit) | `PlaylistDetailTests.cs`: Test `StartPlaybackAsync_WithHttpError400_SetPlaybackError` hinzufügen | Offen | — |
| 22 | Test (Unit) | `PlaylistDetailTests.cs`: Test `StartPlaybackAsync_OnSuccess_ClearsPlaybackError` hinzufügen | Offen | — |
| 23 | Test (E2E) | `VideoPlayerPreviousAtStartE2ETest.cs` anlegen: Klick auf "Vorheriger" am Anfang zeigt keine "Ende"-Meldung | Offen | — |
| 24 | Test (E2E) | `PlaylistLockedEntryPlayButtonE2ETest.cs` anlegen: "Abspielen"-Button nicht sichtbar, Doppelklick hat keine Wirkung auf gesperrtem Eintrag | Offen | — |
| 25 | Test (E2E) | `PlaylistDoubleClickCollectionEntryE2ETest.cs` anlegen: Doppelklick auf TVShow/Staffel/Collection wird verhindert | Offen | — |
| 26 | Test (E2E) | `PlaylistLockedEntryPlayBackendErrorE2ETest.cs` anlegen (optional): Backend-Fehler wird als `playbackError` inline angezeigt | Offen | — |
