# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-20, ca. 20:45–20:57 Uhr MESZ (UTC+2)
- **Branch und Commit-ID:** `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`, Commit `89e33a551b207e7fca6e65e3df8f79917dfa9d84` („Backmerge from main to staging (#253)", 2026-10-20 20:28:33 +0200)
- **Uncommittete Änderungen im getesteten Stand:** nur untracked Projekt-Dokumente `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/` (requirement.md, todo.md, diese Bestandsaufnahme). Keine Änderungen an Produktivcode, Tests oder Testkonfiguration.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (lokale Arbeitsstation), .NET SDK 10.0.402, Laufzeit .NET 10.0.13 (`net10.0`). Test-Stack (`VideoWebPlayer.Tests.csproj`): xunit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5, bUnit 2.11.3, Moq 4.20.72, Microsoft.Playwright 1.62.0 (Chromium, lokal installiert), Microsoft.AspNetCore.Mvc.Testing 10.0.12, EF Core InMemory + Sqlite 10.0.12, coverlet.collector 10.0.1.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zentraler Befehl laut `AGENTS.md` §7: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Die CI (`.github/workflows/staging-ci.yml`, `pr-staging-ci.yml`, Zeilen ~115–127) baut das Testprojekt in Release und führt drei Läufe aus: `--filter "Category!=E2E"`, `--filter "Category=Integration"`, `--filter "Category=E2E"`. Playwright-Browserinstallation wie CI: `playwright.ps1 install --with-deps chromium` (Browser waren bereits vorhanden, `~/AppData/Local/ms-playwright`).
- **Build vor den Läufen:** `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` — 0 Fehler, 140 Warnungen (v. a. vorhandene `BL0008`-Warnungen in Account-Razor-Komponenten; Bestand).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E"` | Repo-Root | 0 | 1.552 | 0 | 0 | [TRX](test-results/test-results-non-e2e.trx), [Konsole](test-results/console-non-e2e.log) |
| 2 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=Integration"` | Repo-Root | 0 | 0 | 0 | 0 | [TRX](test-results/test-results-integration.trx), [Konsole](test-results/console-integration.log) — **kein Test entsprach dem Filter** |
| 3 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E"` | Repo-Root | 0 | 208 | 0 | 1 | [TRX](test-results/test-results-e2e.trx), [Konsole](test-results/console-e2e.log) |

**Gesamt: 1.760 bestanden, 0 fehlgeschlagen, 1 übersprungen** (Laufzeiten: ~60 s / <1 s / ~3,9 min). Es wurden keine Tests geändert, deaktiviert oder repariert.

### Nachgewiesene bestehende Testfehler

**Keine.** Es wurde kein fehlgeschlagener Test nachgewiesen — spätere Arbeiten können jeden Fehlschlag mit diesem Stand abgleichen.

### Testlücken und Ausführungsprobleme

- **Übersprungener Test (1):** `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — per `[Fact(Skip = "...")]` deaktiviert (`DevicePlaylistE2ETests_Playback.cs:76`). Begründung im Code: Befund, dass `api/continue-watching` mit 500 antwortet, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.Create` ohne Null-Prüfung); „wird laut Auftrag in diesem Lauf nicht behoben". Ein übersprungener Test ist weder Erfolg noch Fehler-Nachweis.
- **`Category=Integration` findet 0 Tests:** Kein Test trägt das Trait `Category=Integration` (verifiziert per Suche nach `Trait("Category", "Integration")` — 0 Treffer). Der CI-Schritt ist faktisch leer. „Integration" ist keine tatsächliche Suite; Integrationstests laufen unmarkiert im `Category!=E2E`-Lauf mit.
- **E2E-Abdeckung:** 42 Testdateien tragen `[Trait("Category", "E2E")]` (Klassenebene), darunter Playwright-Browsertests und HTTP-Endpunkt-E2Es (`WebApplicationFactory`-basiert). Hinweis aus `AGENTS.md`: Einzelne Playwright-Tests flackern gelegentlich unter Last — in diesem Lauf trat kein Ausfall auf.
- **Keine Tests zum TV-Sender-Bereich:** Es existieren keine Testklassen zu Scheduler, Sendeplan, Live-Programm oder Segmentierung — der gesamte Bereich ist neu. Nächstliegende Suites: `Services/PlaylistBackfill*` (Worker-/Marker-Muster), `Services/PlaylistServiceTests_*` (Zugriffs-/Kaskadenlogik), `Controllers/ItemsController*Tests` (Streaming/Auth), `Services/Backups/VideoWebPlayerBackupDataTests*` (Alt-Backup-Regression).

## Testklassen (für die Anforderung relevant)

### `ApplicationDbContextTests` (`ApplicationDbContextTests.cs`)
- `DeleteMediaSourceAsync_RemovesSourceAndAllRelatedEntities` — Löschkaskade über Quelle→Kollektionen→Items→Movies/Episoden gegen **echtes SQLite** (`file:...?mode=memory&cache=shared`) — Muster für FK-/Löschverhaltenstests neuer Tabellen.

### `EnumContractTests` (`EnumContractTests.cs`)
- `RecentEntryType_ClientAndServerDefinitions_HaveTheSameMembersAndValues` — Client/Server-Enum-Gleichstand
- `ManualBackupJobStatus_HasTheDocumentedStatesInOrder`, `RestoreBackupJobStatus_HasTheDocumentedStatesInOrder_AndMirrorsTheManualBackupStates`, `PairingCodeKind_HasTheStoredNumericValues` — Pinnt Vertrags-Enums (Mitglieder, Reihenfolge, numerische Werte)

### `ApiDocumentationContractTests` / `..._Runtime` (Root)
- `ApiDocumentationContainsMauiRelevantRoutes`, `RequiredRoutes_ContainEveryRouteOfThePlaylistsController`, `ApiDocumentation_DescribesEveryRouteOfThePlaylistsController`, `PlaylistWalkthrough_SignInCreateAddPlayNext_MatchesDocumentedContract`, `MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds` — API.md-Doku-Zwang für Endpunkte.

### `Services/PlaylistBackfill*` (Worker-/Marker-Muster)
- `PlaylistBackfillWorkerTests`, `PlaylistBackfillCoordinatorTests`, `PlaylistBackfillServiceTests(_Genres)`, `PlaylistBackfillSignalTests`, `PlaylistBackfillMarkerProcessingTests`, `PlaylistBackfillMarkerHookTests` — Aufweck-Signal, Blockverarbeitung, Safety-Sweep, SaveChanges-Hook, Zeitsteuerung über `TimeProvider`.

### `Services/PlaylistServiceTests_*` (Zugriffs-/Kaskaden-/Playback-Logik)
- `_Create`, `_Read`, `_Update`, `_Delete`, `_DeleteWithConflictResolution`, `_AddMedia`, `_RemoveMedia`, `_RemoveMediaWithContinueWatching`, `_GetEntries`, `_GetEntriesPaged`, `_Reorder`, `_SortMode`, `_Genres`, `_Exclusion`, `_EntryDetails`, `_Playback`, `_PublicAccessMatrix`, `_PublicContinueWatching`, `_PublicViewerPlayback`, `_ContinueWatchingReplacementOnSourceDeletion`, `_Cover` — CRUD, Kaskade, Sortierung, Public-Access-Matrix, Playback-Navigation.

### `Controllers/*` (Endpoints/Auth)
- `PlaylistsControllerTests_*` (Auth, Create, Read, Update, Delete, Entries, Entries-Paged, Reorder, SortMode, Genres, Public, Cover*, Playback, RemoveMediaWithContinueWatching) — Controller-Verhalten mit `FakeAuthService`/`TestAuthService`.
- `ItemsControllerTests_Search`, `ItemsControllerAccessTests`, `ItemsControllerLocalSourceTests`, `ItemsControllerErrorMappingTests`, `ItemsControllerMediaFileTypeTests`, `ItemsControllerMediaItemWithoutFileNameTests`, `ItemsControllerMetadataTests` — Stream-/Metadaten-Endpoints, 403/404/400-Mapping.
- `ActorsControllerTests`, `EpisodesControllerBackgroundImageTests`, `PicturesControllerPlaylistCoverTests`, `SourceVisibilityControllerTests`, `BackupsControllerAuthorizationTests`, `UpdatesControllerAuthorizationTests` — weitere Endpunkte/AuthZ.

### `Services/Backups/VideoWebPlayerBackupDataTests*` (Alt-Backup-Regression)
- `VideoWebPlayerBackupDataTests`, `VideoWebPlayerBackupDataTests_DevicePairing` — Restore von Backups ohne neuere Tabellen/Spalten; Hilfsklasse `LegacyBackupArchiveBuilder` erzeugt Alt-Archive. **Vorbild für den Pflicht-Regressionstest der neuen Sender-Tabellen/-Spalten.**

### `Services/ContinueWatching*` / `MediaSource*`
- `ContinueWatchingService*Tests`, `ContinueWatchingPlaylist*Tests` — Fortschritt, Serien-Nachfolge (`GetNextEpisode`), Playlist-Bindung; `MediaSourceScanServiceTests`, `MediaSourceScannerTests`, `MediaSourceScannerLocalTests`, `LocalMediaSourceClassifierTests`, `MediaSourceClassifier*Tests`, `MediaEntryFilterTests`, `MediaHierarchyRegistryUnlockIdSpaceTests`, `MediaSourceReaderDispatcherTests`, `LocalMediaSourceReaderTests`, `SftpMediaSourceReaderOpenFileStreamTests` — Scan-/Klassifikations-/Reader-Pipeline.

### Geräte/Auth/Pairing
- `DeviceTokenServiceTests`, `RefreshTokenServiceTests`, `PairingServiceTests_*`, `PairingExchangeContractTests_*`, `PairingBootstrapServiceTests`, `PairingBootstrapEndpointsTests`, `ApiTokenConfigurationTests`, `ApiTokenScopeTests`, `ClientLibrary/VideoWebPlayerClientTests_*` — Token-Ausstellung, Rotation, Widerruf, Device-Auth-Flows.

### Komponenten-/E2E-Tests
- `Components/*` (bUnit): `PlaylistDetail*Tests`, `PlaylistsOverviewTests`, `PlaylistEntriesList*Tests`, `MediaSearchSelectorTests`, `MediaBoxContextMenuInteractionTests`, `VideoPlayerStreamErrorTests`, `ContinueWatchingListTests`, `NavMenuPlaylistsTests`.
- `*E2ETests` (Playwright + `WebApplicationFactory`): `PlaylistPlaybackE2ETests` (u. a. `StreamErrorMessage_KeepsPlaylistControlsInTheViewport`), `LocalMediaSourceStreamingE2ETests` (`Stream_LocalMovie_WithoutSourceAccess_ReturnsForbidden`), `DevicePlaylistE2ETests_*` (access_token-Query-Auth), `MediaSearchSelector`-E2Es (`Search_UserWithoutAccess_MediumNotInResults`), `EpisodesBackgroundImageAccessTokenE2ETests` (`?access_token=` positiv/negativ).

## Hilfsmethoden / Testinfrastruktur (`Helpers/`)

- `PlaylistServiceTestBase`, `PlaylistsControllerTestBase`, `ContinueWatchingServiceTestBase`, `ContinueWatchingPlaylistTestBase`, `DeviceClientTestBase`, `PairingExchangeContractTestBase`, `LocalMediaPlaylistE2ETestBase`, `PlaylistsE2ETestBase`, `MediaBoxContextMenuE2ETestBase` — Aufbau von DbContext (SQLite-InMemory/shared oder EF InMemory), Nutzern, Quellen, Medienhierarchien.
- `FakeAuthService`, `TestAuthService`, `NoOpVideoWebPlayerClient` — Auth-/Client-Stubs.
- `ManualTimeProvider`, `IncrementingTimeProvider` — **steuerbare Zeit für zeitplanabhängige Logik** (direkt für die Playlist-Engine relevant).
- `PlaylistBackfillTestServices`, `TestableMediaSourceScanService`, `BackfillSftpMediaSourceReader`, `FakeSftpMediaSourceReader`, `SeriesSftpMediaSourceReader`, `ThrowingSftpMediaSourceReader`, `UnreadableFileReader` — Reader-/Scan-Doubles.
- `ItemsControllerTestFactory`, `PairingWebApplicationFactory`, `PairingTestDb`, `PairingCryptoHelper`, `UnlockedMediaTestHelper`, `TestHelpers`, `TestImages`, `TestWebHostEnvironment`, `CapturingLogger`, `ListLogger`, `SqlCommandRecorder`, `BackupUploadSessionServiceTestSupport` — Fabriken/Recorder.
- DB-Strategie: ~30 Testdateien mit echtem SQLite (`UseSqlite`/`SqliteConnection`, FK/Unique wirksam — `AGENTS.md`-Pflicht für Index-/FK-/Löschtests), ~16 mit EF InMemory.
