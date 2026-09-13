# Tasks: Nachbesserung Entwicklungsschritt 6 – Korrektionen zum Weiterschauen mit Playlist-Bezug (Runde 1)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ContinueWatchingDto.Id`-Eigenschaft (`long`) hinzufügen | Offen | — |
| 2 | Logik | `ContinueWatchingService.GetListAsync()` anpassen: `ContinueWatchingDto.Id` mit Datenbank-ID befüllen (Zeile 113–142) | Offen | — |
| 3 | UI | `ContinueWatchingList.razor`: Dictionary-Befüllung von `it.Entry.Id` auf `it.Id` ändern (Zeilen 50–90) | Offen | — |
| 4 | UI | `ContinueWatchingList.razor`: Dictionary-Zugriff von `it.Entry.Id` auf `it.Id` ändern (Zeilen 145–148) | Offen | — |
| 5 | UI | `ContinueWatchingList.razor`: @key-Attribut von `it.Entry.Id` auf `it.Id` ändern (Zeile 147) | Offen | — |
| 6 | Datenmodell | `DtoPlaylistPlaybackStart.StartPositionSeconds`-Eigenschaft (`long`) hinzufügen | Offen | — |
| 7 | Logik | API-Endpunkt (z. B. in `VideoWebPlayerController`): `DtoPlaylistPlaybackStart.StartPositionSeconds` mit `ContinueWatchingEntry.PositionSeconds` befüllen | Offen | — |
| 8 | UI | `PlaylistDetail.razor`: `VideoPlayer`-Komponente mit `StartPositionSeconds`-Parameter initialisieren (Zeilen 86–97) | Offen | — |
| 9 | Logik | `ContinueWatchingService`: Neue private Methode `ResolvePlaylistDeletionConflictsAsync()` implementieren (Konfliktauflösung für Unique-Index) | Offen | — |
| 10 | Logik | `PlaylistService.DeletePlaylistAsync()`: Aufruf von `ResolvePlaylistDeletionConflictsAsync()` vor Playlist-Löschung integrieren (Zeile 130–136) | Offen | — |
| 11 | Tests | bUnit-Test: `ContinueWatchingList_MultipleVariantsOfSameMedia_RendersWithUniqueKeys` – mehrfache Varianten desselben Videos rendern mit eindeutigen Keys | Offen | — |
| 12 | Tests | bUnit-Test: `ContinueWatchingList_ContextActionOnMultipleVariants_TargetsCorrectEntryById` – Kontextaktionen auf richtigem Eintrag ausführen | Offen | — |
| 13 | Tests | SQLite-Integrationtest: `DeletePlaylist_WithCompetingNullPlaylistEntry_ResolvesConflictAndDeletesPlaylist` – Konfliktauflösung ohne DB-Fehler | Offen | — |
| 14 | Tests | Unit-Test: `DtoPlaylistPlaybackStart_WithStartPositionSeconds_IsCorrectlyPopulated` – Position korrekt befüllt | Offen | — |
| 15 | Tests | bUnit-Test (optional): `PlaylistDetail_WithStartPositionSeconds_PassesToVideoPlayer` – Parameter an VideoPlayer übergeben | Offen | — |
| 16 | E2E-Tests | E2E-Test: Weiterschauen-Liste mit mehrfachen Varianten rendern und anzeigen | Offen | — |
| 17 | E2E-Tests | E2E-Test: Kontextaktionen auf mehrfachen Varianten korrekt ausführen | Offen | — |
| 18 | E2E-Tests | E2E-Test: Playlist mit konkurrierendem Eintrag löschen (Konfliktauflösung) | Offen | — |
| 19 | E2E-Tests | E2E-Test: Fortsetzen einer Episode über Playlist mit gespeicherter Position | Offen | — |
