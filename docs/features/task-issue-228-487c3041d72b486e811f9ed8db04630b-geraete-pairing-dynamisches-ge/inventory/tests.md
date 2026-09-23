# Tests — Bestandsaufnahme und Test-Ausgangszustand

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-23, Läufe zwischen 06:43 und 06:46 MESZ (+02:00); Startzeiten laut TRX: Unit-Filter 06:43:41 +02:00, Integration-Filter 06:44:09 +02:00, E2E-Filter 06:44:36 +02:00.
- **Branch und Commit-ID:** `task/issue-228-487c3041d72b486e811f9ed8db04630b-geraete-pairing-dynamisches-ge`, HEAD `bfa8efd360880c566a3aa5d232ea5b6e1b834988` („Lokale Verzeichnisse (#225)", 2026-09-23 05:00:45 +0200).
- **Uncommittete Änderungen im getesteten Stand:** Keine geänderten getrackten Dateien (`git status --porcelain` zeigt nur `?? docs/features/task-issue-228-487c3041d72b486e811f9ed8db04630b-geraete-pairing-dynamisches-ge/` — das neue Feature-Verzeichnis mit `requirement.md`, `todo.md` und den hier entstehenden Inventory-Artefakten). Der getestete Code entspricht also HEAD.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (Host `DESKTOP-CM8OBSG`), .NET SDK 10.0.401, Laufzeit .NET 10.0.12 (xUnit.net VSTest Adapter v3.1.5), xunit.v3 3.2.2, Microsoft.Playwright 1.49.0 mit lokal installiertem Chromium (`%USERPROFILE%\AppData\Local\ms-playwright`). Testprojekt `VideoWebPlayer.Tests` targetet `net10.0`.
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Einzige Testsuite ist `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`. Befehle aus `.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml` (Job `build-and-test`): Release-Build des Testprojekts, dann drei `dotnet test`-Läufe mit Filtern `Category!=E2E` (Unit), `Category=Integration`, `Category=E2E`. Kategorien werden per `[Trait("Category", "...")]` gesetzt; nur `E2E` ist im Projekt tatsächlich vergeben (32 Trait-Vorkommen), `Integration` trägt kein Test.
- **Abweichung zum CI-Befehl:** Der Unit-Lauf wurde lokal ohne `--collect:"XPlat Code Coverage"` ausgeführt (Coverage ist für den Ausgangszustand nicht erforderlich); Playwright-Browser waren bereits installiert, der CI-Schritt `playwright.ps1 install --with-deps chromium` wurde nicht erneut ausgeführt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` | Repo-Wurzel | 0 | — (Build: 0 Fehler, 140 Warnungen) | — | — | Konsolenausgabe (nicht archiviert; 0 Fehler) |
| Unit | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=test-results.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 303 | 0 | 0 | [TRX](test-results/unit-tests.trx), [Konsole](test-results/unit-tests-console.log) |
| Integration | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=Integration" --logger "trx;LogFileName=integration-test-results.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 0 (kein Test entspricht dem Filter) | 0 | — | [TRX](test-results/integration-tests.trx), [Konsole](test-results/integration-tests-console.log) |
| E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=e2e-test-results.trx" --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 53 | 0 | 0 | [TRX](test-results/e2e-tests.trx), [Konsole](test-results/e2e-tests-console.log) |

Gesamt: **356 ausgeführte Tests, 356 erfolgreich, 0 fehlgeschlagen** (303 Nicht-E2E + 53 E2E; die Integration-Suite ist leer, da kein Test das Trait `Category=Integration` trägt).

### Nachgewiesene bestehende Testfehler

Es wurden **keine** fehlgeschlagenen Tests nachgewiesen; alle ausgeführten Tests waren im Ausgangslauf erfolgreich.

### Testlücken und Ausführungsprobleme

- `Category=Integration`: Filter matcht keinen einzigen Test (Meldung „Kein Test entspricht dem angegebenen Testfallfilter"). Die Suite ist faktisch leer; die CI führt den Lauf trotzdem aus.
- E2E-Tests (`Category=E2E`, Playwright/Chromium) waren lokal ausführbar und liefen vollständig durch.
- Für die von der Anforderung betroffenen Bereiche fehlen Tests: keine Tests für `LoginIpBlockService`, keine Tests für `AuthController.Login` jenseits des Vertragstests, keine Tests zu Pairing/Geräte-Tokens (Feature existiert noch nicht).

## Testklassen (anforderungsrelevant)

### `ApiTokenConfigurationTests` (`VideoWebPlayer.Tests/ApiTokenConfigurationTests.cs`)

- `AddVideoWebPlayerServices_ProductionRequiresMauiApiToken` — Wirft `InvalidOperationException`, wenn `Jwt:ApiToken:Maui` in Production leer ist; prüft die Pflichtkonfiguration in `ServiceCollectionExtensions`.
- `ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue` — Baut `ApiTokenCheckAttribute` mit `MauiOnly` + In-Memory-Configuration und `ListLogger` direkt auf (ohne WebApplicationFactory) und prüft, dass das Log den ungültigen Token nicht enthält. Zeigt das Muster, wie das Attribut isoliert getestet wird (`DefaultHttpContext` + `ServiceCollection` mit `IConfiguration`/`ILogger`).

### `ApiDocumentationContractTests` (`VideoWebPlayer.Tests/ApiDocumentationContractTests.cs`)

- `ApiDocumentationContainsMauiRelevantRoutes` — Prüft Pflicht-Routen in `docs/API.md` (Stringvergleich); neuer Pairing-Endpunkt wäre hier ggf. zu ergänzen.
- `MauiLogin_RejectsNonMauiApiTokens` (Theory, 2 Fälle: `test-legacy-api-token`, `test-web-api-token`) — `POST /api/auth/login` mit Nicht-Maui-Token → 401.
- `MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds` — Health, Login mit `test-maui-api-token` und authentifizierter `GET /api/items` gegen `WebApplicationFactory<Program>` (Environment `Testing`, Temp-SQLite-DB, gesetzte `Jwt:*`-Settings inkl. `Jwt:ApiToken:Maui`).
- Hilfsmethode `CreateUserWithReadableMediaSourceAsync` — legt Benutzer via `UserManager` plus `MediaSource`, `MediaSourceUser`, `MovieCollection` an.
- `FindRepositoryRoot` — sucht `VideoPlayer.sln` aufwärts.

### `ApplicationDbContextTests` (`VideoWebPlayer.Tests/ApplicationDbContextTests.cs`)

- Mehrere `DeleteMediaSourceAsync_*`-Tests — zeigen das Muster für DB-Tests: Shared-In-Memory-SQLite (`Data Source=file:<name>?mode=memory&cache=shared` + offene `SqliteConnection`), `ServiceCollection` mit `EventManager` + `AddDbContext<ApplicationDbContext>(UseSqlite)`, `EnsureCreatedAsync`. Direkt wiederverwendbar für Tests der neuen `DbSet<PairedDevice>`/`DbSet<PairingCode>`.

### E2E-/Integrationsmuster (Kontext)

- E2E-Klassen (`*E2ETests.cs`, `[Trait("Category", "E2E")]`) nutzen `WebApplicationFactory<Program>` teils mit `_factory.UseKestrel()` + `StartServer()` und Playwright (Basisklasse `MediaBoxContextMenuE2ETestBase`); `SkipBrowser`-Fallback bei fehlendem Playwright.
- `Services/UpdateAdminServiceTests.cs` u. a. testen Services mit echter `ApplicationDbContext`-In-Memory/SQLite-Basis.

## Hilfsmethoden

### `ListLogger<T>` (`VideoWebPlayer.Tests/Helpers/ListLogger.cs`)

- `ILogger<T>`-Implementierung, die formatierte Nachrichten in eine `ConcurrentQueue<string>` schreibt — genutzt u. a. in `ApiTokenConfigurationTests`.

### `TestAuthService` (`VideoWebPlayer.Tests/Helpers/TestAuthService.cs`)

- `IAuthService`-Stub (`CurrentUser` = null, Methoden werfen `NotImplementedException`) — für Controller-Tests ohne echte Authentifizierung.

### `TestHelpers` (`VideoWebPlayer.Tests/Helpers/TestHelpers.cs`)

- `WaitForMessageAsync`, `WaitForMessageCountAsync` — Warten auf Log-Zeilen.
- `WaitForMediaCollectionAsync`, `WaitForMediaItemAsync`, `WaitForMediaItemClassifiedAsync`, `WaitForTvShowEpisodeCountAsync`, `WaitForMediaItemCountAsync` — Polling auf DB-Zustände (toleriert `SqliteException` Code 5 / locked).
- `CreateTvShowWithSeasonsAsync` — Testdaten-Seeding.
- `DenyReadAccess`, `ResetAccessControl` — ACL-Manipulation für Zugriffstests (Windows `icacls` / Unix-Mode).

### `MediaBoxContextMenuE2ETestBase` (`VideoWebPlayer.Tests/Helpers/MediaBoxContextMenuE2ETestBase.cs`)

- Basisklasse für Browser-E2E: `WebApplicationFactory` mit Kestrel auf `http://127.0.0.1:0`, Temp-SQLite-DB, `Jwt:*`-Settings, Playwright-Chromium (headless), `SkipBrowser`-Flag bei fehlendem Browser.

### Weitere Helpers

- `IncrementingTimeProvider`, `FakeSftpMediaSourceReader`, `BackfillSftpMediaSourceReader`, `SeriesSftpMediaSourceReader`, `ThrowingSftpMediaSourceReader`, `TestableMediaSourceScanService`, `ContinueWatchingServiceTestBase`, `BackupUploadSessionServiceTestSupport` — domänenspezifische Test-Doubles für Scanner/SFTP/Backups.
