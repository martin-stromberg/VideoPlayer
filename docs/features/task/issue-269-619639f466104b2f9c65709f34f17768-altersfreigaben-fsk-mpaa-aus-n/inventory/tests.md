# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-25, Läufe 17:57:46–17:59:35 (nicht-E2E) bzw. 17:59:53–18:03:58 (E2E), Zeitzone +01:00 (W. Europe Standard Time)
- **Branch und Commit-ID:** `task/issue-269-619639f466104b2f9c65709f34f17768-altersfreigaben-fsk-mpaa-aus-n`, Commit `6b6798fe01c5429efef896e9fee14d5a42effed9` (Stand `2026-10-25 17:05:06 +0100`, `fix: ffmpeg-Download räumt staged *.tmp auch vor Missing-Binaries-Snapshot weg (#283)`)
- **Uncommittete Änderungen im getesteten Stand:** ausschließlich untracked Verzeichnis `docs/features/task/issue-269-619639f466104b2f9c65709f34f17768-altersfreigaben-fsk-mpaa-aus-n/` (requirement.md, todo.md und die hier abgelegten Testnachweise) — kein Produktiv-/Testcode geändert
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (DESKTOP-CM8OBSG), .NET SDK 10.0.402, Target `net10.0`, xunit.v3 3.2.2, Microsoft.Playwright 1.62.0, EF Core 10.0.12, SQLite (In-Memory shared-cache) bzw. EF InMemory je Testklasse; Playwright-Chromium unter `%USERPROFILE%\AppData\Local\ms-playwright` installiert (u. a. `chromium-1248`, `chromium_headless_shell-1248`)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Projekt-Konvention aus `AGENTS.md` (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`) und CI-Workflow `.github/workflows/staging-ci.yml` bzw. `pr-staging-ci.yml` (dort Release-Build + drei Läufe: `Category!=E2E`, `Category=Integration`, `Category=E2E`). Es existieren **keine** Tests mit `Trait("Category","Integration")`; die Suite teilt sich faktisch in „ohne E2E" und „E2E". Vor dem Testlauf wurde `dotnet build VideoPlayer.sln -c Debug` ausgeführt: 0 Fehler, 142 Warnungen (u. a. BL0008 in Account-Pages, CS8602 in `LiveStreamE2ETests.cs`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Build) | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 | – | – | – | Konsolenmitschnitt (0 Fehler, 142 Warnungen) |
| 2 (Nicht-E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --no-build --filter "Category!=E2E" --logger "trx;LogFileName=inventory-none2e.trx" --logger "console;verbosity=normal" --results-directory <feature>/inventory/test-results` | Repo-Root | 0 | 2121 | 0 | 1 | [TRX](test-results/inventory-none2e.trx), [Konsole](test-results/none2e-console.log) |
| 3 (E2E) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --no-build --filter "Category=E2E" --logger "trx;LogFileName=inventory-e2e.trx" --logger "console;verbosity=normal" --results-directory <feature>/inventory/test-results` | Repo-Root | 0 | 225 | 0 | 1 | [TRX](test-results/inventory-e2e.trx), [Konsole](test-results/e2e-console.log) |

Gesamt: 2348 Tests, 2346 erfolgreich, 0 fehlgeschlagen, 2 übersprungen.

Hinweis: Ein erster nicht-E2E-Aufruf mit TRX-Parametern `LogFileName`+`LogFilePrefix` scheiterte an der Logger-Initialisierung (Infrastrukturfehler, kein Testlauf) und wurde ohne diese Kombination wiederholt — keine Auswirkung auf den Testbestand.

### Nachgewiesene bestehende Testfehler

**Keine.** Beide Läufe endeten mit 0 Fehlschlägen (TRX-Counters: `failed="0"`, `error="0"`).

### Testlücken und Ausführungsprobleme

| Test-ID inkl. Testfall | Suite / Dateipfad | Grund / Umfang | Nachweis |
|------------------------|------------------|----------------|----------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | `VideoWebPlayer.Tests/Services/FfmpegToolDownloadServiceTests_Install.cs:77` | Bewusst übersprungen unter Windows (`Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix-Dateirechte lassen sich nur unter Unix prüfen.")`); nicht anforderungsrelevant | TRX `inventory-none2e.trx` (`outcome="NotExecuted"`) |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs:77` | Bewusst übersprungen (`[Fact(Skip = …)]`): dokumentierter **Bestandsfehler** — `GET api/continue-watching` antwortet mit HTTP 500, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.GetListAsync`, `Create<DtoMovieCollection>(null)` → `NullReferenceException`, `ContinueWatchingService.cs:122` bzw. `ApiBaseController.cs:78`); nicht anforderungsrelevant, aber als Dokumentationsbefund zu notieren | TRX `inventory-e2e.trx`; Skip-Begründung im Quellcode Zeilen 67–76 |

Es gibt keine Tests, die `<mpaa>`/`<certification>`-NFO-Elemente, Altersfreigaben oder eine Online-Metadatenquelle prüfen — die für die Anforderung nötigen Tests (NFO-Erfassung, Normalisierung, Priorität, Alt-Backup-Regression, Online-Fehlertoleranz) müssen neu erstellt werden.

## Testklassen (anforderungsrelevant)

### `LocalMediaSourceClassifierTests`
Datei: `VideoWebPlayer.Tests/LocalMediaSourceClassifierTests.cs` — `[Collection(MediaSourceClassifierCollection.Name)]`, echte SQLite-In-Memory-DB + `LocalMediaSourceReader` auf Temp-Verzeichnis.

- `ClassifyAllAsync_ClassifiesTvShow_FromLocalSource` — End-to-End-Scan: `tvshow.nfo` + `episode01.nfo` → `TVShow` (Name, GenreNames), `TVShowSeason` („Staffel 01"), `TVShowEpisode` erzeugt. **Natürlicher Ort für NFO-Rating-Testfälle.**

### `LocalMediaSourceClassifierTests_Trailer`
Datei: `VideoWebPlayer.Tests/LocalMediaSourceClassifierTests_Trailer.cs`

- `ClassifyAllAsync_TrailerSuffix_LinksToMovie_NotAsMovie` — `-trailer`-Suffix erzeugt keinen Film, sondern `MovieMediaItem.IsTrailer`-Link
- `ClassifyAllAsync_TrailersFolder_LinksToParentMovie` — `trailers/`-Ordner verknüpft mit Filmen der Eltern-Collection
- `ClassifyAllAsync_TrailerSuffix_IsNeverRepresentativeMediaItem` — Trailer ist nie Hauptdatei

### `MediaSourceClassifierActorBackfillTests`
Datei: `VideoWebPlayer.Tests/MediaSourceClassifierActorBackfillTests.cs` — `[Collection(MediaSourceClassifierCollection.Name)]`, SQLite-In-Memory + `BackfillSftpMediaSourceReader` (Fake-Reader mit Datei-Map).

- `BackfillMissingActorsAsync_LoadsActors_FromMovieDotNfo` — `movie.nfo`-Fallback im Backfill-Pfad
- `BackfillMissingActorsAsync_LoadsActors_FromTvShowNfoInParentCollection` — `tvshow.nfo`-Fallback über Parent-Collection
- `BackfillMissingActorsAsync_SkipsManuallyEditedMovie` — `IsManuallyEdited`-Schutz im Backfill (Vorbild für „kein Überschreiben manueller Werte")
- `BackfillMissingActorsAsync_Continues_WhenCollectionDirectoryIsMissing` — Fehlertoleranz
- `BackfillMissingActorsAsync_ReassignsActors_WithoutUniqueConstraintViolation` — Neu-Zuordnung ohne Unique-Verletzung

### `MediaSourceClassifierGenreInheritanceTests`
Datei: `VideoWebPlayer.Tests/MediaSourceClassifierGenreInheritanceTests.cs` — `ReloadGenres`-Normalisierung/Vererbung (Genre-Vorlagen, Alternativnamen, saisonale Fenster). Muster für Normalisierungslogik-Tests.

### `MediaSourceClassifierBackgroundImageTests`
Datei: `VideoWebPlayer.Tests/MediaSourceClassifierBackgroundImageTests.cs` — Episoden-Hintergrundbilder beim Scan (4 Tests).

### `MediaSourceScanServiceTests`
Datei: `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs` — Scan-Service-Orchestrierung (u. a. `ExecuteAsync_ClassifiesEpisodes_WhenSeriesStructureExists`); nutzt `TestableMediaSourceScanService`.

### `MediaMetadataEditorServiceTests` (Verzeichnis `Services/`)
Datei: `VideoWebPlayer.Tests/Services/MediaMetadataEditorServiceTests.cs` — EF InMemory.

- `UpdateMovieAsync_SavesMetadataAndCreatesNewGenres`, `UpdateTVShowAsync_SavesGenresAndRejectsPremieredAt`, `UpdateTVShowEpisodeAsync_SavesExistingEpisodeFieldsOnly`, `UpdateMovieCollectionAsync_SavesReleaseDateAndRejectsGenres`, `UpdateTVShowSeasonAsync_RejectsPlotAndGenres`, `GetGenreOptionsAsync_ReturnsDistinctSortedGenreNames`, `UpdateAsync_RejectsPremieredAtForReleaseDateTypes`, `UpdateAsync_RejectsReleaseDateForPremieredAtTypes`, `UpdateAsync_RejectsInvalidIdsAndNames`, `UpdateAsync_RejectsTooLongName`, `UpdateAsync_EntersBackgroundProcessingGate`, `UpdateAsync_WaitsForExclusiveMetadataWriteCoordinator`

### `MediaMetadataEditorServiceGenreInheritanceTests`
Datei: `VideoWebPlayer.Tests/MediaMetadataEditorServiceGenreInheritanceTests.cs` — Editor-Genre-Vererbung/-Normalisierung.

### `ItemsControllerMetadataTests`
Datei: `VideoWebPlayer.Tests/ItemsControllerMetadataTests.cs` — Controller-Unit-Tests mit Moq-Diensten + EF InMemory.

- `UpdateMetadata_WhenUserIsNotAdmin_ReturnsForbidden` — Admin-Erzwingung (403)
- `UpdateMetadata_WhenDateFieldDoesNotMatchType_ReturnsBadRequest`, `UpdateMetadata_WhenIdIsInvalid_ReturnsBadRequest`, `UpdateMetadata_WhenNameIsTooLong_ReturnsBadRequest` — Validierung
- `UpdateMetadata_WhenMovieCollectionUpdateIsValid_ReturnsOk` — prüft u. a. `IsManuallyEdited = true` nach Edit

### `MetadataEditModelValidationTests`
Datei: `VideoWebPlayer.Tests/MetadataEditModelValidationTests.cs` — DataAnnotations-Validierung des Edit-Modells (Titel leer/zu lang, Plot zu lang).

### `MetadataEditorUiStateTests`
Datei: `VideoWebPlayer.Tests/MetadataEditorUiStateTests.cs` — **Quelltext-Asserts** auf `TVShowDetails.razor`/`MovieCollectionDetails.razor` (liest die Dateien vom Repo-Root aus): EditForm/Validation/Dirty-Dialog-Struktur, Genre-Sortierung, Episoden-Datum, Auth-Guard. **Achtung:** Markup-/Code-Änderungen an diesen Seiten können diese Tests treffen.

### `VideoWebPlayerBackupDataTests` + Varianten
Dateien: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests*.cs` (u. a. `_DevicePairing`, `_Trailer`, `_StrandKey`, `_TvSenderEnabled`, `_TvSenderUserAccess`, `_ClipGroups`, `_ProgramRuleLinks`) — echte SQLite-In-Memory-DBs.

- Muster: `ReadFromAsync_LegacyBackupWithoutX_RestoresSuccessfully` — `LegacyBackupArchiveBuilder.RemoveTablesAsync`/`RemoveColumnsAsync` baut Alt-Archive; Assert auf `null`-Exception + Default-Werte
- `ReadFromAsync_LegacyBackupWithoutIsTrailerColumn_RestoresSuccessfully` (`_Trailer`) — direktes Vorbild für den Pflicht-Alt-Backup-Regressionstest der neuen Freigabe-Spalten
- `WriteToAndReadFromAsync_*` — Roundtrip-Tests

## Hilfsmethoden

### `LegacyBackupArchiveBuilder`
Datei: `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs` (`internal static`)

- `CreateSeededBackupAsync(SqliteConnection, ct)` — seeded DB (Admin-User, Setup) + `VideoWebPlayerBackupData`-Instanz
- `RemoveTablesAsync(backup, tableNames, ct)` — Alt-Archiv ohne Tabellen
- `RemoveColumnsAsync(backup|stream, tableName, columnNames, ct)` — Alt-Archiv ohne Spalten (Index + Payload)
- `AppendRowsAsync(...)` — Zeilen an Tabellen-Payload anhängen
- `ReadTableColumnsAsync(stream, tableName, ct)` — Spaltenliste aus `index.json` lesen

### `BackfillSftpMediaSourceReader` / `FakeSftpMediaSourceReader` / `SeriesSftpMediaSourceReader` / `ThrowingSftpMediaSourceReader`
Dateien: `VideoWebPlayer.Tests/Helpers/` — Fake-`IMediaSourceReader`-Implementierungen; `BackfillSftpMediaSourceReader` nimmt eine `(path, fileName) → content`-Map entgegen (NFO-Simulation ohne Dateisystem).

### `ItemsControllerTestFactory`, `FakeAuthService`, `TestAuthService`, `NoOpVideoWebPlayerClient`, `TestWebHostEnvironment`, `TestImages`, `TestHelpers`, `SqlCommandRecorder`
Dateien: `VideoWebPlayer.Tests/Helpers/` — Controller-/Client-/Umgebungs-Doubles. `NoOpVideoWebPlayerClient` muss ggf. um neue Client-Methoden erweitert werden (bUnit-/E2E-Nutzung).

### E2E-Infrastruktur
`LocalMediaPlaylistE2ETestBase`, `MediaBoxContextMenuE2ETestBase`, `TVSenderAdminE2ETestBase`, `PairingWebApplicationFactory`, `PairingTestDb`, `ProgramScheduleTestDb` — `WebApplicationFactory`-basierte E2E-Tests mit Playwright (`Category="E2E"`, 226 Tests). NFO-/Scan-bezogen: `LocalMediaSourcePipelineE2ETests`, `LocalMediaSourceLocalDirectoryE2ETests`-Familie.

### Weitere relevante Suites
- `EnumContractTests` — pinnen Vertrags-Enums (Muster für einen evtl. Rating-Enum)
- `ApplicationDbContextTests` — DbContext-Verhalten
- `LocalMediaSourcePipelineE2ETests` — Scan→Klassifizierung→API-Durchstich gegen lokale Quelle
