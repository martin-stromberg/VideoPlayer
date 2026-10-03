# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-03, Läufe ca. 14:20–14:46 +0200 (MESZ)
- Branch und Commit-ID: `task/issue-226-ca48a532dd7347a6b5e56388a105ef7f-update-ueberschreibt-webconfig` @ `6aa578d63d19da9e365381ea1d4f39697626d728` („Backmerge from main to staging (#241)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Dateien unter `docs/features/task/issue-226-…/` (`requirement.md`, `todo.md`, diese Bestandsaufnahme) — kein Produktivcode, keine Teständerungen
- Testumgebung und Runtime-/SDK-Versionen: Windows 10.0.26200 (win-x64), .NET SDK 10.0.401, xUnit v3 3.2.2, Microsoft.Playwright 1.62.0 (Chromium-Cache unter `%USERPROFILE%\AppData\Local\ms-playwright` vorhanden)
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - `VideoWebPlayer.Tests` (xUnit v3): CI-Befehle aus `.github/workflows/staging-ci.yml` Zeilen 114–127 (`--filter "Category!=E2E"`, `Category=Integration`, `Category=E2E`) und DoD-Befehl `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` aus `AGENTS.md`/`docs/GUIDE_Installation.md`
  - Kategorien: nur `[Trait("Category", "E2E")]` ist im Testcode vergeben (63 Treffer); der CI-Lauf `--filter "Category=Integration"` trifft **keinen** einzigen Test — die Kategorie `Integration` existiert im Testcode nicht.
  - `tools/MarkdownLinkCheck.Tests` (zweite, kleinere Suite, in `GUIDE_Installation.md` genannt)

### Build-Nachweis

`dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` — 0 Fehler, 140 Warnungen (v. a. `BL0008` in Razor-Seiten; in CI durch `WarningsNotAsErrors`-Liste abgedeckt).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=inventory-non-e2e.trx"` | Repo-Root | 0 | 1402 | 0 | 0 | [TRX](test-results/inventory-non-e2e.trx), [Konsole](test-results/non-e2e-console.log) |
| 2 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=inventory-e2e.trx"` | Repo-Root | 0 | 204 | 0 | 1 | [TRX](test-results/inventory-e2e.trx), [Konsole](test-results/e2e-console.log) |

Gesamt: 1607 Tests, 1606 bestanden, 1 übersprungen, 0 fehlgeschlagen. Die beiden Läufe zusammen decken die volle Suite ab (Filter `Category!=E2E` + `Category=E2E`).

### Nachgewiesene bestehende Testfehler

**Keine.** Beide Läufe ohne Fehlschlag (TRX-Counters: `failed="0"`).

### Testlücken und Ausführungsprobleme

- **1 absichtlich übersprungener E2E-Test:** `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — statischer `[Fact(Skip = "…")]` (`DevicePlaylistE2ETests_Playback.cs:76`). Dokumentierter, **nicht auftragsbezogener** Produktfehler: `GET api/continue-watching` antwortet mit HTTP 500 statt 200, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.GetListAsync`, `VideoWebPlayer/Services/ContinueWatchingService.cs` — `Create<DtoMovieCollection>(…FirstOrDefault())` übergibt `null`, `NullReferenceException` in `Create<T>`). Der Skip verweist auf einen anderen Auftrag („Wird laut Auftrag in diesem Lauf nicht behoben"); dieser Befund ist weder ein Erfolg noch ein Beleg für einen auftragsbezogenen Fehler.
- **`Category=Integration` ohne Tests:** Die CI-Stufe `dotnet test … --filter "Category=Integration"` (staging-ci.yml:124, pr-staging-ci.yml:144) trifft null Tests, da kein Test dieses Trait trägt. Kein Ausführungsproblem der Suite, aber eine tote CI-Stufe.
- **`tools/MarkdownLinkCheck.Tests` nicht ausgeführt:** Zweite, thematisch nicht betroffene Suite (prüft Markdown-Links); wurde in dieser Bestandsaufnahme nicht gelaufen. Für spätere Doku-Änderungen an verlinkten Dateien relevant — der projektübliche Befehl steht in `GUIDE_Installation.md` Zeile 161.
- E2E-Tests liefen lokal stabil (keine Flakes beobachtet); `AGENTS.md` vermerkt gelegentliches Playwright-Flackern unter Last als bekanntes Phänomen.

## Testklassen (auftragsrelevant)

### `ApiTokenConfigurationTests` (`VideoWebPlayer.Tests/ApiTokenConfigurationTests.cs`)
- `AddVideoWebPlayerServices_ProductionRequiresMauiApiToken` — baut `WebApplication.CreateBuilder` mit `EnvironmentName = Production` und `AddInMemoryCollection` für `Jwt:*`-Werte; erwartet `InvalidOperationException` bei fehlendem `Jwt:ApiToken:Maui`. **Direktes Muster für Konfigurationsquellen-Tests** (In-Memory-Konfiguration + `AddVideoWebPlayerServices`).
- `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` — ungültiges Token wird nicht geloggt (401, kein Secret-Leak ins Log).
- `ApiTokenCheckAttribute_MauiOnly_AcceptsDeviceToken` / `_RejectsRevokedDeviceToken` / `_MauiConfigTokenStillAccepted` — Gerätetoken-Pfad über In-Memory-SQLite (`CreateAttributeFixtureAsync`).
- `ApiTokenCheckAttribute_AnyClient_DoesNotTouchDeviceTokens` — `AnyClient` fragt `IDeviceTokenService` nicht ab.

### `ApiTokenScopeTests` (`VideoWebPlayer.Tests/ApiTokenScopeTests.cs`)
- Scope-bezogene Token-Akzeptanz des `ApiTokenCheckAttribute`.

### `KestrelLimitsTests` (`VideoWebPlayer.Tests/KestrelLimitsTests.cs`)
- `ParseMaxRequestBodySize` für positive Werte, `0`/negativ (unbegrenzt) und nicht-numerische Werte (`InvalidOperationException` mit Schlüsselname). Muster für Konfigurationswert-Parsing-Tests.

### `UpdateSettingsServiceTests` (`VideoWebPlayer.Tests/Services/UpdateSettingsServiceTests.cs`, 8 Fakten)
- `GetOrCreateAsync_UsesConfiguredDefaults` — `AutoUpdate:*`-Konfigurationswerte (In-Memory) → DB-Defaults inkl. `ServiceName`-Trimming.
- `UpdateAsync_AppliesValidValuesToPersistenceAndRuntimeOptions` — Persistenz + Übertrag in `AutoUpdateOptions`.

### `UpdateAdminServiceTests` (`VideoWebPlayer.Tests/Services/UpdateAdminServiceTests.cs`, 7 Fakten)
- Snapshot, manuelle Prüfung/Installation, `IsBusy`/`IsInstallable`-Semantik.

### `UpdateBackupCoordinatorTests` / `UpdateBackupEventBinderTests` / `VideoWebPlayerUpdateBackupServiceTests`
- Backup-vor-Update: deaktivierte Option, fehlender `IUpdateBackupService`, `CancelInstallationOnFailure`-Semantik, Event-Verdrahtung, `ProgramUpdate`-Generation.

### `MsToolsUpdaterIntegrationTests` (`VideoWebPlayer.Tests/Services/MsToolsUpdaterIntegrationTests.cs`)
- `ManualCheckAsync_ClearsPreviousLastErrorInUpdaterStatus` — spannt `Host.CreateApplicationBuilder` + `UseAutoUpdate` mit `UseLocalFolderSource` + `DisableHostedServices` über einem Temp-Verzeichnis auf und löst Updater-Dienste (`AutoUpdateStatusService`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`) per DI auf. **Direktes Muster, um die Updater-DI (inkl. `IAutoUpdateScriptGenerator`) in Tests zu prüfen oder zu übersteuern.**

### `UpdatesPageE2ETests` / `UpdatesControllerAuthorizationTests`
- E2E-Abdeckung der Admin-Update-Seite bzw. `AdminOnly`-Autorisierung der Update-Endpunkte.

## Hilfsmethoden/-klassen

### `PairingWebApplicationFactory` (`VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`)
- `Create(dbPath, configure)` — `WebApplicationFactory<Program>` mit `Environment = "Testing"`, Temp-SQLite und `UseSetting`-Injektion aller `Jwt:*`-Testwerte (`Jwt:Key` zufällig 32 Bytes Base64, `Jwt:Issuer = "VideoWebPlayer.Tests"`, drei API-Token-Konstanten `LegacyApiToken`/`WebApiToken`/`MauiApiToken`). Basis für sämtliche `WebApplicationFactory`-E2E-/Endpunkttests.
- `CreateTempDbPath(prefix)` — eindeutige Temp-DB-Pfade.

### `DeviceClientTestBase` / `PlaylistsE2ETestBase` / `LocalMediaPlaylistE2ETestBase` / `MediaBoxContextMenuE2ETestBase` / `PairingExchangeContractTestBase`
- E2E-Basisklassen: Playwright-Browser, Factory-Aufbau, Seeding-Helfer (`CreateUserAndPairDeviceAsync`, `SeedTwoAccessibleMoviesAsync`, `AssignMovieCollectionAsync`, `CreatePlaylistWithTwoMoviesAsync`, `ContinueWatchingTestHelper.ReportProgressAsync`/`WaitForEntryAsync`).

### `ListLogger<T>` / `CapturingLogger` / `TestHelpers` / `PairingTestDb` / `TestWebHostEnvironment`
- generische Testhilfen (Log-Aufzeichnung, In-Memory-SQLite-Setup, gemockte `IWebHostEnvironment`).

### `MsToolsUpdaterIntegrationTests`-Helfer (klassenintern)
- `ComputeSha256Async`, `GetCurrentPlatform`, `GetCurrentRuntimeIdentifier` — Plattform-/RID-Auflösung für Update-Manifeste in Tests.
