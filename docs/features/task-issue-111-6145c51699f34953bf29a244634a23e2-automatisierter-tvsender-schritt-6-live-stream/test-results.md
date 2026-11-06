# Testergebnisse

## Abschluss-Testlauf

- **Branch:** `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-6-live-stream`
- **Befehl:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --logger "console;verbosity=normal"`
- **Vorlauf-Builds:** `dotnet build VideoPlayer.sln -c Debug` (0 Fehler) und `dotnet build VideoPlayer.sln -c Release` (0 Fehler)
- **Exit-Code:** 0
- **Ergebnis:** 1953 bestanden, 0 fehlgeschlagen, 1 übersprungen (Gesamt: 1954)
- **Übersprungen:** `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — bekannter Baseline-Skip, unverändert
- **Dauer:** ca. 4,0 Minuten
- **Vollprotokoll:** `final-dotnet-test.log` in diesem Verzeichnis

Gegenüber der Baseline (1893 Tests: 1892 grün, 1 übersprungen) kamen **61 neue Tests** hinzu — alle grün:

- `FfmpegLocatorTests` (5) — Suchreihenfolge konfigurierbarer Pfad → Konventionsverzeichnisse → PATH
- `FfmpegLiveStreamSegmenterTests` (7) — Argumentbau, Datei- vs. Pipe-Eingang, Start/Stop/Neustart, Kill-Baum
- `LiveStreamSegmentStoreTests` (8 + 6 Theory) — Verzeichnisauflösung, Whitelist/Containment, Retention-Sweep, Startup-Clear
- `LiveStreamMediaResolverTests` (4) — Local→Pfad, SFTP→Pipe, fehlende Referenzen
- `LiveStreamCoordinatorTests` (7) — Start/Wechsel/Drift/Lücke/ffmpeg-fehlt/Medium-fehlt/WakeAtUtc
- `LiveStreamControllerTests` (3 + 5 Theory + 1) — m3u8-Token-Rewrite, Content-Types, 404-Pfade, 401
- `LiveStreamWorkerTests` (1) — Startup-Clear + Tick
- `ProgramSettingsServiceLiveStreamTests` (3 + 5 Theory) — Defaults, Roundtrip, Validierung
- `VideoWebPlayerBackupDataTests` (+1) — Alt-Backup ohne die drei neuen Spalten
- `LiveStreamE2ETests` (2 + 3 Theory + 3) — HTTP-E2E: 401 anonym, Playlist+Token-Rewrite, Segment, 404/Traversal
- `TVSenderAdminE2ETests` (+0 Methoden, erweitert) — Stream-Einstellungen im bestehenden Playwright-Lauf

## Frühere Läufe in diesem Schritt

- Baseline vor Umsetzung: 1892/1893 grün, 1 übersprungen — `inventory/test-results/baseline-dotnet-test.log`
- Fokussierter Lauf der neuen Tests nach Umsetzung: zunächst 5 Befunde (Default-Pfad-Separator, absolute Pfadspeicherung vs. relativer Auflösung im Store, Content-Type-Zusatz, Zeilenende) — behoben, danach 60/60 grün.
