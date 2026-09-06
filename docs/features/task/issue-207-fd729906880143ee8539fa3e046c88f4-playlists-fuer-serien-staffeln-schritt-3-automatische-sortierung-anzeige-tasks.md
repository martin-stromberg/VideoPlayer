# Tasks: Schritt 3 – Automatische Sortierung und performante Anzeige mit Infinity-List

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `PlaylistSettings.cs`: Property `DefaultPageSize: int = 20` hinzufügen | Offen | — |
| 2 | Konfiguration | `PlaylistSettings.cs`: Property `MaxPageSize: int = 100` hinzufügen | Offen | — |
| 3 | Datenmodelle / DTOs | Neue Klasse `DtoPlaylistEntriesPagedResult` erstellen mit Properties: `Entries: DtoPlaylistEntry[]`, `TotalCount: int`, `HasNextPage: bool`, `PageNumber: int`, `PageSize: int` | Offen | — |
| 4 | Datenmodelle / DTOs | `DtoPlaylistEntry` erweitern: Property `IsAccessible: bool` hinzufügen (vorerst immer `true`) | Offen | — |
| 5 | Interface | `IPlaylistService.cs` erweitern: Neue Methode `GetPlaylistEntriesPagedAsync(playlistId: long, userId: string, pageNumber: int, pageSize: int, cancellationToken: CancellationToken)` mit Rückgabewert `Task<DtoPlaylistEntriesPagedResult>` hinzufügen | Offen | — |
| 6 | Service / Logik | `PlaylistService.cs`: Innere Klasse `MediaTypeHandler` um Property `LoadReleaseDateAsync: Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, DateTime?>>>?` erweitern | Offen | — |
| 7 | Service / Logik | `PlaylistService.cs`: Handler für `Movie` implementieren: `LoadReleaseDateAsync` lädt `ReleaseDate ?? PremieredAt` | Offen | — |
| 8 | Service / Logik | `PlaylistService.cs`: Handler für `TVShowEpisode` implementieren: `LoadReleaseDateAsync` lädt `ReleaseDate ?? PremieredAt` | Offen | — |
| 9 | Service / Logik | `PlaylistService.cs`: Handler für `TVShowSeason` implementieren: `LoadReleaseDateAsync` lädt `PremieredAt ?? FirstEpisode.ReleaseDate` | Offen | — |
| 10 | Service / Logik | `PlaylistService.cs`: Handler für `TVShow` implementieren: `LoadReleaseDateAsync` lädt `PremieredAt ?? FirstEpisode.ReleaseDate` | Offen | — |
| 11 | Service / Logik | `PlaylistService.cs`: Handler für `MovieCollection` implementieren: `LoadReleaseDateAsync` lädt `ReleaseDate ?? PremieredAt` | Offen | — |
| 12 | Service / Logik | `PlaylistService.cs`: Innere Klasse `MediaTypeHandler` um Property `GetHierarchySequenceAsync: Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>>>?` erweitern | Offen | — |
| 13 | Service / Logik | `PlaylistService.cs`: Handler für `Movie` implementieren: `GetHierarchySequenceAsync` liefert `(null, null)` | Offen | — |
| 14 | Service / Logik | `PlaylistService.cs`: Handler für `TVShowEpisode` implementieren: `GetHierarchySequenceAsync` liefert `(TVShowSeasonId, Episode.Number)` | Offen | — |
| 15 | Service / Logik | `PlaylistService.cs`: Handler für `TVShowSeason` implementieren: `GetHierarchySequenceAsync` liefert `(TVShowId, SeasonNumber)` | Offen | — |
| 16 | Service / Logik | `PlaylistService.cs`: Handler für `TVShow` implementieren: `GetHierarchySequenceAsync` liefert `(null, null)` | Offen | — |
| 17 | Service / Logik | `PlaylistService.cs`: Handler für `MovieCollection` implementieren: `GetHierarchySequenceAsync` liefert `(null, null)` | Offen | — |
| 18 | Service / Logik | `PlaylistService.cs`: Private Hilfsmethode `LoadReleaseDataForEntriesAsync()` implementieren, die parallel Erscheinungsdatum-Daten für alle Medientypen lädt | Offen | — |
| 19 | Service / Logik | `PlaylistService.cs`: Private Hilfsmethode `LoadHierarchySequencesForEntriesAsync()` implementieren, die parallel Hierarchie-Sequenzen für alle Medientypen lädt | Offen | — |
| 20 | Service / Logik | `PlaylistService.cs`: Private Hilfsmethode `SortEntriesByReleaseDate()` implementieren mit Sortierlogik: Primär nach `ReleaseDate`, Sekundär nach Hierarchie (`ParentMediaType`, Sequenz), Tertiär nach `AddedAt` | Offen | — |
| 21 | Service / Logik | `PlaylistService.cs`: Neue öffentliche Methode `GetPlaylistEntriesPagedAsync()` implementieren mit: Berechtigung → Einträge laden → Orphan-Bereinigung → Titel laden → Erscheinungsdatum laden → Hierarchie laden → Sortierung → Paginierung → DTO-Konvertierung | Offen | — |
| 22 | API / Controller | `PlaylistsController.cs` erweitern: HTTP GET Endpoint `GET /api/playlists/{playlistId}/entries?pageNumber={pageNumber}&pageSize={pageSize}` mit Query-Parametern (Defaults: pageNumber=1, pageSize=20) hinzufügen | Offen | — |
| 23 | API / Controller | `PlaylistsController.cs`: Endpoint Validierung implementieren: `pageNumber >= 1`, `pageSize >= 1 && <= 100` | Offen | — |
| 24 | API / Controller | `PlaylistsController.cs`: Endpoint Fehlerbehandlung implementieren: 400 Bad Request für Validierungsfehler, 403 Forbidden für Zugriff verweigert, 404 Not Found | Offen | — |
| 25 | UI / Frontend | `PlaylistDetail.razor` anpassen: Bestehende `@foreach` Loop durch `<Virtualize<DtoPlaylistEntry> ItemsProviderAsync="ItemsProviderAsync">` ersetzen (Blazor `Virtualize<T>` aus `Microsoft.AspNetCore.Components.Web`) | Offen | — |
| 26 | UI / Frontend | `PlaylistDetail.razor` erweitern: Properties `CurrentPageNumber: int`, `IsLoadingMore: bool`, `AllEntries: List<DtoPlaylistEntry>`, `HasMorePages: bool`, `TotalCount: int`, `VirtualizeReference: Virtualize<DtoPlaylistEntry>` hinzufügen | Offen | — |
| 27 | UI / Frontend | `PlaylistDetail.razor` implementieren: Methode `LoadInitialPageAsync()` zum Laden von Seite 1 beim Komponenten-Init | Offen | — |
| 28 | UI / Frontend | `PlaylistDetail.razor` implementieren: Methode `ItemsProviderAsync(ItemsProviderRequest)` als Delegate für `Virtualize<T>` zur Lazy-Load-Unterstützung | Offen | — |
| 29 | UI / Frontend | `PlaylistDetail.razor` implementieren: Bedingte Styling für `IsAccessible == false` (opacity: 0.5, graue Farbe, disabled Click-Handler) | Offen | — |
| 30 | Unit-Tests | Neue Testklasse `PlaylistServiceTests_GetEntriesPaged.cs` erstellen | Offen | — |
| 31 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_ReturnsFirstPage()` — Erste Seite wird korrekt geladen | Offen | — |
| 32 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_DifferentPageNumbers()` — Verschiedene `pageNumber` und `pageSize` liefern richtige Einträge | Offen | — |
| 33 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_HasNextPageTrue_WhenMoreExist()` — `HasNextPage` ist `true` wenn mehr Einträge vorhanden | Offen | — |
| 34 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_HasNextPageFalse_OnLastPage()` — `HasNextPage` ist `false` auf letzter Seite | Offen | — |
| 35 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_TotalCountAccurate()` — `TotalCount` nach Orphan-Bereinigung korrekt | Offen | — |
| 36 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_EmptyPlaylist()` — Leere Playlist wird korrekt behandelt | Offen | — |
| 37 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_NotOwner_Throws403()` — Nicht-Besitzer wirft `PlaylistAccessDeniedException` | Offen | — |
| 38 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_PlaylistNotFound_Throws404()` — Nicht existierende Playlist wirft `KeyNotFoundException` | Offen | — |
| 39 | Unit-Tests | Neue Testklasse `PlaylistServiceTests_Sorting.cs` erstellen | Offen | — |
| 40 | Unit-Tests | Test: `SortByReleaseDate_AllEntries_SortedCorrectly()` — Alle Einträge mit `ReleaseDate` werden chronologisch sortiert | Offen | — |
| 41 | Unit-Tests | Test: `SortByReleaseDate_MissingDates_FallbackToHierarchy()` — Einträge ohne Datum fallen auf Hierarchie-Sortierung zurück | Offen | — |
| 42 | Unit-Tests | Test: `SortByReleaseDate_MissingHierarchy_FallbackToAddedAt()` — Einträge ohne Hierarchie fallen auf `AddedAt` zurück | Offen | — |
| 43 | Unit-Tests | Test: `SortByReleaseDate_MixedScenarios_SortCorrectly()` — Gemischte Szenarien sortieren korrekt | Offen | — |
| 44 | Unit-Tests | Test: `SortByReleaseDate_MultipleMediaTypes_SortCorrectly()` — Verschiedene Medientypen in einer Playlist sortieren korrekt | Offen | — |
| 45 | Unit-Tests | Test: `GetPlaylistEntriesPagedAsync_IsAccessibleAlwaysTrue()` — `IsAccessible` ist für alle Einträge `true` (Platzhalter) | Offen | — |
| 46 | Unit-Tests | Hilfsmethode `CreateTestPlaylistWithReleaseDatesAsync()` in `PlaylistServiceTestBase` oder `TestHelpers` hinzufügen | Offen | — |
| 47 | Unit-Tests | Hilfsmethode `CreateEntryWithHierarchyAsync()` in `PlaylistServiceTestBase` hinzufügen | Offen | — |
| 48 | API-Tests | Neue Testmethode in `PlaylistsControllerTests_Entries.cs`: `GetPlaylistEntriesEndpoint_ReturnsPagedResult()` — Endpoint liefert `DtoPlaylistEntriesPagedResult` | Offen | — |
| 49 | API-Tests | Test: `GetPlaylistEntriesEndpoint_DefaultPageSize()` — Defaults werden korrekt angewendet | Offen | — |
| 50 | API-Tests | Test: `GetPlaylistEntriesEndpoint_ValidatesPageNumber()` — `pageNumber < 1` wirft 400 Bad Request | Offen | — |
| 51 | API-Tests | Test: `GetPlaylistEntriesEndpoint_ValidatesPageSize()` — `pageSize < 1` oder `> 100` wirft 400 Bad Request | Offen | — |
| 52 | API-Tests | Test: `GetPlaylistEntriesEndpoint_UnauthorizedNotOwner()` — Nicht-Besitzer erhält 403 | Offen | — |
| 53 | E2E-Tests | E2E-Tests in `PlaylistDetailE2ETests.cs` überprüfen und auf neue `Virtualize<T>` Komponenten-Struktur anpassen | Offen | — |
| 54 | E2E-Tests | E2E-Test: `PlaylistDetail_LoadsFirstPage_OnInit()` — Komponente lädt Seite 1 beim Initialisieren; Virtualize rendert erste Einträge | Offen | — |
| 55 | E2E-Tests | E2E-Test: `PlaylistDetail_LazyLoadsNextPage_OnScroll()` — Beim Scrollen zum Ende lädt Komponente nächste Seite asynchron; Einträge werden appended | Offen | — |
| 56 | E2E-Tests | E2E-Test: `PlaylistDetail_CorrectSorting_ByReleaseDate()` — Einträge werden in korrekter Reihenfolge angezeigt (Release-Datum mit Fallback) | Offen | — |
| 57 | E2E-Tests | E2E-Test: `PlaylistDetail_IsAccessibleFalse_StylesCorrectly()` — Einträge mit `IsAccessible=false` haben Opacity 0.5, graue Farbe, disabled Click-Handler | Offen | — |
| 58 | E2E-Tests | E2E-Test: `PlaylistDetail_Performance_MediumLoad_200Entries()` — Playlist mit 200–300 Einträgen scrollbar ohne merkliche Verzögerung (Virtual Scrolling funktioniert) | Offen | — |

