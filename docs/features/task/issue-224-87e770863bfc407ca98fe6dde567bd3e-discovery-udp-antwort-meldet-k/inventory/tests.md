# Tests — Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-05, ca. 05:45–05:50 Uhr MESZ (UTC+02:00)
- Branch und Commit-ID: `task/issue-224-87e770863bfc407ca98fe6dde567bd3e-discovery-udp-antwort-meldet-k`, Commit `9b328d023734208355f705ad5c1774cc663aead5`
- Uncommittete Änderungen im getesteten Stand: nur untracked Dateien unter
  `docs/features/task/issue-224-.../` (`requirement.md`, `todo.md` sowie die bei dieser
  Bestandsaufnahme angelegten `inventory`-Dateien); keine Änderungen an Produktivcode,
  Tests oder Testkonfiguration.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.401, Zielframework
  `net10.0`; xunit.v3 3.2.2, Microsoft.Playwright 1.62.0 (Chromium lokal installiert unter
  `%USERPROFILE%\AppData\Local\ms-playwright`), Moq 4.20.72, bunit 2.11.3,
  Microsoft.AspNetCore.Mvc.Testing 10.0.12. E2E-Tests laufen gegen
  `WebApplicationFactory<Program>` mit `UseEnvironment("Testing")` und Kestrel auf
  `http://127.0.0.1:0` (vgl. `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`).
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - Hauptsuite laut `AGENTS.md` (Definition of Done):
    `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig,
    inkl. E2E). CI (`.github/workflows/staging-ci.yml` Zeilen 114–127) teilt denselben
    Lauf in drei Filter (`Category!=E2E`, `Category=Integration`, `Category=E2E`, jeweils
    Release-Build mit vorherigem `playwright.ps1 install --with-deps chromium`).
  - Zweite, kleinere Suite im Repository: `tools/MarkdownLinkCheck.Tests` (xunit, 6 Tests);
    wird in keinem CI-Workflow referenziert.
  - `dotnet build VideoPlayer.sln` in Debug UND Release ist laut `AGENTS.md` Teil der
    Definition of Done.

### Vorab geprüfter Build

`dotnet build VideoPlayer.sln -c Debug` (Repo-Root): 0 Fehler, 140 Warnungen
(bestehende Warnungen, u. a. `BL0008`, `CS86xx`), ~6 s. Release-Build wurde in dieser
Bestandsaufnahme nicht ausgeführt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Basislauf (volle Suite, Debug) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --no-build --logger "trx;LogFileName=inventory-baseline.trx" --logger "console;verbosity=normal"` | Repo-Root `D:\Repositories\softwareschmiede\87e77086-3bfc-407c-a98f-e6dde567bd3e` | 0 | 1665 | 0 | 1 | [dotnet-test-baseline.log](test-results/dotnet-test-baseline.log), [inventory-baseline.trx](test-results/inventory-baseline.trx) |
| Tool-Suite | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj -c Debug --no-build` | Repo-Root | 0 | 6 | 0 | 0 | Konsolenausgabe (kein Report erzeugt) |

Gesamtdauer Basislauf: 4,0 Minuten.

### Nachgewiesene bestehende Testfehler

Es wurden **keine Testfehler** nachgewiesen — alle ausgeführten Tests des Basislaufs
bestanden.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| – | – | – | – |

### Testlücken und Ausführungsprobleme

- `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  wurde übersprungen — absichtlich per `[Fact(Skip = "...")]`
  (`VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` Zeilen 76–77). Begründung im
  Attribut: bekannter Produktfehler (`api/continue-watching` antwortet mit 500, wenn ein
  Film zu keiner Filmsammlung gehört), wird in jenem Auftrag nicht behoben.
- Keine Infrastruktur-, Build- oder Setupfehler aufgetreten. Playwright/Chromium lief
  lokal; alle E2E-Tests wurden ausgeführt.
- Für `UdpDiscoveryListener` existieren keinerlei Tests — der Socket-Versand selbst ist
  aktuell nur manuell verifizierbar (Anforderung nennt Unit-Tests für die künftige
  Ableitungslogik als Ziel).
- Release-Build sowie die CI-Filteraufteilung (`Category!=E2E` / `Integration` / `E2E`)
  wurden nicht separat ausgeführt; der lokale Volllauf ohne Kategoriefilter deckt alle
  Tests ab.

## Testklassen (discovery-/konfigurationsnah)

### `MdnsServiceProfileBuilderTests`
Datei: `VideoWebPlayer.Tests/MdnsServiceProfileBuilderTests.cs`

- `Build_UsesDefaults_WhenNothingConfigured` — Defaults: Instanzname, Diensttyp, Port 5000, TXT-Records
- `Build_PrefersMdnsPort_OverAllSources` — `Mdns:Port` schlägt gebundene Adresse, Kestrel-URL und `Host:Port`
- `Build_UsesBoundServerAddressPort` — Port aus gebundener Serveradresse
- `Build_UsesKestrelEndpointUrlPort` — Port aus `Kestrel:Endpoints:Http:Url`
- `Build_UsesHostPortFallback` — Port aus `Host:Port`
- `Build_UsesConfiguredServiceType` / `Build_UsesConfiguredInstanceName` — Optionen-Durchgriff

### `MdnsAdvertiserWorkerTests`
Datei: `VideoWebPlayer.Tests/MdnsAdvertiserWorkerTests.cs`

- `IsAdvertisementEnabled_RequiresBothSwitches` (Theory, 4 Fälle) — Konjunktion beider Schalter
- `ExecuteAsync_ConfigDisabled_ExitsWithoutPollingAdminSwitch` — kein DB-Polling bei `Mdns:Enabled=false`
- `ExecuteAsync_AdminSwitchInitialReadFails_DoesNotAdvertise` — Fail-closed bei Lesefehler; `IServer.Features`-Zugriff als Advertise-Indikator per Mock

### `MdnsRegistrationTests`
Datei: `VideoWebPlayer.Tests/MdnsRegistrationTests.cs`

- `AddVideoWebPlayerServices_DoesNotRegisterWorker_InTesting` — kein `IHostedService` unter `Testing`
- `AddVideoWebPlayerServices_RegistersWorker_OutsideTesting` — Registrierung unter `Development`

### `MdnsConfigurationTests`
Datei: `VideoWebPlayer.Tests/MdnsConfigurationTests.cs`

- `MdnsSection_BindsToOptions` — Binding `Mdns`-Sektion auf `MdnsOptions`

### `MdnsOptionsValidatorTests`
Datei: `VideoWebPlayer.Tests/MdnsOptionsValidatorTests.cs`

- `Validate_AcceptsDefaults`, `Validate_RejectsInvalidPort` (Theory), `Validate_RejectsInvalidServiceType` (Theory), `Validate_AcceptsValidServiceType` (Theory), `Validate_RejectsEmptyInstanceName` (Theory), `Validate_RejectsInstanceNameLongerThan63Characters`, `Validate_AcceptsEmptyInstanceName_WhenDisabled`

### `AutoUpdateProtectedFilesTests`
Datei: `VideoWebPlayer.Tests/Services/AutoUpdateProtectedFilesTests.cs`

- `ConfigurationBindsProtectedFiles` — Binding der `ProtectedFiles`-Optionen inkl. `JsonKeys`
- `ShippedAppsettingsProtectedFiles_AreValid` — validiert die ausgelieferte `appsettings.json` gegen `AutoUpdateOptionsValidator` (fängt fehlende/unzulässige Merge-Einträge ab)
- `ShippedWebConfig_ContainsAllMergeParents` — XPath-Merge-Eltern existieren in `web.config`
- `GeneratedScript_ContainsBackupAndMergeInOrder` — Update-Skript enthält Backup → Copy → Merge in Reihenfolge

### `KestrelLimitsTests`
Datei: `VideoWebPlayer.Tests/KestrelLimitsTests.cs`

- Tests für `KestrelLimits.ParseMaxRequestBodySize` — Muster für netzwerkfreie Tests einer reinen Konfigurations-Parse-Logik

**Keine Testklasse für `UdpDiscoveryListener` vorhanden** (Repository-weite Suche nach
`UdpDiscoveryListener`, `VIDEOWEBPLAYER_DISCOVERY`, `VIDEOWEBPLAYER_SERVER` ergab außer
Produktivcode und Doku keine Treffer in `VideoWebPlayer.Tests/`).

## Hilfsmethoden

### `MdnsServiceProfileBuilderTests` (lokal)
- `EmptyConfiguration()` / `CreateConfiguration(Dictionary<string,string?>)` — `ConfigurationBuilder().AddInMemoryCollection(...)` für netzwerkfreie Config-Tests

### `MdnsAdvertiserWorkerTests` (lokal)
- `CreateWorker(bool enabled, Mock<IServiceScopeFactory>, TaskCompletionSource? featuresAccessed, out ...)` — Worker mit `IOptions<MdnsOptions>`, leerem `IConfiguration`, `IHostApplicationLifetime`-Mock (`ApplicationStarted` bereits signalisiert), `IServer`-Mock, `NullLogger`

### `PlaylistsE2ETestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

- `WebApplicationFactory<Program>` mit `UseEnvironment("Testing")`, `UseUrls("http://127.0.0.1:0")`, Temp-SQLite-DB, `Jwt:Key`/`Jwt:ApiToken`-Testwerte — Beleg, dass die `Testing`-Abschirmung von `UdpDiscoveryListener`/`MdnsAdvertiserWorker` E2E-Starts auf dynamischen Ports ermöglicht
- `Playwright.CreateAsync()` + `Chromium.LaunchAsync(Headless = true)`; `SkipBrowser`-Fallback bei fehlendem Browser

### `TestWebHostEnvironment` / `PairingWebApplicationFactory` / `ItemsControllerTestFactory`
Dateien: `VideoWebPlayer.Tests/Helpers/`

- Wiederverwendbare Factory-/Umgebungs-Helfer für `WebApplicationFactory`-basierte Integrations- und E2E-Tests (Details nicht discovery-relevant)
