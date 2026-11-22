# Bestandsaufnahme: Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-22, Läufe 18:24:50–18:33:19 `W. Europe Standard Time` (UTC+1); Metadaten erfasst ~17:24 UTC
- Branch und Commit-ID: `task/issue-368-e8ebf717b9eb4c499216766302ec5d3a-flaky-test-haertung-umgebungss` @ `84fc6a56b13890a1d872496e17fb3bc89f6a6a3b`
- Uncommittete Änderungen im getesteten Stand: nur das untracked Feature-Verzeichnis `docs/features/task/issue-368-…/` selbst; keinerlei Änderungen an Code, Tests oder Konfiguration
- Testumgebung und Runtime-/SDK-Versionen: `DESKTOP-CM8OBSG` (Windows lokal — nicht die CI-Umgebung `ubuntu-latest`); .NET SDK `11.0.100`, Testlaufzeit .NET 10.0.15, `net10.0`, xunit.v3 VSTest-Adapter 3.1.5; PowerShell `7.6.10` (`pwsh` verfügbar); ffmpeg/ffprobe auf `PATH` (WinGet); Playwright-Chromium im Browser-Cache (`chromium-1255` u. a.) — alle Umgebungsvoraussetzungen der Suite erfüllt
- Ermittelte Testsuiten und Quellen der Testbefehle: einziges Testprojekt `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`; Befehlsquelle `.github/workflows/staging-ci.yml` bzw. `pr-staging-ci.yml` (`build-and-test`, Schritte Z. 119–126 / 141–148: `dotnet test` mit Filtern `Category!=E2E`, `Category=Integration`, `Category=E2E`, jeweils `-c Release`). Build vor den Läufen: `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release` (0 Fehler, 139 Warnungen) sowie `dotnet build VideoPlayer.sln` Debug (0 Fehler, 139 Warnungen)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Regulär (CI-Lane „Unit") | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=test-results-inventory-regular.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2707 | 0 | 1 (von 2708) | [TRX](test-results/test-results-inventory-regular.trx) · [Konsole](test-results/run-regular-console.txt) |
| E2E (CI-Lane „E2E") | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=test-results-inventory-e2e.trx" --logger "console;verbosity=normal"` | Repo-Root | nicht separat erfasst (umgebender Shell-Aufruf 0; Testlauf-Outcome `Failed` lt. TRX/Konsole) | 254 | 1 | 1 (von 256) | [TRX](test-results/test-results-inventory-e2e.trx) · [Konsole](test-results/run-e2e-console.txt) |

Zeitrahmen der TRX (`<Times>`): Regulär 18:24:50→18:27:54 (~3:04 min), E2E 18:28:02→18:33:19 (~5:17 min). Zähler autoritativ aus `<Counters>` der TRX. Gesamtabdeckung beider Läufe: 2964 Tests ohne Überschneidung. Die CI-Schrittfolge enthält dazwischen noch `--filter "Category=Integration"` — matcht **0** Tests (kein `Integration`-Trait im Projekt), daher hier kein eigener Lauf dokumentiert, sondern als Befund in `test-infrastructure.md`/`ci-workflows.md`.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` | `VideoWebPlayer.Tests/TvSenderOnAirE2ETests.cs` (Test Z. 55–94, Fehlstelle Z. 92; `[Trait("Category","E2E")]` Z. 17) | `Microsoft.Playwright.PlaywrightException`: `ToContainTextAsync` (Timeout 5000 ms) erwartete `Off-Air-Film` in `#live-tv-now-title`, Element nicht gefunden (`But was: 'null'`). Aria-Snapshot zeigt die korrekt gerenderte Off-Air-Ansicht („Der Sender ist zurzeit nicht auf Sendung.", `naturalWidth > 0` war erfüllt) — die „Jetzt"-Karte hinkt dem Seitenaufbau asynchron hinterher. Timing-Flake, kein Produktfehler: alle anderen 253 E2E-Tests grün, darunter der Gegenpart `Admin_TogglesBackOnAir_StreamAndPlayerResume`. | E2E-Lauf; [TRX](test-results/test-results-inventory-e2e.trx) (Dauer 27,9 s), [Konsole](test-results/run-e2e-console.txt) Z. 168–260 |

### Testlücken und Ausführungsprobleme

**Übersprungen / nicht ausgeführt:**

| Test | Lauf | Grund |
|------|------|-------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Regulär | `Assert.SkipWhen(OperatingSystem.IsWindows(), …)` (`FfmpegToolDownloadServiceTests_Install.cs:79`) — OS-Gate; in der ubuntu-CI läuft dieser Test |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | E2E | Dauerhaftes `[Fact(Skip = "Befund: api/continue-watching antwortet mit 500 …")]` (`DevicePlaylistE2ETests_Playback.cs:76`) — dokumentierter Produktbefund (`ContinueWatchingService.cs:122`, NullReference→HTTP 500), kein Flake; Reaktivierung gehört zu dem dort benannten Auftrag |

**Nicht aufgetreten:** abgebrochene (`aborted="0"`), nicht lauffähige (`notRunnable="0"`) Tests und Build-/Setup-/Infrastrukturfehler in beiden Läufen.

**Wiederholungs-Befund (Cross-Task):** Derselbe Test `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` ist bereits in der Bestandsaufnahme von Issue #354 (`docs/features/task/issue-354-…/inventory/tests.md`, Z. 44) mit **identischer Fehlsignatur** fehlgeschlagen (`ToContainTextAsync`-Timeout auf `#live-tv-now-title`) und war dort nach Einzel-Wiederholung grün; sein Gegenpart `Admin_TogglesBackOnAir_StreamAndPlayerResume` flackte dort ebenfalls (`TimeoutException` in `CapturePlaylistUrlAsync`, Z. 47). Der Flake ist damit über mind. zwei unabhängige Baselines auf zwei Worktrees reproduzierbar — stärkster `Flaky`-Kandidat des Audits.

**Stille Skip-Pfade (als `Passed` gezählt, nicht gelaufen):** in diesem Lauf nicht ausgelöst, aber verdrahtet — `if (SkipBrowser) return;` bei Playwright-Startfehlern (Basen `PlaylistsE2ETestBase`/`MediaBoxContextMenuE2ETestBase`) und `E2EBinaryLocator`-Null-Return bei fehlendem ffmpeg/ffprobe (`FfmpegLiveStreamSegmenterE2ETests`, `LiveStreamE2ETests`, `MediaProbeHttpIntegrationTests`). Auf einem Rechner ohne Browser/Binaries wäre die gemeldete Zahl daher nicht vergleichbar.

**Live-Beleg Trait-Lücke:** der reguläre TRX enthält 34 Testergebnisse der 8 ungetaggten Playwright-Klassen (`LiveTvE2ETests`, `LiveTvClipBlocksE2ETests`, `TVSenderAdminE2ETests`, `TVSenderAdminE2ETests_AgeWindow`, `TVSenderAdminClipBlocksE2ETests`, `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminDayBarE2ETests`, `TVSenderAdminValidityE2ETests`) — sie laufen mit echtem Chromium in der `Category!=E2E`-Lane.

## Testklassen (relevant für dieses Issue)

### `TvSenderOnAirE2ETests` (`TvSenderOnAirE2ETests.cs`, `Category=E2E`)
- `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` — On-Air-Umschalter → Playlist/Segmente/Standby 404, Program-Guide weiterhin 200, `/live-tv` zeigt Off-Air-Bild statt Player; **im Baseline-Lauf geflackt** (Z. 92).
- `Admin_TogglesBackOnAir_StreamAndPlayerResume` — Rückkehr on air: Umschalter, Stream liefert wieder `#EXTM3U`; grün.

### `FfmpegLiveStreamSegmenterE2ETests` (`Services/`, `Category=E2E`)
- 7 `RealFfmpeg_*`-Tests — echte ffmpeg-Segmentierung lavfi-generierter Videos (Pipe-, File-, moov-at-End-, SFTP-unerreichbar-Szenarien); Deadlines 45–60 s; alle grün.

### `LiveStreamE2ETests` (`Category=E2E`, `DeviceClientTestBase`)
- `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` — echter ffmpeg-`Process.Start` (Z. 223), 60-s-Poll auf Standby-Playlist (32 s Laufzeit, grün); Host-Budget < `TranscodeTimeout` 120 s (s. `time-budgets.md`).

### `MediaProbeHttpIntegrationTests` (`Category=E2E`)
- Echter Kestrel-Loopback + ffmpeg-Videogenerator + ffprobe gegen Token-URIs; alle grün.

### `DevicePlaylistE2ETests_Playback` (`Category=E2E`, `DeviceClientTestBase`)
- 2 grüne Playback-/Fortschritts-Tests + 1 dauerhaft geskipptes `Fact` (Befund-Dokumentation).

### `UdpDiscoveryListenerTests` (ohne Trait)
- Echte UDP-Loopback-Sockets: `Start_ListensOnFreePort`, Rebind-/Port-belegt-Szenarien, Datagramm-Verlust vor Bind; 10-s-Deadlines; alle grün — klarster regulärer OS-Interface-Kandidat.

### `SegmenterProcessSupportTests` (`Services/`, ohne Trait)
- `FakePumpProcess`-Drain-Tests (deterministisch) + `StartRealProcess_UnstartableBinary_*` (echter Start-Versuch auf fehlende `.exe`); grün.

### `LocalMediaSourceReaderTests` (`Services/`, ohne Trait)
- Junction/Symlink-Fixture (`TryCreateJunction`, Z. 298: Windows `mklink /J` vs. Unix `CreateSymbolicLink`), echte Temp-Medienbäume; grün.

## Hilfsmethoden

### `TestHelpers` (`Helpers/TestHelpers.cs`)
- `WaitFor*`-Familie — 10-ms-Poll mit Caller-Timeout (2–10 s) inkl. `DumpDatabaseStateAsync`-Diagnose.
- `DenyReadAccess`/`ResetAccessControl` (Z. 261–303) — Windows `icacls`-Kindprozess / Unix `SetUnixFileMode`.

### `PlaylistsE2ETestBase` / `MediaBoxContextMenuE2ETestBase` / `TVSenderAdminE2ETestBase` (`Helpers/`)
- `InitializeAsync`/`DisposeAsync` — Kestrel-Loopback `:0` + Chromium + Temp-SQLite-Dateibank; `SkipBrowser`-Fallback.
- `DriveUntilStateAsync` — Wiederholer (10-s-Versuch, 60-s-Deadline) gegen Circuit-Handover.

### `DeviceClientTestBase` / `PairingExchangeContractTestBase` / `LocalMediaPlaylistE2ETestBase` (`Helpers/`)
- `WebApplicationFactory`-TestServer (kein Port) + Temp-SQLite-Dateibank via `PairingWebApplicationFactory.CreateTempDbPath` (+ echtes Medienverzeichnis bei LocalMedia).

### `PairingWebApplicationFactory` (`Helpers/`)
- `CreateTempDbPath(filePrefix)` — `Path.GetTempPath()/{prefix}-{guid}.db`; `Create(dbPath, configure)` — Testing-Umgebung, SQLite-Connection-String, JWT/HTTPS-Setup.

### `E2EBinaryLocator` (`Helpers/`)
- `Find("ffmpeg"|"ffprobe")` — `PATH`-Suche (`.exe`-Suffix auf Windows); `null` → Test-Methoden kehren still zurück.

### `BackupUploadSessionServiceTestSupport` / `TempDirectory` (`Helpers/`)
- `CreateProvider`/`CreateService`, `DisposeSessionAsync` (ungeschütztes `File.Delete`, Z. 34), `TempDirectory.Dispose` (geschütztes `Directory.Delete`, Z. 59–70).

### `FfmpegPackageServer` (`Helpers/`)
- `IAsyncDisposable` Kestrel-Loopback-Server für ffmpeg-Download-Szenarien (`TransferDelayMilliseconds` für Fortschritt).
