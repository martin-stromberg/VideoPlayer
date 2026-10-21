# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, ca. 04:00–05:57 UTC
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-2-sender-programmregeln`, Commit `69c5860675d9c69c1a7d28903052be7b9252eb4b`
- Uncommittete Änderungen im getesteten Stand: keine (nur Lifecycle-Artefakte unter `docs/features/` wurden nach dem Lauf angelegt)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.13, .NET 10.0, xUnit.net VSTest Adapter v3.1.5, Windows, Debug-Build (`dotnet build VideoPlayer.sln -c Debug`, 0 Fehler)
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Suite `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit + Microsoft.Playwright + WebApplicationFactory, echte SQLite-Dateien/-In-Memory-DBs); Befehl aus AGENTS.md Definition of Done.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Baseline) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal" --logger "trx;LogFileName=baseline-schritt2.trx"` | Repo-Wurzel | 0 | 1.795 | 0 | 1 | [baseline-schritt2.trx](test-results/baseline-schritt2.trx) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf hatte 0 Fehlschläge. Der eine übersprungene Test ist der bereits bekannte, per `[Fact(Skip)]` deaktivierte Alt-Skip (vor der Umsetzung dokumentiert, unverändert).

### Testlücken und Ausführungsprobleme

- 1 Test übersprungen (bereits per `[Fact(Skip)]` deaktiviert, dokumentierter Alt-Zustand).
- Playwright-E2E-Tests liefen in dieser Umgebung erfolgreich (Chromium headless); einzelne Playwright-Tests können laut AGENTS.md unter Last flackern — einmalige Ausfälle sind einzeln zu wiederholen.

## Testklassen (relevant für diesen Schritt)

### `ProgramSettingsServiceTests` (`VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs`)
- `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue` — Default-Verhalten einer `Setup`-Einstellung
- `UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled` — Persistenz einer Einstellung über `ProgramSettingsService`
- `GetDiscoveryPublicBaseUrlAsync_DefaultsToNull` / `UpdateGeneralSettingsAsync_PersistsNormalizedDiscoveryPublicBaseUrl` — Muster für Get-/Update-Tests neuer `Setup`-Spalten

### `VideoWebPlayerBackupDataTests` (`VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`)
- `Restore_LegacyBackupWithoutXTable_Succeeds`-Muster — `LegacyBackupArchiveBuilder.CreateSeededBackupAsync` erzeugt echtes Backup, `RemoveTablesAsync`/`RemoveColumnsAsync` simuliert Alt-Stand, `ReadFromAsync` muss erfolgreich wiederherstellen (Muster für `UnlockedMediaEntries`, `PlaylistBackfillMarkers`, `Setups.MdnsAdvertisementEnabled` u. a.)

### `ApplicationDbContextTests` (`VideoWebPlayer.Tests/ApplicationDbContextTests.cs`)
- Schema-/Verhaltenstests gegen echtes SQLite (Muster for FK/Löschverhalten-Tests der neuen Tabellen)

### `ProgramSettingsE2ETests` (`VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs`)
- E2E-Basis für Admin-Seiten: eigener `WebApplicationFactory<Program>` + Playwright; `SeedUserAsync(email, isAdmin)` legt Benutzer inkl. `IsAdmin`-Claim an; Admin-Seite → Speichern → sichtbares Ergebnis; Nicht-Admin → „Nicht autorisiert."

### `PlaylistsE2ETestBase` (`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`)
- E2E-Basisklasse mit Playwright-Setup, `LoginAsync`, `SelectSearchResultAsync` (treibt den `MediaSearchSelector`), `DriveUntilStateAsync` (Anti-Flake-Muster für Blazor-Circuit-Handover), `Seed*`-Hilfsmethoden für Medienbestand

### `ItemsControllerAccessTests` / `ItemsControllerTestFactory` (`VideoWebPlayer.Tests/Controllers/`, `Helpers/`)
- Zugriffsfilterung des `/api/items`-Endpunkts (sichtbarer Medienbestand)

### `EnumContractTests` (`VideoWebPlayer.Tests/EnumContractTests.cs`)
- Pinning vertraglicher Enums zwischen Assemblies

## Hilfsmethoden

### `PairingTestDb` (`VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`)
- `CreateAsync(dbName, ct)` — echtes SQLite (`mode=memory&cache=shared`) mit `EnsureCreated`, `UserManager<ApplicationUser>`, `AuthorizationTokenService`; `Db`-Property liefert `ApplicationDbContext`; `CreateUserAsync(email, isAdmin)` legt Benutzer mit Admin-Flag an — Standard-Fixture für Service-Tests gegen echtes SQLite

### `LegacyBackupArchiveBuilder` (`VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs`)
- `CreateSeededBackupAsync` — erzeugt Backup-Stream aus echtem Schema
- `RemoveTablesAsync(stream, tables, ct)` — entfernt Tabellen aus dem Backup-Index → Alt-Backup-Simulation
- `RemoveColumnsAsync(stream, table, columns, ct)` — entfernt Spalten → Alt-Backup-Simulation
- `ReadTableColumnsAsync` — Spalten eines Backup-Eintrags lesen

### `TestHelpers` (`VideoWebPlayer.Tests/Helpers/TestHelpers.cs`)
- `CreateTvShowWithSeasonsAsync(db, …)` — Serien-Testdaten
- `WaitFor*` — Warteschleifen für asynchrone Scans

### `PlaylistServiceTestBase` / `PlaylistsControllerTestBase`
- Aufwändigere Fixture-Muster für Playlist-Service/Controller-Tests (Service-Aufbau mit echten Abhängigkeiten)
