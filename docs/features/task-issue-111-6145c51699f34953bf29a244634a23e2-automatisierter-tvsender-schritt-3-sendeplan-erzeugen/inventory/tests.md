# Tests – Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, Mitteleuropäische Zeit (Systemzeit des Rechners)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen` @ `6a5945d` (plus uncommittete Planungsartefakte unter `docs/features/…schritt-3…/`, die keinen Einfluss auf Code/Tests haben)
- Uncommittete Änderungen im getesteten Stand: nur Dokumentationsartefakte dieses Workflows (`docs/features/…schritt-3-sendeplan-erzeugen/`); kein Code
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET 10 SDK (EF-Tools 10.0.10), Ziel-Framework `net10.0`, Playwright-E2E laufen mit echtem Browser (laut Abnahme Schritt 2 verifiziert)
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Testsuite `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit), Befehl aus `AGENTS.md`/DoD: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"` | Repo-Root | 0 | 1825 | 0 | 1 | [baseline.log](test-results/baseline.log) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf ist vollständig grün. Der einzige übersprungene Test ist der bekannte Alt-Skip:

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` | Dauerhaft übersprungen (vorhandener Alt-Skip, in Abnahme Schritt 2 bereits dokumentiert) | baseline.log |

### Testlücken und Ausführungsprobleme

Keine. Die Suite enthält Playwright-E2E-Tests, die in dieser Umgebung mit echtem Browser laufen (Einzel-Flakes sind laut Projektkonvention einzeln zu wiederholen und zu berichten).

## Testklassen (relevant für diese Anforderung)

### `ProgramRuleServiceTests_Crud` / `_Channel` / `_Authorization` / `_Validation`
Dateien: `VideoWebPlayer.Tests/ProgramRuleServiceTests_*.cs`
- CRUD, Sender-Anlage/-Umbenennung, Admin-Erzwingung (`ForbiddenAccessException`/`UnauthorizedAccessException`), Validierung inkl. Material-Whitelist und -Existenz; `ProgramRuleServiceTests_Channel.RuleInput(...)` ist die gemeinsame Input-Factory.

### `ProgramSettingsServiceHolidayFederalStateTests`
- Getter/Updater, Whitelist-Validierung `DE-*`, `null` = bundesweit.

### `VideoWebPlayerBackupDataTests`
Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
- Muster für Alt-Backup-Regressionstests: `LegacyBackupArchiveBuilder.CreateSeededBackupAsync` + `RemoveTablesAsync`/`RemoveColumnsAsync` → `ReadFromAsync` muss fehlerfrei durchlaufen. Vorhandenes Beispiel: `ReadFromAsync_LegacyBackupWithoutTVChannelTablesAndHolidayFederalState_RestoresSuccessfully`.

### `TVSenderAdminE2ETests`
Datei: `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- Playwright-E2E auf `PlaylistsE2ETestBase`: konsolidierter Regel-Lebenszyklus-Test `Admin_CreateEditDeactivateDeleteRule` + `NonAdmin_SeesNotAuthorized`. Helper: `OpenAdminPageAsAdminAsync`, `SeedTvShowWithSeasonsAsync`, `DriveUntilStateAsync`, `MakeUserAdminAsync`, `GrantMediaSourceAccessForUserAsync`.

### `PlaylistBackfillWorkerTests` / `PlaylistBackfillCoordinatorTests` / `MediaDurationProbeWorkerTests`
Dateien: `VideoWebPlayer.Tests/Services/`
- Muster zum Testen von Workers/Coordinators mit `TimeProvider`-Injection, `PlaylistBackfillTestServices.Build(connectionString, settings, signal, gate)`.

## Hilfsmethoden

### `PairingTestDb` (`VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`)
- `CreateAsync(name, ct)` — echtes In-Memory-SQLite (`mode=memory&cache=shared`) mit `EnsureCreated`, echtem `UserManager` und `CreateUserAsync(email, isAdmin)`; FKs/Unique-Indizes greifen real.

### `PlaylistServiceTestBase` (`VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`)
- SQLite-Fixture mit zwei Benutzern; `CreateShowWithSeasonAsync`, `AddSeasonToShowAsync`, `AddMovieToCollectionAsync`, `CreateTestMediaEntryAsync`, `GrantMediaSourceAccessForUserAsync`, `UnlockMediaForUserAsync`, `GetOrCreateGenreIdAsync`, `CreateMovieWithGenresAsync`, `AddGenresToTVShowAsync`, `CreateTestManualPlaylistWithEntriesAsync`.
- Für den Sendeplan wiederverwendbares Muster; eine eigene, schlankere Fixture (analog `PairingTestDb`) ist für die reinen Plan-Tests naheliegender, da kein `PlaylistService` benötigt wird.
