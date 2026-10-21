# Bestandsaufnahme — Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (UTC): 2026-10-21T15:46:48Z
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-12-sftp-probe` @ `2e45575`
- Uncommittete Änderungen im getesteten Stand: keine (nur neue `docs/features/...`-Artefakte, nicht im Testpfad)
- Testumgebung: Windows, .NET SDK 10.0.402, `dotnet test` Release-Build (Debug+Release `dotnet build VideoPlayer.sln` vorab: je 0 Fehler, 142 Warnungen = Bestand)
- Ermittelte Testsuite: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit + Playwright-E2E), Quelle: AGENTS.md bzw. Projektkonvention (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` | Repo-Root | 1 | 2012 | 1 | 1 | [baseline-release.txt](test-results/baseline-release.txt) |
| Einzelretry | `dotnet test ... -c Release --no-build --filter "FullyQualifiedName~PlaylistSkipLockedEntriesE2ETest"` | Repo-Root | 0 | 1 | 0 | 0 | Konsolenausgabe (Bestanden, 10 s) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|------------------|---------------------------|-------------------|
| `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` | `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs:178` | `System.TimeoutException : Timeout 30000ms exceeded` — `Locator("#playlist-detail-selected-entry")` wurde nicht sichtbar; **bekannter E2E-Flake** (bereits im Handover Schritt 11 dokumentiert), Einzelretry grün | Baseline → [baseline-release.txt](test-results/baseline-release.txt); Retry bestanden |

### Übersprungene Tests

| Test | Grund |
|------|-------|
| `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | bekannter Alt-Skip (Skip-Attribut), seit Projektbestand dokumentiert |

## Testklassen (für diese Anforderung relevant)

### `FfprobeMediaDurationProbeTests` (`VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`)
- `TryParseDurationOutput_*` — Parser (12 Fälle)
- `ProbeFileAsync_ParsesStdoutDuration` / `_NonZeroExit_ReturnsTransientFailure` / `_TimedOut_ReturnsTransientFailure` / `_ExitZeroWithoutDuration_ReturnsPermanentFailure` / `_NoBinary_ReturnsTransientFailure` / `_UnexpectedException_ReturnsTransientFailure` / `_Cancellation_Propagates` / `_PassesConfiguredTimeout` — Ergebnis-Mapping über den `_executeAsync`-Seam (kein echtes ffprobe nötig)
- `ProbeStreamAsync_PipesStdinWithPipeArgument` / `_UsesStreamTimeout` — `pipe:0`-Pfad + Stream-Timeout
- Hilfen: `FakeFfprobeLocator` (`Path`-setzbarer `IFfprobeLocator`), `CreateProbe(delegate)`

### `MediaDurationProbeServiceTests` (`VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs`)
- Echtes (Shared-In-Memory-)SQLite über `SqliteConnection` + `EnsureCreated`
- `ProbePendingAsync_*` — Retry-Gate, MaxAttempts, permanent→Attempts=max, BatchSize, Parallelität, `LocalDirectory`→FileProbe, `Sftp`→`OpenFileStream`+StreamProbe, `null`-Stream→transient
- Hilfen: `SeedItemAsync(name, sourceType, path)`, `StubMediaSourceReader` (`StreamToReturn`, `OpenFileStreamCalls`, `LastFileName`), `FakeDurationProbe` (`FileResult`/`StreamResult`, Call-Zähler), `FakeFfprobeLocator`, `FakeScheduleSignal`
- **Zu ändern:** `FakeDurationProbe` implementiert `IMediaDurationProbe` → bei Interface-Erweiterung neue Methode ergänzen; `SftpSource_StreamsViaOpenFileStream` wird zum Fallback-Test (ohne Credentials der Seed-Source).

### `MediaDurationProbeServiceTests_Progress` — Fortschrittsabfrage, nicht betroffen.

## Testlücken und Ausführungsprobleme

- Kein echter SFTP-Server in der Testumgebung — `sftp://`-Probe ist nur über den `_executeAsync`-Seam (Argument-String, Ergebnis-Mapping) testbar, nicht als echter Netz-Integrationstest. ffprobe-9.0.2-full (Gyan, `--enable-libssh`) ist auf diesem System installiert und unterstützt `sftp`.
- Keine Probe-Tests gegen echtes ffprobe-Binary vorhanden (alles Seam-basiert) — bleibt so.
