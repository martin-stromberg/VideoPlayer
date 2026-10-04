# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-04, ca. 18:57–19:03 Uhr MESZ (`W. Europe Standard Time`, UTC+02:00)
- Branch und Commit-ID: `task/issue-223-bc1b19de3a544d4a9caf1d47bd038839-discovery-server-ist-ueber-mdn` @ `fb76698fe54591dc3df0b4a8b8d905e94c97ef1a` („Update überschreibt web.config — JWT-/Konfigurations-Einträge gehen verloren (#244)")
- Uncommittete Änderungen im getesteten Stand: nur das neue, untracked Feature-Verzeichnis `docs/features/task/issue-223-…/` (`requirement.md`, `todo.md` aus den Lifecycle-Schritten 2–3 sowie die hier abgelegten Testnachweise). Keine Änderungen an getracktem Code.
- Testumgebung und Runtime-/SDK-Versionen: Windows-PC `DESKTOP-CM8OBSG`, .NET SDK `10.0.401`, Testlaufzeit .NET `10.0.12`, xUnit.net v3 (VSTest-Adapter 3.1.5), Microsoft.Playwright `1.62.0` (Chromium-Browser vorinstalliert unter `%LOCALAPPDATA%\ms-playwright`, u. a. `chromium-1243`).
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflow `.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml` — `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release`, dann drei gefilterte `dotnet test`-Läufe (`Category!=E2E`, `Category=Integration`, `Category=E2E`). Zusätzlich existiert die Test-Suite `tools/MarkdownLinkCheck.Tests` im selben Solution-File (`VideoPlayer.sln`); die CI ruft sie nicht explizit auf, sie wurde hier der Vollständigkeit halber ausgeführt. Repo-DoD (`AGENTS.md` §7): volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün, Build in Debug **und** Release.

### Vorbereitender Build

| Befehl | Arbeitsverzeichnis | Exit-Code | Ergebnis |
|--------|--------------------|-----------|----------|
| `dotnet restore VideoPlayer.sln` | Repo-Root | 0 | Alle Projekte wiederhergestellt. |
| `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` | Repo-Root | 0 | 0 Fehler, 140 Warnungen (bekannte, in CI via `WarningsNotAsErrors` ausgenommene Codes wie CS8618, BL0008, CS0436 u. a.). |

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit/ohne E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=unit-test-results.trx" --results-directory …/test-results/unit` | Repo-Root | 0 | 1416 | 0 | 0 | [unit-test-results.trx](test-results/unit/unit-test-results.trx) |
| Integration | `dotnet test … --filter "Category=Integration" --logger "trx;LogFileName=integration-test-results.trx" --results-directory …/test-results/integration` | Repo-Root | 0 | 0 | 0 | 0 | [integration-test-results.trx](test-results/integration/integration-test-results.trx) — **kein Test entspricht dem Filter**; die Kategorie `Integration` existiert in der Suite faktisch nicht (0 Testdateien mit diesem Trait). |
| E2E | `dotnet test … --filter "Category=E2E" --logger "trx;LogFileName=e2e-test-results.trx" --results-directory …/test-results/e2e` | Repo-Root | 0 | 204 | 0 | 1 | [e2e-test-results.trx](test-results/e2e/e2e-test-results.trx) |
| tools/MarkdownLinkCheck | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj -c Release --results-directory …/test-results/tools` | Repo-Root | 0 | 6 | 0 | 0 | [markdownlinkcheck-test-results.trx](test-results/tools/markdownlinkcheck-test-results.trx) |

### Nachgewiesene bestehende Testfehler

Keine. Alle ausgeführten Tests bestanden im Baseline-Lauf.

### Testlücken und Ausführungsprobleme

- **1 dauerhaft übersprungener Test (absichtlich, dokumentierter Produktfehler):**
  `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  (`VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` Zeile 76, `[Fact(Skip = "…")]`):
  `api/continue-watching` antwortet mit HTTP 500, wenn ein Film zu keiner Filmsammlung gehört (`ContinueWatchingService.Create` ohne Null-Prüfung). Der Skip ist im Code begründet und steht nicht im Zusammenhang mit dieser Anforderung. Ein übersprungener Test ist weder Erfolg noch Fehlschlag-Nachweis.
- **Kategorie `Integration` leer:** Der CI-Schritt läuft, findet aber keinen einzigen Test — dokumentiert als „kein Test entspricht dem Filter".
- **Nicht ausgeführt:** `dotnet format --verify-no-changes`, Security-Scan, Coverage-Threshold — gehören zur CI, nicht zum lokalen Testbefehl; hier nicht Teil der Bestandsaufnahme.
- **Keine Discovery-Tests:** Es gibt keinerlei Test für `UdpDiscoveryListener` oder das Discovery-Protokoll (Volltextsuche `UdpDiscovery`/`VIDEOWEBPLAYER` in `VideoWebPlayer.Tests` ohne Quelltreffer). Für mDNS existiert — wie der Anforderung zu entnehmen — bewusst nur der manuelle Prüfpfad (`avahi-browse`/`dns-sd`).

## Testklassen (für diese Anforderung relevant)

Es existiert **keine** Testklasse für den Discovery-Bereich. Relevant sind vielmehr die Infrastruktur- und benachbarten Tests, die ein neues Discovery-Feature berühren oder deren Konventionen es nutzen würde:

### `AutoUpdateProtectedFilesTests`
Datei: `VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs`
- `ConfigurationBindsProtectedFiles` — Bindung der `AutoUpdate:ProtectedFiles`-Konfiguration an `AutoUpdateOptions`.
- `ShippedAppsettingsProtectedFiles_AreValid` — validiert die ausgelieferte `appsettings.json` gegen `AutoUpdateOptionsValidator` (relevant, wenn neue `JsonKeys` ergänzt werden; lädt die Repo-Datei via `FindRepositoryFile`).
- `ShippedWebConfig_ContainsAllMergeParents` — prüft, dass alle XPath-Merge-Eltern in `web.config` existieren.
- `GeneratedScript_ContainsBackupAndMergeInOrder` — prüft Reihenfolge Backup → Kopie → Merge im generierten Update-Skript.

### `KestrelLimitsTests`
Datei: `VideoWebPlayer.Tests/KestrelLimitsTests.cs`
- Tests für `KestrelLimits.ParseMaxRequestBodySize` (gültig/ungültig/negativ) — Konvention für kleine reine Konfigurationsparsing-Tests.

### `ApiDocumentationContractTests` / `ApiDocumentationContractTests_Runtime`
Datei: `VideoWebPlayer.Tests/ApiDocumentationContractTests*.cs`
- Prüfen Konsistenz von `docs/API.md` mit den Controllern bzw. der Laufzeit — relevant, falls `docs/API.md` um einen Discovery-Abschnitt erweitert wird.

## Hilfsmethoden

### `PairingWebApplicationFactory` (`VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`)
- `Create(dbPath, configure)` — `WebApplicationFactory<Program>` mit `EnvironmentName = "Testing"` (Zeile 33), temp. SQLite-DB, generiertem JWT-Key und API-Tokens. Belegt den Mechanismus, der den `Testing`-Guard in `Program.cs` Zeile 51 auslöst.
- `CreateTempDbPath(filePrefix)` — temp. DB-Pfad.

### `PlaylistsE2ETestBase` (`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`)
- Startet die App in E2E-Tests über `UseKestrel()` mit `UseUrls("http://127.0.0.1:0")` (dynamischer Port, Zeilen 56/70) — daher darf das Discovery-Feature in `Testing` keinen festen Port belegen.
- Weitere E2E-Basisklassen analog: `LocalMediaPlaylistE2ETestBase`, `MediaBoxContextMenuE2ETestBase`, `DeviceClientTestBase`, `PairingExchangeContractTestBase`.

### `TestWebHostEnvironment` (`VideoWebPlayer.Tests/Helpers/TestWebHostEnvironment.cs`)
- Minimaler `IWebHostEnvironment`-Stub (`EnvironmentName = "Test"`) für reine Unit-Tests.

### Weitere Helfer
- `CapturingLogger`, `ListLogger` — Logger-Doubles für Services.
- `TestHelpers`, `PairingCryptoHelper`, `PairingTestDb` — allgemeine Testdaten/PKCE-Helfer.
