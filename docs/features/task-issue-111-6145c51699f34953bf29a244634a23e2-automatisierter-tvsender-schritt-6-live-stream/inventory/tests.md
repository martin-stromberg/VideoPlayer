# Bestandsaufnahme: Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, Laufstart ~09:05 MESZ (UTC+2), Ende ~09:09 MESZ
- Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-6-live-stream`
- Commit-ID: `d24b750fd72473a4aec31059e86f93e4c7d067e5` (Build davor: `dotnet build VideoPlayer.sln`, Debug, 0 Fehler, 141 Alt-Warnungen)
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Dokumente (`docs/features/...-schritt-6-live-stream/`); kein Produktivcode, keine Teständerungen
- Testumgebung: Windows, .NET SDK 10.0.402, ffmpeg/ffprobe 9.0.2 auf PATH, Playwright-E2E lauffähig
- Testsuiten: einzige Suite `VideoWebPlayer.Tests` (Unit-/Integrations- und E2E-Tests, letztere per `[Trait("Category", "E2E")]`); Quelle des Befehls: AGENTS.md (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"` | Repo-Wurzel | 0 | 1892 | 0 | 1 | `baseline-dotnet-test.log` (`test-results/baseline-dotnet-test.log`) |

Gesamtdauer: ~4 Minuten. Gesamtzahl laut Runner: 1893.

### Nachgewiesene bestehende Testfehler

Keine — im Ausgangslauf ist kein Test fehlgeschlagen.

### Testlücken und Ausführungsprobleme

- 1 Test dauerhaft übersprungen (bekannter Alt-Skip, identisch zu den Baselines der Schritte 4/5):
  `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`.

## Testklassen (für diesen Bereich relevant)

### `FfprobeLocatorTests` (`VideoWebPlayer.Tests/Services/FfprobeLocatorTests.cs`)

- Suchreihenfolge konfigurierbarer Pfad → Konventionsverzeichnisse → PATH; Vorlage für `FfmpegLocatorTests`.

### `FfprobeMediaDurationProbeTests` (`VideoWebPlayer.Tests/Services/`)

- Fake-`_executeAsync`-Muster: Prozessausführung wird per injiziertem Delegat simuliert (Argumente, Exit-Code, Timeout, stdin) — Vorlage für die ffmpeg-Segmentierungs-Tests ohne echtes Binary.

### `ProgramScheduleWorkerTests` (`VideoWebPlayer.Tests/`)

- Worker-Testmuster: injizierter `IServiceScopeFactory`/`TimeProvider`/Signal, kurze Delays.

### `ProgramGuideServiceTests` (`VideoWebPlayer.Tests/`)

- Service-Tests gegen echtes SQLite (`ProgramScheduleTestDb.CreateAsync` + `CreateSlotAsync`).

### `ProgramSettingsServiceTests` (`VideoWebPlayer.Tests/`)

- `ProgramSettingsServiceFixture`-Muster: SQLite-InMemory, Get/Update/Defaults/Validierung — Vorlage für die Stream-Einstellungen.

### `ProgramGuideControllerTests` (`VideoWebPlayer.Tests/Controllers/`)

- Controller direkt instanziiert, `FakeAuthService`-Muster — Vorlage für den Stream-Controller.

### `ProgramGuideE2ETests` (`VideoWebPlayer.Tests/`)

- HTTP-E2E über `DeviceClientTestBase`/`PairingWebApplicationFactory`: 401 anonym, Bearer-Header, `access_token`-Query, Seeding von Channel+Slots direkt über `ApplicationDbContext`.

### `ProgramSettingsE2ETests` / `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/`)

- Playwright-E2E der Admin-Seiten: `WebApplicationFactory` + `UseKestrel()` + echte Chromium-Instanz, Admin-Seeding, Formular-Interaktion — Vorlage für die neuen Einstellungsfelder.

### `VideoWebPlayerBackupDataTests` + `LegacyBackupArchiveBuilder` (`VideoWebPlayer.Tests/Services/Backups/`)

- `RemoveColumnsAsync`-Muster für den Alt-Backup-Regressionstest neuer `Setups.*`-Spalten.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(dbName, ct)` — echtes SQLite-InMemory (`mode=memory&cache=shared`), `EnsureCreated`
- `CreateSlotAsync(channelId, start, end, mediaItemId, movieId?, episodeId?, ruleId?, ruleType, status)` — direktes Slot-Seeding
- `CreateMediaItemAsync(sourceId, duration)` — `MediaSource` + `MediaCollection` + `MediaItem` (SourceType ist `Sftp`-Default; für Local-Quellen ist `SourceType` zu setzen)
- `EnsureSourceAsync`, `GrantSourceAccessAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateRuleAsync`, `GetChannelAsync`

### `DeviceClientTestBase` (`VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`)

- `CreateUserAndPairDeviceAsync(email)` — legt Benutzer an und paart ein Gerät; liefert Token für Bearer/`access_token`-Aufrufe
- `CreateHttpClient()` / `CreateScope()` — HttpClient gegen die `WebApplicationFactory` bzw. DI-Scope zum Seeden
