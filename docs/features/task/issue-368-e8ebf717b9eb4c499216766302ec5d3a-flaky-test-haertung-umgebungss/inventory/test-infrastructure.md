# Bestandsaufnahme: Trait-/Kategorien- und Test-Infrastruktur

Stand der Trait-Infrastruktur, Test-Basisklassen, Fixtures und Dispose-/Cleanup-Muster in `VideoWebPlayer.Tests/` vor der Umsetzung von Issue #368.

## Traits und Kategorien

**Vorhanden:** ausschließlich `[Trait("Category", "E2E")]` — 79 Vorkommen in 53 Dateien (Klassen- und Methodenebene; z. B. `BackupUploadE2ETests.cs` 11×, `UpdatesPageE2ETests.cs` 7×, `DevicePairingE2ETests.cs` 7×).

**Nicht vorhanden:**

- `Trait("Category", "Integration")` — 0 Treffer im gesamten Testprojekt. Der CI-Schritt `Run integration tests` (`--filter "Category=Integration"`, `staging-ci.yml` Z. 121–122 bzw. `pr-staging-ci.yml` Z. 143–144) matcht daher **0 Tests** — er ist ein No-op, keine Doppelausführung (Korrektur zum Issue-Text: die behauptete Doppelausführung via `Category!=E2E ⊃ Integration` findet nicht statt, weil es schlicht keine `Integration`-Tests gibt; der Nebenbefund greift erst, sobald `Integration`-Traits vergeben werden).
- `Trait("Category", "OsInterface")` / `Flaky` — 0 Treffer.
- `ITraitAttribute`-Implementierungen — 0 Treffer. `xunit.v3` 3.2.2 ist referenziert (`VideoWebPlayer.Tests.csproj` Z. 18) und stellt `Xunit.v3.ITraitAttribute` bereit — Voraussetzung für die Blaupausen-Attribute erfüllt.
- Konstanten-Klasse `TestCategories`, Meta-Test `FlakyConventionTests`, `TempDirectoryCleanup` — nicht vorhanden.
- 8 Playwright-Klassen tragen **keinen** `E2E`-Trait (Details: `os-interface-audit.md`, Kategorie A).

## Test-Basisklassen und Fixtures (`VideoWebPlayer.Tests/Helpers/`)

### Gehostete Anwendung / Browser

| Klasse | Datei | Pattern |
|--------|-------|---------|
| `PlaylistsE2ETestBase` | `Helpers/PlaylistsE2ETestBase.cs` | `IAsyncLifetime`; `WebApplicationFactory` + `UseKestrel()` + `StartServer()` auf `http://127.0.0.1:0` (Z. 57, 85–90); temp SQLite-Datei-DB (Z. 48); Chromium headless (Z. 96–100); `SkipBrowser`-Fallback bei `PlaywrightException` (Z. 102–106); `DriveUntilStateAsync` 60-s-Deadline/10-s-Versuch (Z. 170–211); `DisposeAsync` Z. 109–121: Browser-Close, Factory-Dispose, `File.Delete(_dbPath)` geschützt |
| `MediaBoxContextMenuE2ETestBase` | `Helpers/MediaBoxContextMenuE2ETestBase.cs` | gleiches Muster (Z. 39, 48, 62–97); `DisposeAsync` Z. 86–97 geschützt |
| `TVSenderAdminE2ETestBase` | `Helpers/TVSenderAdminE2ETestBase.cs` | erbt `PlaylistsE2ETestBase`; `SwitchTabAsync` über `DriveUntilStateAsync` (Z. 190–201) |
| `LocalMediaPlaylistE2ETestBase` | `Helpers/LocalMediaPlaylistE2ETestBase.cs` | `IAsyncLifetime`; `PairingWebApplicationFactory` (TestServer, **kein** echter Port); temp DB + echtes `RootDirectory` (Z. 54–56); `DisposeAsync` Z. 86–92: beide Deletes best-effort mit gefiltertem Catch (`IOException`/`UnauthorizedAccessException`/`DirectoryNotFoundException`) |
| `DeviceClientTestBase` | `Helpers/DeviceClientTestBase.cs` | `IAsyncLifetime`; `PairingWebApplicationFactory` (TestServer); temp DB via `CreateTempDbPath` (Z. 34) |
| `PairingWebApplicationFactory` | `Helpers/PairingWebApplicationFactory.cs` | `static`; `CreateTempDbPath(filePrefix)` → `Path.GetTempPath()/{prefix}-{guid}.db` (Z. 24–25); `Create(dbPath, configure)` mit `Data Source={dbPath}` (Z. 34) — **Temp-DB-Dateien werden von den Nutzern gelöscht, nicht von der Factory** |
| `PairingExchangeContractTestBase` | `Helpers/PairingExchangeContractTestBase.cs` | `PairingWebApplicationFactory.Create` (Z. 24–25) |
| `FfmpegPackageServer` | `Helpers/FfmpegPackageServer.cs` | `IAsyncDisposable`; echter Kestrel-Loopback `Listen(Loopback, 0)` (Z. 51–54) via `WebApplication.CreateEmptyBuilder`; `TransferDelayMilliseconds` für Fortschritts-Szenarien (Z. 42, 79–85) |
| `E2EBinaryLocator` | `Helpers/E2EBinaryLocator.cs` | sucht `ffmpeg(.exe)`/`ffprobe` auf `PATH` (Z. 12–24); `null` → Test kehrt still zurück |

### In-Memory-Datenbank-Fixtures

| Klasse | Pattern |
|--------|---------|
| `PairingTestDb` | `Data Source=file:{dbName}?mode=memory&cache=shared` (Z. 104), Keeper-Connection |
| `ProgramScheduleTestDb` | gleiches Muster (Z. 29); `CreateAsync` + `Dispose` |
| `PlaylistServiceTestBase` | `IDisposable`; gleiches Muster (Z. 32–38) — Basis u. a. für `ContinueWatchingPlaylistTestBase` |
| `PlaylistsControllerTestBase` | gleiches Muster (Z. 38–48) |
| `ItemsControllerTestFactory` | Factory für in-memory SQLite + Dienst-Sammelaufbau |

### Zeit-Fakes

| Klasse | Zweck |
|--------|-------|
| `ControllableTimeProvider` | steuerbarer `TimeProvider` (`TickTimers()`) für Worker-Polling |
| `ManualTimeProvider`, `IncrementingTimeProvider`, `ScheduleTestTimeProvider` | deterministische Zeit für Schedule-/Service-Tests |

### Weitere relevante Helpers

- `TestHelpers.cs` — `WaitFor*`-Poll-Familie (10-ms-Intervall, Caller-Timeout), `DenyReadAccess`/`ResetAccessControl` (Windows `icacls` / Unix `SetUnixFileMode`, Z. 261–303), `DumpDatabaseStateAsync` (Diagnose).
- `ListLogger`/`CapturingLogger` — Log-Capture für `WaitForMessage*`-Assertions.
- `BackupUploadSessionServiceTestSupport` — `CreateProvider`/`CreateService`, `DisposeSessionAsync` (Z. 27–35: `File.Delete` **ungeschützt**), `TempDirectory`-Klasse (Z. 41–71: `Directory.Delete` best-effort geschützt Z. 59–70).
- `FfmpegToolDownloadTestDoubles` — `TestDirectory`-Artige Temp-Ordner (Z. 280 f.), HTTP-Handler-Stubs (`BytesHandler`, `ThrottledStream`, `ExceptionHandler`, `StalledStream`).
- `MediaProbeTestDoubles`, `FakeSftpMediaSourceReader`, `FakeTmdbAgeRatingService`, `FakeAuthService`, `FakeBackgroundProcessingGate`, `FakeProgramScheduleService`, `FakeScheduleSignal`, `GatedMetadataWriteCoordinator` — Fakes/Doubles.
- `TestWebHostEnvironment` — `IWebHostEnvironment`-Stub mit `ContentRootPath` (Temp-Root für Segment-Stores).
- `SetupSeedHelpers`, `PrimetimeSeed`, `TestImages`, `SqlCommandRecorder`, `UnreadableFileReader`, `FailingReadStream`, `TruncatingReadStream` — Seed-/Stream-Fixture-Daten.

## Dispose-/Cleanup-Muster

### Ungeschütztes `Directory.Delete` (wirft in Dispose/DisposeAsync — kann Assert überdecken)

`Controllers/LiveStreamControllerTests.cs:65` · `Extensions/LocalConfigurationTests.cs:128` · `LiveStreamCoordinatorTests.cs:128` · `LiveStreamWorkerTests.cs:116` · `Services/AutoUpdateProtectedFilesTests.cs:167` · `Services/Backups/VideoWebPlayerBackupFacadeTests.cs:152` · `Services/FfmpegLocatorTests.cs:19` · `Services/LiveStreamMediaResolverTests.cs:65` · `Services/LiveStreamSegmentStoreTests.cs:49` · `Services/LiveStreamStandbyProviderTests.cs:37` · `Services/LiveStreamStandbySegmenterTests.cs:131` · `Services/MsToolsUpdaterIntegrationTests.cs:113` · `Services/StaticAssetVersionerTests.cs:17` · `Services/UpdateBackupCoordinatorTests.cs:27` · `Services/UpdateBackupEventBinderTests.cs:27` · `Services/VideoWebPlayerUpdateBackupServiceTests.cs:117`

### Ungeschütztes `File.Delete` in Cleanup-Pfaden

`Helpers/BackupUploadSessionServiceTestSupport.cs:34` (`DisposeSessionAsync` — **Korrektur zur Requirement:** dort wird „ungeschütztes `Directory.Delete` in `Dispose` (Z. 59–64)" behauptet; tatsächlich ist Z. 63–64 innerhalb `try/catch` geschützt — die ungeschützte Stelle ist das `File.Delete` in Z. 34).

### Geschützte Deletes (best-effort, zu vereinheitlichen)

~50 Stellen `try { Directory.Delete(…, recursive: true); } catch {…}` bzw. `try { File.Delete(_dbPath) } catch {…}` — Varianten: blanker `catch {}` (z. B. `MediaProbeHttpIntegrationTests.cs:76–77`, `PlaylistsE2ETestBase.cs:49,120`), gefilterter Catch auf `IOException`/`UnauthorizedAccessException`/`DirectoryNotFoundException` (`LocalMediaPlaylistE2ETestBase.cs:89–90`, `LocalMediaPlaylistE2ETests_SourceDeletion.cs:47`, `ItemsControllerMediaFileTypeTests.cs:35`), `BackupUploadSessionServiceTestSupport.cs:59–70` mit `Directory.Exists`-Vorprüfung.

### Prozess-Kill im Cleanup

Kein `Kill(entireProcessTree: true)`-Muster in Test-`Dispose`/`DisposeAsync`/`finally` — die einzigen `Kill()`-Fundstellen sind `ISegmenterProcess`-Fake-Implementierungen. Echte Prozesse werden ausschließlich über `Segmenter.StopAsync`/`WaitForExitAsync` beendet (ffmpeg-E2E) oder sind kurzlebig (`icacls`, `mklink`, ffmpeg-Generatoren mit `WaitForExit(10_000)`/`WaitForExitAsync`). Ein langlebiger ffmpeg-Kindprozess, der nach einem Testabbruch überlebt, wird heute **nicht** im Cleanup gekillt — Befund für die Cleanup-Härtung.

## `IAsyncLifetime`-Nutzung

32 Dateien nutzen `IAsyncLifetime` (Initialize/DisposeAsync) — davon 19 gehostete E2E-Fixtures (Kestrel oder TestServer) und 13 Service-/Controller-Fixtures mit Temp-Roots (`LiveStreamCoordinatorTests`, `LiveStreamWorkerTests`, `Controllers/LiveStreamControllerTests`, `Controllers/ProgramGuideControllerTests`, `Services/LiveStream*Tests`, `ProgramSettingsService*Tests`, `PairingBootstrapEndpointsTests`).

## Fehlende Artefakte (zu erzeugen, Blaupausen-Namen)

- `Infrastructure/Testing/TestCategories.cs` — Verzeichnis `Infrastructure/` existiert im Testprojekt nicht (Struktur: `ClientLibrary/`, `Components/`, `Controllers/`, `Extensions/`, `Helpers/`, `Services/`, `Utils/`).
- `Infrastructure/Testing/OsInterfaceAttribute.cs`, `FlakyAttribute.cs` (ggf. `OsInterfaceFactAttribute`/`OsInterfaceTheoryAttribute` — Blaupause hat sie).
- `Infrastructure/Testing/FlakyConventionTests.cs`.
- `Helpers/TempDirectoryCleanup.cs` (+ Tests).
- `scripts/Retry-FlakyTests.ps1`, `scripts/Analyze-FlakyTestResults.ps1`, `scripts/lib/{Trx,TestDiscovery}.ps1` — `scripts/` existiert, enthält nur `resolve-release-version.mjs` (Node).
- `docs/help/stabilitaet/` — Verzeichnis existiert nicht (`docs/help/` enthält 26 Einträge, kein `stabilitaet`).
