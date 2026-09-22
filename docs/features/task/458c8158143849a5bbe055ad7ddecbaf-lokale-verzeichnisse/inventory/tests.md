# Tests – Bestandsaufnahme und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-22, Läufe zwischen ca. 19:00 und 19:30 Uhr (MESZ, UTC+2)
- **Branch:** `task/458c8158143849a5bbe055ad7ddecbaf-lokale-verzeichnisse`
- **Commit-ID:** `9d8868db47590c99f3a694e5eee5eaeb0eec7770` („fix: Fehlende Authorisierung beim Quellenabruf im Menü", erstellt 18:57:15 +0200 während dieser Bestandsaufnahme durch einen parallelen Prozess „Softwareschmiede Bot"; ändert nur `VideoWebPlayer/Components/Layout/NavMenu.razor`, +2 Zeilen)
- **Uncommittete Änderungen im getesteten Stand:** nur untracked `docs/features/task/` (diese Dokumentation); sonst clean (`git status --porcelain` = 1 Eintrag)
- **Testumgebung / Runtime:** Windows, Git Bash; .NET SDK 10.0.401 (einzige installierte SDK), Runtime .NET 10.0.12; xUnit.net v3 (3.2.2) mit VSTest-Adapter 3.1.5; Playwright 1.49.0 mit lokal installierten Browsern (`%LOCALAPPDATA%\ms-playwright`, u. a. chromium-1243)
- **Vorlauf:** `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` → 0 Fehler, 140 Warnungen (u. a. preexisting BL0008 in Account-Pages)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** einzige Suite ist `VideoWebPlayer.Tests` (xUnit v3). Die Testbefehle stammen aus `.github/workflows/staging-ci.yml` (identisch in `pr-staging-ci.yml`), Job `build-and-test` (Zeilen 114–127): Unit (`Category!=E2E`), Integration (`Category=Integration`), E2E (`Category=E2E`). Die Kategorie `Integration` existiert im Code nicht (kein `Trait("Category","Integration")` gefunden; nur 29× `Trait("Category","E2E")`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit/Non-E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=test-results-baseline.trx" --results-directory docs/features/task/458c8158143849a5bbe055ad7ddecbaf-lokale-verzeichnisse/inventory/test-results` | Repo-Root | 0 | 256 | 0 | 0 | [Log](test-results/unit-tests-console.log), [TRX](test-results/test-results-baseline.trx) |
| Integration | `dotnet test ... --filter "Category=Integration" --logger "trx;LogFileName=integration-test-results-baseline.trx"` (Rest wie oben) | Repo-Root | 0 | 0 | 0 | 0 | [Log](test-results/integration-tests-console.log), [TRX](test-results/integration-test-results-baseline.trx) — „Kein Test entspricht dem angegebenen Testfallfilter" |
| E2E | `dotnet test ... --filter "Category=E2E" --logger "trx;LogFileName=e2e-test-results-baseline.trx"` (Rest wie oben) | Repo-Root | 1 (Konsole: „Fehler beim Testlauf"; Pipeline-Exit durch `tee` verdeckt, TRX: `failed="37"`) | 8 | 37 | 0 | [Log](test-results/e2e-tests-console.log), [TRX](test-results/e2e-test-results-baseline.trx) |

### Nachgewiesene bestehende Testfehler

Alle 37 E2E-Fehlschläge haben eines von zwei Fehlerbildern:

1. **`Assert.Equal() Failure: Expected: OK, Actual: Found`** (HTTP 302 statt 200) — 3 Tests
2. **`System.TimeoutException : Timeout (30|120)s exceeded. waiting for Locator("#email")`** — die Login-Seite wird im Playwright-Browser nie angezeigt — 34 Tests

Gemeinsame Ursache (Code-Evidenz, nicht verifiziert durch Gegenlauf): Seit Commit `9d8868d` ruft `NavMenu.razor` `OnInitializedAsync` → `Client.EnsureAuthorizationTokenAsync(state.User)` auf; `InternalVideoWebPlayerClient.ImpersonateAsync` wirft für anonyme Benutzer `UnauthorizedAccessException` (`InternalVideoWebPlayerClient.cs` Zeile 131). `NavMenu` wird über `MainLayout.razor` (Zeile 25) auf jeder Seite gerendert — inkl. `/about`, `/Account/Login`, `/Account/Register`. Die Ausnahme führt bei SSR offenbar zu einem Redirect (`Found`) statt zur gerenderten Seite. Vor dem Commit fing `RequestSourcesAsync` alle Fehler ab (`VideoWebPlayerClient.cs` Zeilen 226–233). Die Fehler sind damit vermutlich **keine** langjährig preexisting Fehler, sondern Folge des während dieser Bestandsaufnahme entstandenen Commits — nachgewiesen ist jedoch nur der dokumentierte Stand an `9d8868d`.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `AboutPageE2ETests.AboutPage_ReturnsFirstStepsAndGitHubLink` | `VideoWebPlayer.Tests/AboutPageE2ETests.cs:44` | `Expected: OK, Actual: Found` | E2E-Lauf, TRX |
| `FirstUserRedirectE2ETests.RegisterFirstUser_SubmitsAndSetsAuthCookie` | `VideoWebPlayer.Tests/FirstUserRedirectE2ETests.cs:99` | `Expected: OK, Actual: Found` | E2E-Lauf, TRX |
| `IdentityRedirectManagerE2ETests.RegisterWithConfirmedAccount_DoesNotThrowNavigationException` | `VideoWebPlayer.Tests/IdentityRedirectManagerE2ETests.cs:52` | `Expected: OK, Actual: Found` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_ExceedingLimit_ShowsError` | `VideoWebPlayer.Tests/BackupUploadE2ETests.cs:243` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_InterruptedSession_CanDiscardResumeHint` | `BackupUploadE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_InterruptedSession_ShowsResumeHint` | `BackupUploadE2ETests.cs:293` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_Interrupted_ResumesFromServerOffset` | `BackupUploadE2ETests.cs:174` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_InvalidFile_ShowsError` | `BackupUploadE2ETests.cs:215` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_LargeFile_UsesMultipleChunks` | `BackupUploadE2ETests.cs:147` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack` | `BackupUploadE2ETests.cs:352` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_NonAdmin_SeesNoUploadControl` | `BackupUploadE2ETests.cs:395` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_NonJsonErrorResponse_ShowsFriendlyMessage` | `BackupUploadE2ETests.cs:326` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_ValidFile_ShowsProgressAndImportsBackup` | `BackupUploadE2ETests.cs:126` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `BackupUploadE2ETests.BackupUpload_WhileRunning_DisablesUploadButton` | `BackupUploadE2ETests.cs:263` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.ClickOutside_ClosesOpenMenu` | `VideoWebPlayer.Tests/MediaBoxContextMenuInteractionE2ETests.cs` | Timeout 30 s, `Locator("#email")` (via `MediaBoxContextMenuE2ETestBase.LoginAndNavigateToHomeAsync`, Zeile 105) | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.EscapeKey_ClosesOpenMenuAndRestoresLinkFocus` | `MediaBoxContextMenuInteractionE2ETests.cs:132` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.LongPress_ExactlyThreeSeconds_OpensMenuWithActions` | `MediaBoxContextMenuInteractionE2ETests.cs` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.MenuAction_Remove_ClosesMenuAndRemovesCard` | `MediaBoxContextMenuInteractionE2ETests.cs:180` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.MovementOverTolerance_CancelsPendingLongPress` | `MediaBoxContextMenuInteractionE2ETests.cs:64` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.NativeContextMenu_RightClick_DoesNotOpenActionMenu` | `MediaBoxContextMenuInteractionE2ETests.cs:85` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.PointerCancel_ClosesOpenMenu` | `MediaBoxContextMenuInteractionE2ETests.cs:103` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuInteractionE2ETests.PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally` | `MediaBoxContextMenuInteractionE2ETests.cs:44` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuPositionE2ETests.OpenMenu_OnFirstCard_StaysWithinViewportBounds(width: 1280, height: 800)` | `VideoWebPlayer.Tests/MediaBoxContextMenuPositionE2ETests.cs:24` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuPositionE2ETests.OpenMenu_OnFirstCard_StaysWithinViewportBounds(width: 375, height: 667)` | `MediaBoxContextMenuPositionE2ETests.cs:24` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuPositionE2ETests.OpenMenu_OnLastCard_StaysWithinViewportBounds(width: 1280, height: 800)` | `MediaBoxContextMenuPositionE2ETests.cs:38` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaBoxContextMenuPositionE2ETests.OpenMenu_OnLastCard_StaysWithinViewportBounds(width: 375, height: 667)` | `MediaBoxContextMenuPositionE2ETests.cs:38` | Timeout 30 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaSourceDeleteE2ETests.Admin_Can_Delete_MediaSource_And_It_Disappears` | `VideoWebPlayer.Tests/MediaSourceDeleteE2ETests.cs:107` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MediaSourceSwitchE2ETests.User_Can_Switch_Source_From_Menu_And_Sees_Only_Selected_Source_Titles` | `VideoWebPlayer.Tests/MediaSourceSwitchE2ETests.cs:113` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `MovieCollectionEditE2ETests.Admin_Can_Edit_And_Save_Single_Movie_Title` | `VideoWebPlayer.Tests/MovieCollectionEditE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UnlockedMediaE2ETests.Admin_Can_Unlock_TVShow_And_MovieCollection_For_User` | `VideoWebPlayer.Tests/UnlockedMediaE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UnlockedSourceE2ETests.Regular_User_Can_Open_Unlock_Source_And_Only_Unlocked_Entries_Are_Shown` | `VideoWebPlayer.Tests/UnlockedSourceE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Admin_Checks_For_Updates_And_Refreshes_Changed_Data` | `VideoWebPlayer.Tests/UpdatesPageE2ETests.cs:155` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Admin_Edits_Resets_And_Saves_Update_Configuration` | `UpdatesPageE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Admin_Installs_Update_Through_Post_And_Sees_Result` | `UpdatesPageE2ETests.cs:262` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Admin_Navigates_To_Updates_Page_And_Sees_Structured_Status` | `UpdatesPageE2ETests.cs:122` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Updates_Page_Disables_Manual_Actions_When_Busy_Or_Locked` | `UpdatesPageE2ETests.cs` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |
| `UpdatesPageE2ETests.Updates_Page_Remains_Usable_On_Mobile_Viewport` | `UpdatesPageE2ETests.cs:309` | Timeout 120 s, `Locator("#email")` | E2E-Lauf, TRX |

**Im E2E-Lauf bestanden (8):** `ContinueWatchingE2ETests` (4 Tests), `EpisodesBackgroundImageAccessTokenE2ETests` (2), `FirstUserRedirectE2ETests.HomePage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl`, `FirstUserRedirectE2ETests.LoginPage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl`.

### Testlücken und Ausführungsprobleme

- **`Category=Integration` liefert 0 Tests:** Der CI-Workflow führt diesen Filter aus, aber kein Test im Projekt trägt dieses Trait. Die Suite ist faktisch leer (kein Ausführungsfehler).
- **E2E-Umgebung:** Playwright-Browser lokal vorhanden (`ms-playwright`), Browser-Tests starten (Timeouts treten beim Warten auf Seitenelemente auf, nicht beim Browser-Start). Keine Infrastruktur-/Setup-Fehler festgestellt.
- **Keine übersprungenen Tests** in beiden Läufen (`skipped=0`, TRX `notExecuted="0"`).
- Anforderungsbezogene Testlücke: Es existieren **keine** Tests für einen lokalen Quelltyp — es gibt noch keinen `LocalMediaSourceReader`, kein `SourceType`-Feld und keine Reader-Auswahl; alle Reader-Tests faken SFTP.

## Testklassen (anforderungsrelevant)

### `MediaSourceScannerTests` — `VideoWebPlayer.Tests/Services/MediaSourceScannerTests.cs`
- `ScanNextMediaCollection_SkipsCollection_WhenRemoteDirectoryIsMissing` — verifiziert, dass `SftpPathNotFoundException` beim Scannen zum Überspringen der Collection führt (nutzt `ThrowingSftpMediaSourceReader`).

### `MediaSourceScanServiceTests` — `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `ExecuteAsync_RunsScanAndClassification_WhenNoCollectionsExist`
- `ExecuteAsync_AddsMediaItem_WhenSourceContainsMovieFile`
- `ExecuteAsync_ClassifiesEpisodes_WhenSeriesStructureExists`
- `ExecuteAsync_DetectsNewSeason_WhenEpisodesAddedAfterFirstScan`
- `Services_ScannerAndClassifier_IncrementalSeriesProcessing`
Alle nutzen `SeriesSftpMediaSourceReader`/`FakeSftpMediaSourceReader` + `TestableMediaSourceScanService` und EF-InMemory/SQLite.

### `MediaSourceClassifierBackgroundImageTests` — `VideoWebPlayer.Tests/MediaSourceClassifierBackgroundImageTests.cs`
- `ClassifyAllAsync_MarksBackgroundForUpdate_WhenNewFanartFileAppearsForEpisodeWithExistingGeneratedBackground`
- `ClassifyAllAsync_DoesNotMarkBackgroundForUpdate_WhenOnlyBannerFileIsAssigned`
- `ClassifyAllAsync_DoesNotOverwriteManuallyEditedSeriesMetadata`
- `ClassifyAllAsync_ReusesManuallyRenamedSeriesSeasonAndEpisode`

### `MediaSourceClassifierActorBackfillTests` — `VideoWebPlayer.Tests/MediaSourceClassifierActorBackfillTests.cs`
- `BackfillMissingActorsAsync_LoadsActors_FromMovieDotNfo`
- `BackfillMissingActorsAsync_LoadsActors_FromTvShowNfoInParentCollection`
- `BackfillMissingActorsAsync_SkipsManuallyEditedMovie`
- `BackfillMissingActorsAsync_Continues_WhenCollectionDirectoryIsMissing`
- `BackfillMissingActorsAsync_ReassignsActors_WithoutUniqueConstraintViolation`
Enthält eine weitere private `SftpMediaSourceReader`-Ableitung (Overrides ab ~Zeile 521).

### `ItemsControllerMetadataTests` — `VideoWebPlayer.Tests/ItemsControllerMetadataTests.cs`
- `UpdateMetadata_WhenUserIsNotAdmin_ReturnsUnauthorized` u. a. (5 Tests, nur Metadaten-Validierung, kein Streaming).

### `ItemsControllerAccessTests` — `VideoWebPlayer.Tests/Controllers/ItemsControllerAccessTests.cs`
- `Get_MovieCollection_Without_Access_Returns_Unauthorized`, `Get_MovieCollection_Unlocked_Returns_Ok`, `Get_TVShow_Without_Access_Returns_Unauthorized`, `Get_TVShow_Unlocked_Returns_Ok`, `Get_TVShowEpisode_Without_Access_Returns_Unauthorized`, `Get_TVShowEpisode_Unlocked_Returns_Ok`, `Get_Movie_Without_Access_Returns_Unauthorized`, `Get_Movie_From_Unlocked_Collection_Returns_Ok`.
Nutzt `FakeSftpMediaSourceReader`; Streaming/Download (`GetSftpFileStream`) wird nicht getestet (Methode nicht virtuell/nicht fakebar).

### `SourceVisibilityControllerTests` — `VideoWebPlayer.Tests/Controllers/SourceVisibilityControllerTests.cs`
- `GetSources_Includes_Sources_With_Unlocked_Items`
- `Get_Items_For_Unlocked_Source_Lists_Only_Unlocked_Entries`

### E2E (Category=E2E, anforderungsrelevant)
- `MediaSourceDeleteE2ETests.Admin_Can_Delete_MediaSource_And_It_Disappears` (`VideoWebPlayer.Tests/MediaSourceDeleteE2ETests.cs`)
- `MediaSourceSwitchE2ETests.User_Can_Switch_Source_From_Menu_And_Sees_Only_Selected_Source_Titles` (`VideoWebPlayer.Tests/MediaSourceSwitchE2ETests.cs`)
Beide schlagen im Ausgangslauf fehl (Timeout `#email`, siehe oben).

### `MediaSourceClassifierCollection` — `VideoWebPlayer.Tests/MediaSourceClassifierCollection.cs`
- Collection-Definition `MediaSourceClassifier` mit `DisableParallelization = true` für Classifier-Tests.

## Hilfsmethoden / Test-Helpers

### `FakeSftpMediaSourceReader` — `VideoWebPlayer.Tests/Helpers/FakeSftpMediaSourceReader.cs`
- Erbt `SftpMediaSourceReader`; parametrisierter `rootPath`/`fileName`; überschreibt `ReadRootDirectory`, `ReadDirectoryEntries` (liefert je ein `MediaItem`), `FileExistsAsync`/`ReadFileAsync`/`ReadFileStreamAsync` (liefern `false`/`null`).

### `ThrowingSftpMediaSourceReader` — `VideoWebPlayer.Tests/Helpers/ThrowingSftpMediaSourceReader.cs`
- Erbt `SftpMediaSourceReader`; wirft `SftpPathNotFoundException` in `ReadDirectoryEntries` für einen konfigurierten Pfad.

### `SeriesSftpMediaSourceReader` — `VideoWebPlayer.Tests/Helpers/SeriesSftpMediaSourceReader.cs`
- Erbt `SftpMediaSourceReader`; in-Memory-Dateibaum (`SourceFolder`/`SourceFile`-Structs) mit `AddShow`/`AddSeason`/`AddEpisode`/`AddEpisodePictureFile`; bildet `tvshow.nfo`, `poster.jpg`, `SxxEyy.mp4`/`.nfo`/`-thumb.jpg` ab; überschreibt alle fünf virtuellen Methoden.

### `BackfillSftpMediaSourceReader` — `VideoWebPlayer.Tests/Helpers/BackfillSftpMediaSourceReader.cs`
- Erbt `SftpMediaSourceReader`; Dictionary `(CollectionPath, FileName) → Inhalt` für `FileExistsAsync`/`ReadFileAsync`/`ReadFileStreamAsync`; leere Directory-Ergebnisse.

### `TestableMediaSourceScanService` — `VideoWebPlayer.Tests/Helpers/TestableMediaSourceScanService.cs`
- Erbt `MediaSourceScanService`; `RunAsync` macht `ExecuteAsync` aufrufbar; interne `NullHubClients`/`NullClientProxy` für SignalR.

### `TestHelpers` — `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`
- `WaitForMessageAsync`, `DumpDatabaseStateAsync`, `WaitForMediaCollectionAsync`, `WaitForMediaItemAsync`, `WaitForMediaItemClassifiedAsync`, `WaitForTvShowEpisodeCountAsync`, `WaitForMediaItemCountAsync`, `WaitForMessageCountAsync` (Polling-Utilities), `CreateTvShowWithSeasonsAsync` (DB-Fixture).

### Weitere Helpers
- `IncrementingTimeProvider` — deterministischer `TimeProvider`.
- `ListLogger` — `ILogger` mit Logliste.
- `TestAuthService` — `IAuthService`-Fake.
- `MediaBoxContextMenuE2ETestBase` — WebApplicationFactory+Playwright-Basis für E2E (`LoginAndNavigateToHomeAsync`, Zeile 105).
- `BackupUploadSessionServiceTestSupport`, `ContinueWatchingServiceTestBase` — domänenspezifische Basisklassen.
