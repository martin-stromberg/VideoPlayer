# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, Laufstart ~08:10 MESZ (UTC+2), Ende ~08:14 MESZ
- Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-5-programmauskunft`
- Commit-ID: `7438f5a4b46000abf00a9f680a506551b1f6d50a`
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Dokumente (`handover.md`-Bearbeiterzeile,
  `docs/features/...-schritt-5-programmauskunft/`); kein Produktivcode, keine Teständerungen
- Testumgebung: Windows, .NET SDK 10.0.402, Playwright-E2E vorhanden und lauffähig
- Testsuiten: einzige Testsuite `VideoWebPlayer.Tests` (Unit-/Integrations- und E2E-Tests, letztere
  per `[Trait("Category", "E2E")]` markiert); Quelle des Befehls: AGENTS.md (`dotnet test
  VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 1865 | 0 | 1 | [baseline-dotnet-test.log](test-results/baseline-dotnet-test.log) |

Gesamtdauer: 4,2 Minuten. Gesamtzahl laut Runner: 1866.

### Nachgewiesene bestehende Testfehler

Keine — im Ausgangslauf ist kein Test fehlgeschlagen.

### Testlücken und Ausführungsprobleme

- 1 Test dauerhaft übersprungen (bekannter Alt-Skip, auch in Schritt-4-Baseline dokumentiert):
  `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`.

## Testklassen (für diesen Bereich relevant)

### `ProgramScheduleServiceTests_*` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_*.cs`)

- `Generation`, `Conflicts`, `Filler`, `Access`, `Reconcile` — Sendeplan-Erzeugung/-Nachführung
  gegen echtes SQLite-InMemory über `ProgramScheduleTestDb`.
- `ProgramScheduleSignalTests`, `ProgramScheduleWorkerTests`, `ProgramSettingsServiceTests`,
  `ProgramRuleServiceTests_*` — Signal, Worker, Einstellungen, Regel-CRUD/Autorisierung.

### `PlaylistsControllerTests_*` (`VideoWebPlayer.Tests/Controllers/`)

- Muster für Controller-Tests: echte SQLite-DB + `FakeAuthService`, Controller direkt instanziiert
  (`PlaylistsControllerTestBase`).

### `DevicePlaylistE2ETests_*` (`VideoWebPlayer.Tests/`)

- HTTP-E2E über `WebApplicationFactory` (`PairingWebApplicationFactory`, `DeviceClientTestBase`):
  Geräte-Pairing per QR-Bootstrap, Bearer-Token sowohl als Header als auch als
  `access_token`-Query; 401-Nachweis ohne Nachweis.

### `TVSenderAdminE2ETests`, `ProgramSettingsE2ETests`

- Playwright-E2E für die Admin-Oberfläche — für diesen API-Schritt nicht betroffen.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(dbName, ct)` — SQLite-InMemory (`mode=memory&cache=shared`), `EnsureCreated`.
- `CreateService()` — `ProgramScheduleService` mit `ProgramRuleService`, `ProgramSettingsService`, Null-Loggern.
- `AddUserAsync`, `EnsureSourceAsync`, `GrantSourceAccessAsync` — Benutzer-/Freigabe-Seeds.
- `CreateMediaItemAsync`, `CreateMovieAsync`, `CreateShowAsync` — Inhalte mit probierter Dauer.
- `CreateRuleAsync` — Programmregel direkt in die DB (Validierung umgangen).
- `GetChannelAsync`, `SlotsAsync`, `AssertGapless`, `SaveDiagAsync` (FK-Diagnose).

→ Für die Programmauskunft wiederverwendbar; `ProgramSlot`-Seeds direkt über `fixture.Db` möglich.

### `FakeAuthService` (`VideoWebPlayer.Tests/Helpers/FakeAuthService.cs`)

- `CurrentUser` setzbar → `CheckLoggedIn`-Pfade im Controller testbar.

### `DeviceClientTestBase` / `PairingWebApplicationFactory` (`VideoWebPlayer.Tests/Helpers/`)

- `CreateUserAndPairDeviceAsync(email, isAdmin)` → `(User, PairingBootstrapPayload)` mit
  `payload.Token` (Bearer), `Client`, `CreateHttpClient()`, `Services` (DI-Scope zum Seeden,
  z. B. `ProgramSlot`/`TVChannel` direkt in `ApplicationDbContext`).
