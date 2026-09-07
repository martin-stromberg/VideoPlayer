# Tasks: Schritt 4 – Manuelle Sortierung von Playlists

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Eigenschaft `SortOrder` (long?) zu `PlaylistEntry` hinzufügen | Offen | — |
| 2 | Datenmodell | Composite-Index `(PlaylistId, SortOrder)` zu `PlaylistEntryConfiguration` hinzufügen | Offen | — |
| 3 | Datenbank-Migration | Migration `AddSortOrderToPlaylistEntries` erstellen und anwenden | Offen | — |
| 4 | DTOs | Eigenschaft `SortOrder` (long?) zu `DtoPlaylistEntry` hinzufügen | Offen | — |
| 5 | DTOs | DTO-Klasse `DtoReorderPlaylistEntryRequest` mit Eigenschaft `NewSortOrder: long` erstellen | Offen | — |
| 6 | DTOs | DTO-Klasse `DtoReorderOperation` (nested) mit Eigenschaften `EntryId: long`, `NewSortOrder: long` erstellen | Offen | — |
| 7 | DTOs | DTO-Klasse `DtoBatchReorderPlaylistEntriesRequest` mit Eigenschaft `ReorderOperations: List<DtoReorderOperation>` erstellen | Offen | — |
| 8 | DTOs | DTO-Klasse `DtoChangeSortModeRequest` mit Eigenschaften `NewSortMode: string`, `ConfirmLossOfManualOrder: bool?` erstellen | Offen | — |
| 9 | DTOs | DTO-Klasse `DtoChangeSortModeConflictResponse` mit Eigenschaft `IsLossOfDataConfirmationRequired: bool` erstellen | Offen | — |
| 10 | Service-Interface | Methode `ReorderPlaylistEntryAsync(long, string, long, long, CancellationToken)` zu `IPlaylistService` hinzufügen | Offen | — |
| 11 | Service-Interface | Methode `BatchReorderPlaylistEntriesAsync(long, string, List<(long, long)>, CancellationToken)` zu `IPlaylistService` hinzufügen | Offen | — |
| 12 | Service-Interface | Methode `ChangeSortModeAsync(long, string, string, bool?, CancellationToken)` zu `IPlaylistService` hinzufügen | Offen | — |
| 13 | Service-Implementierung | Methode `GetPlaylistEntriesPagedAsync()` anpassen: Sortierlogik für Manual-Mode (nach `SortOrder` aufsteigend) | Offen | — |
| 14 | Service-Implementierung | Methode `AddMediaToPlaylistAsync()` anpassen: SortOrder-Zuweisung bei Manual-Mode (`maxSortOrder + 1`) | Offen | — |
| 15 | Service-Implementierung | Methode `ReorderPlaylistEntryAsync()` implementieren (Ownership-Check, Mode-Check, Update, Speichern) | Offen | — |
| 16 | Service-Implementierung | Methode `BatchReorderPlaylistEntriesAsync()` implementieren (Ownership-Check, Mode-Check, Duplikat-Detektion, transaktionales Speichern) | Offen | — |
| 17 | Service-Implementierung | Methode `ChangeSortModeAsync()` implementieren (Ownership-Check, Mode-spezifische Logik, SortOrder-Population/-Clearing) | Offen | — |
| 18 | API-Endpoints | Endpoint `[HttpPut("{id}/entries/{entryId}/order")]` für Reorder-Operationen implementieren | Offen | — |
| 19 | API-Endpoints | Endpoint `[HttpPost("{id}/entries/batch-reorder")]` für Batch-Reorder-Operationen implementieren | Offen | — |
| 20 | API-Endpoints | Endpoint `[HttpPatch("{id}/sort-mode")]` für Sortiermodus-Wechsel implementieren | Offen | — |
| 21 | UI-Komponenten | `PlaylistDetail.razor` erweitern: Drag-&-Drop-Event-Handler hinzufügen (draggable, ondragstart, ondragover, ondrop, ondragend) | Offen | — |
| 22 | UI-Komponenten | JavaScript-Datei `wwwroot/js/playlist-drag-drop.js` erstellen mit Drag-&-Drop-Funktionen | Offen | — |
| 23 | UI-Komponenten | `PlaylistDetail.razor` erweitern: Quick-Action-Buttons „An Anfang" und „An Ende" für Manual-Mode hinzufügen | Offen | — |
| 24 | UI-Komponenten | `PlaylistDetail.razor` erweitern: Modal-Dialog für Datenverlust-Warnung bei Mode-Wechsel implementieren | Offen | — |
| 25 | Unit-Tests | Test `ReorderPlaylistEntry_Manual_SuccessfullyReorders` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 26 | Unit-Tests | Test `ReorderPlaylistEntry_NotOwner_ThrowsAccessDenied` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 27 | Unit-Tests | Test `ReorderPlaylistEntry_NotManualMode_ThrowsInvalidOperation` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 28 | Unit-Tests | Test `ReorderPlaylistEntry_EntryNotFound_ThrowsKeyNotFound` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 29 | Unit-Tests | Test `ReorderPlaylistEntry_NegativeSortOrder_ThrowsArgumentException` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 30 | Unit-Tests | Test `BatchReorderPlaylistEntries_SuccessfullyReorders_Multiple` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 31 | Unit-Tests | Test `BatchReorderPlaylistEntries_DuplicateSortOrder_ThrowsInvalidOperation` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 32 | Unit-Tests | Test `BatchReorderPlaylistEntries_DuplicateEntryId_ThrowsArgumentException` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 33 | Unit-Tests | Test `BatchReorderPlaylistEntries_EmptyList_ThrowsArgumentException` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 34 | Unit-Tests | Test `BatchReorderPlaylistEntries_TransactionalRollback_OnError` in `PlaylistServiceTests_Reorder` schreiben | Offen | — |
| 35 | Unit-Tests | Test `ChangeSortModeAsync_ManualToByReleaseDate_WithoutConfirmation_ThrowsInvalidOperation` in `PlaylistServiceTests_SortMode` schreiben | Offen | — |
| 36 | Unit-Tests | Test `ChangeSortModeAsync_ManualToByReleaseDate_WithConfirmation_ClearsSortOrder` in `PlaylistServiceTests_SortMode` schreiben | Offen | — |
| 37 | Unit-Tests | Test `ChangeSortModeAsync_ByReleaseDateToManual_PopulatesSortOrder` in `PlaylistServiceTests_SortMode` schreiben | Offen | — |
| 38 | Unit-Tests | Test `ChangeSortModeAsync_NotOwner_ThrowsAccessDenied` in `PlaylistServiceTests_SortMode` schreiben | Offen | — |
| 39 | Unit-Tests | Test `AddMediaToPlaylistAsync_ManualMode_AppendsWithSortOrder` in `PlaylistServiceTests_AddMedia` schreiben | Offen | — |
| 40 | Unit-Tests | Test `AddMediaToPlaylistAsync_ByReleaseDateMode_SortOrderIsNull` in `PlaylistServiceTests_AddMedia` schreiben | Offen | — |
| 41 | Unit-Tests | Test `AddMediaToPlaylistAsync_ManualMode_CascadeChildren_SequentialSortOrder` in `PlaylistServiceTests_AddMedia` schreiben | Offen | — |
| 42 | Unit-Tests | Test `GetPlaylistEntriesPagedAsync_ManualMode_SortedBySortOrder` in `PlaylistServiceTests_GetEntriesPaged` schreiben | Offen | — |
| 43 | Unit-Tests | Test `GetPlaylistEntriesPagedAsync_ByReleaseDateMode_SortedByReleaseDate` in `PlaylistServiceTests_GetEntriesPaged` schreiben | Offen | — |
| 44 | Unit-Tests | Test `GetPlaylistEntriesPagedAsync_ManualMode_NullSortOrder_Fallback` in `PlaylistServiceTests_GetEntriesPaged` schreiben | Offen | — |
| 45 | Controller-Tests | Test `ReorderPlaylistEntryEndpoint_WithValidRequest_Returns200` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 46 | Controller-Tests | Test `ReorderPlaylistEntryEndpoint_NotOwner_Returns403` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 47 | Controller-Tests | Test `ReorderPlaylistEntryEndpoint_NotManualMode_Returns400` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 48 | Controller-Tests | Test `ReorderPlaylistEntryEndpoint_EntryNotFound_Returns404` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 49 | Controller-Tests | Test `BatchReorderPlaylistEntriesEndpoint_WithValidRequest_Returns200` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 50 | Controller-Tests | Test `BatchReorderPlaylistEntriesEndpoint_DuplicateSortOrder_Returns409` in `PlaylistsControllerTests_Reorder` schreiben | Offen | — |
| 51 | Controller-Tests | Test `ChangeSortModeEndpoint_ManualToByReleaseDate_WithoutConfirmation_Returns409` in `PlaylistsControllerTests_SortMode` schreiben | Offen | — |
| 52 | Controller-Tests | Test `ChangeSortModeEndpoint_ManualToByReleaseDate_WithConfirmation_Returns200` in `PlaylistsControllerTests_SortMode` schreiben | Offen | — |
| 53 | Controller-Tests | Test `ChangeSortModeEndpoint_ByReleaseDateToManual_Returns200` in `PlaylistsControllerTests_SortMode` schreiben | Offen | — |
| 54 | E2E-Tests | Test `E2E_DragDropReorder_ManualMode_PersistsSortOrder` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 55 | E2E-Tests | Test `E2E_QuickActionButtons_MoveToBeginning_Reorders` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 56 | E2E-Tests | Test `E2E_QuickActionButtons_MoveToEnd_Reorders` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 57 | E2E-Tests | Test `E2E_SortModeChange_Manual_Displays_DragControls` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 58 | E2E-Tests | Test `E2E_SortModeChange_ByReleaseDate_ToManual_NeverWarns` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 59 | E2E-Tests | Test `E2E_SortModeChange_Manual_ToByReleaseDate_ShowsWarningModal` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 60 | E2E-Tests | Test `E2E_SortModeChange_Manual_ToByReleaseDate_ConfirmDeletion_UpdatesMode` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 61 | E2E-Tests | Test `E2E_AddMediaToPlaylist_ManualMode_AppendedAtEnd` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 62 | E2E-Tests | Test `E2E_InfinityList_ManualMode_VirtualizationWorks` in `PlaylistDetailE2ETests` schreiben | Offen | — |
| 63 | E2E-Tests | Test `E2E_BatchReorder_MultipleEntries_AllReorder` in `PlaylistEntriesE2ETests` schreiben | Offen | — |
| 64 | Test-Infrastruktur | Hilfsmethode `CreatePlaylistInManualMode()` zu `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 65 | Test-Infrastruktur | Hilfsmethode `CreatePlaylistWithEntries_ManualMode(int count)` zu `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 66 | Test-Infrastruktur | Hilfsmethode `VerifySortOrderSequence(List<DtoPlaylistEntry>, long[])` zu `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 67 | Test-Anpassungen | Bestehende Tests in `PlaylistServiceTests_GetEntriesPaged` überprüfen und ggf. anpassen für Manual-Mode-Sortierung | Offen | — |
| 68 | Test-Anpassungen | Bestehende Tests in `PlaylistServiceTests_AddMedia` überprüfen und ggf. anpassen für SortOrder-Zuweisung | Offen | — |
| 69 | Test-Anpassungen | Bestehende Tests in `PlaylistsControllerTests_Update` überprüfen auf Auswirkungen durch `/sort-mode` Endpoint | Offen | — |
| 70 | Dokumentation | Dokumentation in `docs/help/playlists-api.md` für neue Endpoints aktualisieren | Offen | — |
| 71 | Dokumentation | Dokumentation in `docs/help/playlists.md` für manuelle Sortierung aktualisieren | Offen | — |

---

**Dokumentation erstellt:** 2026-09-07  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-4-manuelle-sortierung

**Hinweise zur Aktualisierung:**
- Status: Nach Abschluss einer Aufgabe auf „Abgeschlossen" setzen
- Testnachweis: Teste-ID oder Link eintragen, der den Abschluss nachweist (z. B. Test-Datei, PR-Commit)
- Diese Datei wird über `/review-plan` aktualisiert
