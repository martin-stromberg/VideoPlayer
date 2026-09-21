# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-21, ca. 21:40–21:50 lokale Systemzeit (UTC+2, entsprechend `git log`-Zeitzone +0200)
- **Branch und Commit-ID:** `task/issue-220-99c0a1481c9340abb3ee1086374c6498-plattformuebergreifender-backu` @ `8141631b6cf92c0616d57073c2de7c11a3179aa8` (2026-09-17 13:01:06 +0200)
- **Uncommittete Änderungen im getesteten Stand:** nur untracked `docs/features/task/` (Anforderungs-/Inventur-Dokumente); keine Änderungen an Produktivcode, Tests oder Konfiguration
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (lokal), .NET SDK 10.0.401, Runtime .NET 10.0.12, xUnit.net v3 (VSTest Adapter 3.1.5), Playwright 1.49.0 mit installiertem Chromium (E2E lauffähig)
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - `VideoWebPlayer.Tests` (net10.0, xunit.v3, Moq, EF InMemory/Sqlite, `Microsoft.AspNetCore.Mvc.Testing`, Playwright) — Befehle aus `README.md` Zeilen 62–63, `docs/INDEX.md` Zeilen 38–39 und CI `.github/workflows/staging-ci.yml` Zeilen 120–127 (Unit: `Category!=E2E`, Integration: `Category=Integration`, E2E: `Category=E2E`; Traits per `[Trait("Category", ...)]` in den `*E2ETests`-Dateien)
  - `tools/MarkdownLinkCheck.Tests` — zweite dokumentierte Testsuite (`docs/INDEX.md`, `PUBLICATION_CHECKLIST.md`)
- **Vorbereitender Build:** `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` — Exit-Code 0, 0 Fehler, 140 Warnungen (bestehende Nullable-/BL0008-Warnungen, nicht testrelevant). Das MarkdownLinkCheck-Testprojekt wurde implizit durch `dotnet test` gebaut.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit/Nicht-E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "console;verbosity=normal"` | Repo-Root | 0 | 205 | 0 | 0 (im Log keine Übersprungenen gemeldet) | [unit-tests-not-e2e.log](test-results/unit-tests-not-e2e.log) |
| Integration | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=Integration" --logger "console;verbosity=normal"` | Repo-Root | 0 | 0 (kein Test entsprach dem Filter) | 0 | – | [integration-tests.log](test-results/integration-tests.log) |
| E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "console;verbosity=normal"` | Repo-Root | 0 | 34 | 0 | 0 | [e2e-tests.log](test-results/e2e-tests.log) |
| MarkdownLinkCheck | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --logger "console;verbosity=normal"` | Repo-Root | 0 | 6 | 0 | 0 | [markdownlinkcheck-tests.log](test-results/markdownlinkcheck-tests.log) |

### Nachgewiesene bestehende Testfehler

Keine. Alle ausgeführten Tests (205 Nicht-E2E + 34 E2E + 6 MarkdownLinkCheck) bestanden fehlerfrei; der Filter `Category=Integration` traf auf keinen Test (im Repo existieren nur `Category="E2E"`-Traits).

### Testlücken und Ausführungsprobleme

- `Category=Integration` ist im CI definiert, es existiert aber **kein** Test mit diesem Trait — der Lauf war leer (kein Fehler).
- Keine Tests für `VideoWebPlayerBackupFacade.ImportUploadAsync` (Temp-Datei, `manifest.json`-Prüfung, Move) — der bisherige Import-Pfad ist ungetestet.
- Keine Tests für Chunk-Validierung, Offset-Handling oder Resume-Verhalten (diese Mechanismen existieren noch nicht).
- Der Test `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` (`BackupsControllerAuthorizationTests` Zeilen 47–62) assertiert `[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]` — er spiegelt den aktuellen Multipart-Stand und muss bei Umstellung angepasst werden.
- Playwright-E2E-Infrastruktur war lokal vorhanden (Chromium); in Umgebungen ohne Browser-Installation würden die E2E-Tests die Browser-Infrastruktur melden (siehe `Assert.Fail`-Muster in `UpdatesPageE2ETests`/`MediaSourceSwitchE2ETests`).

## Testklassen

### `BackupsControllerAuthorizationTests`
Datei: `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs` — reine Reflexionstests auf Attribute, kein HTTP-Lauf.

- `BackupsController_RequiresAdminOnlyPolicy` — `[Authorize(Policy = "AdminOnly")]` auf Klassenebene
- `DownloadEndpoint_IsOnlyExposedThroughProtectedController` — `[HttpGet("download/{fileName}")]` auf `Download`
- `CreateEndpoint_IsExposedAsServerSidePost` — `[HttpPost("create")]` auf `Create`
- `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` — `[HttpPost("upload")]`, `DisableRequestSizeLimitAttribute` und `RequestFormLimitsAttribute.MultipartBodyLengthLimit == long.MaxValue` auf `Upload`

### `BackupSettingsServiceTests`
Datei: `VideoWebPlayer.Tests/BackupSettingsServiceTests.cs` — EF Core InMemory + InMemory-`IConfiguration`.

- `GetOptionsAsync_UsesConfiguredDefaultsAndPersistsUpdates` — Mapping `Backups:*`-Konfiguration auf `BackupOptions`, Update-Roundtrip inkl. `MaxUploadSizeBytes`
- `GetOptionsAsync_MapsUpdateBackupRetentionFromUpdateSettings` — `ProgramUpdateCount` aus `UpdateSettings`-Zeile
- `GetOptionsAsync_RaisesPersistedLegacyUploadLimitToConfiguredDefault` — Legacy-512-MiB-Wert wird auf konfiguriertes Limit angehoben

### `RestoreBackupJobServiceTests`
Datei: `VideoWebPlayer.Tests/RestoreBackupJobServiceTests.cs`

- `StartRestore_RunsInBackgroundAndRejectsParallelRestore` — Hintergrund-Restore via `BlockingRestoreBackupService` (TaskCompletionSource-Gate), paralleler Start wird abgelehnt, Statusübergänge
- Private Fakes: `BlockingRestoreBackupService` (`IBackupService`), `NoopBackupDataSource`, `NoopBackupOptionsProvider`, `FakeWebHostEnvironment`

### `RestoreInProgressMiddlewareTests`
Datei: `VideoWebPlayer.Tests/RestoreInProgressMiddlewareTests.cs`

- `InvokeAsync_ReturnsStatusJsonForApiRequestsDuringRestore` — 503 + `RetryAfter` + JSON-Body für `/api`-Requests bei aktivem Restore
- `InvokeAsync_AllowsBackupAdminRoutesDuringRestore` — `/admin/backups`-Routen werden durchgelassen (relevant: Upload-Endpunkte liegen unter diesem Segment)
- Private Fakes: `BlockingBackupService`, `NoopBackupDataSource`, `NoopBackupOptionsProvider`, `FakeWebHostEnvironment`

### `VideoWebPlayerAutomaticBackupRunnerTests`
Datei: `VideoWebPlayer.Tests/VideoWebPlayerAutomaticBackupRunnerTests.cs`

- `RunAutomaticBackupAsync_RecordsHistoryAndAppliesRetention` — automatischer Lauf ruft `StoreAsync`/`ApplyRetentionAsync` (`RecordingBackupService`-Fake) und schreibt Historie

### `VideoWebPlayerBackupDataTests`
Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`

- `ReadFromAsync_LegacyBackupWithoutUnlockedMediaWatchedEntriesAndEndThreshold_RestoresSuccessfully` — Restore-Kompatibilität eines Legacy-Backup-Archivs (objektbasiertes ZIP mit `index.json`); nutzt `FakeWebHostEnvironment`

## Hilfsmethoden

### `TestHelpers`
Datei: `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`

- `WaitForMessageAsync` / `WaitForMessageCountAsync` — Warten auf Logzeilen in `ConcurrentQueue<string>` (mit `ListLogger`)
- `DumpDatabaseStateAsync` — Debug-Dump von `MediaCollections`/`MediaItems`/`TVShowEpisodes`
- `WaitForMediaCollectionAsync` / `WaitForMediaItemAsync` / `WaitForMediaItemClassifiedAsync` / `WaitForTvShowEpisodeCountAsync` / `WaitForMediaItemCountAsync` — Pollen auf DB-Zustand (toleriert `SqliteException` 5/locked)
- `CreateTvShowWithSeasonsAsync` — Testdaten-Setup für Serien/Staffeln/Episoden

### Weitere Helfer (`VideoWebPlayer.Tests/Helpers/`)

- `ListLogger` — `ILogger`-Implementierung, die Zeilen in eine `ConcurrentQueue<string>` schreibt
- `IncrementingTimeProvider` — deterministischer `TimeProvider`
- `TestAuthService` — Test-`IAuthService`
- `FakeSftpMediaSourceReader` / `ThrowingSftpMediaSourceReader` / `SeriesSftpMediaSourceReader` / `BackfillSftpMediaSourceReader` — SFTP-Fakes (nicht backup-relevant)
- `MediaBoxContextMenuE2ETestBase` — Basisklasse für Playwright-E2E (hosted App + Headless-Chromium)
- `TestableMediaSourceScanService` — testbare Variante des Scan-Services

### Testkonventionen

- `ApplicationDbContext` wird in Service-Tests mit `UseInMemoryDatabase(Guid)` erzeugt und benötigt einen `EventManager` im Konstruktor; `TestContext.Current.CancellationToken` (xunit.v3) wird als CancellationToken verwendet.
- E2E-Tests tragen `[Trait("Category", "E2E")]` und starten die Anwendung selbst (Environment `"Testing"` deaktiviert den UDP-Discovery-Listener, `Program.cs` Zeile 38).
