# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-21, ~05:25–05:30 MESZ (UTC+2)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-1-videolaufzeit` @ `73a3c7733df65469ae7a2064adda8e13bd595a77`
- Uncommittete Änderungen im getesteten Stand: nur neue Artefakte unter `docs/features/…-videolaufzeit/` (Dokumentation, kein Code)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.402, Runtime 10.0.13, xUnit.net v3 (VSTest Adapter 3.1.5), Playwright-Chromium installiert (`ms-playwright`)
- Ermittelte Testsuiten und Quellen der Testbefehle: einzige Suite `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Befehl gemäß AGENTS.md/CI: `dotnet test` auf dem Testprojekt; CI splittet zusätzlich in `Category!=E2E`/`Integration`/`E2E`, lokale Laufzeit ohne Filter)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline 2026-10-21 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug --logger "trx;LogFileName=baseline-test-results.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 1760 | 0 | 1 | [TRX](test-results/baseline-test-results.trx), [Console-Log](test-results/baseline-console.log) |
| Debug-Build vorab | `dotnet build VideoPlayer.sln -c Debug` | Repo-Root | 0 | – | – | – | 0 Fehler, 140 Warnungen (bestehende, u. a. BL0008) |

### Nachgewiesene bestehende Testfehler

Keine — 0 Fehlschläge im Ausgangslauf.

### Testlücken und Ausführungsprobleme

| Test-ID | Suite / Dateipfad | Grund | Umfang |
|---------|-------------------|-------|--------|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs` | `[Fact(Skip = …)]` — dokumentierter, bekannter Produktiv-Befund (`api/continue-watching` 500 bei Film ohne Filmsammlung); Skip-Kommentar verweist auf bewusste Nicht-Behebung in einem früheren Lauf | 1 Test, nicht ausgeführt |

## Testklassen (für diese Anforderung relevant)

### `MediaSourceScannerLocalTests`
Datei: `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs`
- `ScanAllSourcesAsync_CreatesRootCollection_ForLocalSource` — Root-Collection für lokale Quelle
- `ScanNextMediaCollection_PopulatesLocalDirectory` — Items/Collections aus echtem Temp-Verzeichnis + shared-in-memory-SQLite (`CreateServiceProviderAsync`-Muster: `SqliteConnection("…mode=memory&cache=shared")`, `EnsureCreatedAsync`, `Setup`-Zeile mit `DataVersion`)
- `ScanNextMediaCollection_SkipsCollection_WhenLocalDirectoryMissing` — Fehlertoleranz des Scanners

### `MediaSourceScannerTests` / `MediaSourceScanServiceTests`
Dateien: `VideoWebPlayer.Tests/Services/…`
- Scanner/ScanService-Verhalten gegen SQLite in-memory und `FakeSftpMediaSourceReader`/`LocalMediaSourceReader`

### `LocalMediaSourceClassifierTests` / `MediaSourceClassifierActorBackfillTests` / `MediaSourceClassifierBackgroundImageTests`
- Klassifizierungs- und Backfill-Verhalten mit lokalen Testverzeichnissen

### `VideoWebPlayerBackupDataTests`
Datei: `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
- `ReadFromAsync_LegacyBackupWithoutPlaylistIdColumnInContinueWatchingEntries_RestoresSuccessfully` — Vorlage für den Alt-Backup-Regressionstest (Spalte im Payload entfernt, Restore muss erfolgreich sein)
- weitere `…LegacyBackup…`-Tests für optionale Tabellen/Spalten

### `PlaylistBackfillWorkerTests` / `PlaylistBackfillCoordinatorTests` / `PlaylistBackfillSignalTests`
Dateien: `VideoWebPlayer.Tests/Services/…`
- Worker-Aufrufverhalten mit `ManualTimeProvider`/`IncrementingTimeProvider` und Fake-Services — Muster für `MediaDurationProbeWorkerTests`

## Hilfsmethoden

### `LegacyBackupArchiveBuilder`
Datei: `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs`
- `CreateSeededBackupAsync(connection, ct)` — erzeugt Backup-Objekt + seedete DB über shared in-memory-SQLite
- `RemoveColumnsAsync(backup, table, columns, ct)` — baut ein Alt-Payload-Archiv ohne die genannten Spalten (genau der Mechanismus für den neuen Regressionstest)

### Test-DB-Muster
- `file:{name}?mode=memory&cache=shared` + offene Keeper-`SqliteConnection` im `ServiceCollection`; `db.Database.EnsureCreatedAsync`; `db.Setups.Add(new Setup { DataVersion = DataUpgradeManager.CurrentVersion })`
- Fakes: `FakeSftpMediaSourceReader`, `BackfillSftpMediaSourceReader`, `TestAuthService`, `ListLogger`/`CapturingLogger`, `ManualTimeProvider`, `IncrementingTimeProvider`, `TestWebHostEnvironment`
