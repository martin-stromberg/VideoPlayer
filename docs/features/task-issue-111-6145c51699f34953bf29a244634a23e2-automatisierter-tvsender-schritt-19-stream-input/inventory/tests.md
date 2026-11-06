# Bestandsaufnahme — Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt: 2026-10-22, Fortsetzung von Schritt 18 (identischer Code-Stand)
- Branch/Commit: `task-issue-111-…-schritt-19-stream-input` @ `3d7db1b` (HEAD)
- Uncommittete Änderungen: keine (`git status` clean)
- Testumgebung: Windows, .NET SDK (Repo-Konvention), SQLite-basierte Test-DBs

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (Schritt 18, identischer Code-Stand) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | Repo-Root | 0 | 2124 | 1 (eigene E2E-Erweiterung, gefixt + Retry grün) | 1 (bekannter Alt-Skip) | `handover.md` Schritt 18 |

Der HEAD-Stand unterscheidet sich vom getesteten Stand nur durch zwei Doku-Commits
(`nachbesserungen.md` N15-Eintrag, `steps.md` Statuszeile) — kein Produktiv- oder
Testcode geändert; der dokumentierte Lauf gilt als Baseline.

### Nachgewiesene bestehende Testfehler

Keine dauerhaften — der einzige Fehlschlag im Baseline-Lauf war die eigene Schritt-18-E2E-Erweiterung
(seed-abhängig), wurde gefixt und im Einzel-Retry grün. Baseline-Flake
`PlaylistSkipLockedEntriesE2ETest` lief im Baseline-Lauf durch.

## Testklassen (betroffener Bereich)

### `FfmpegLiveStreamSegmenterTests` (`VideoWebPlayer.Tests/Services/`)
- `FakeSegmenterProcess` — `ISegmenterProcess`-Fake: `StandardError`/`StandardInput` als `MemoryStream`, `Kill()` setzt `HasExited`.
- `StartAsync_FileInput_BuildsExpectedArguments` — Argumente + `RedirectStandardInput=false`.
- `StartAsync_PipeInput_UsesPipeArgumentAndRedirectsStdin` — `pipe:0`, kein `-nostdin`, Stdin redirected.
- `StartAsync_SetsRunningStateAndSlot`, `StopAsync_KillsProcessAndClearsState`, `StartAsync_SecondJob_StopsPreviousProcess`, `StartAsync_FailedStart_ThrowsAndStaysStopped`, `ExpectedPositionSeconds_NoJob_IsNull`.

### `LiveStreamMediaResolverTests` (`VideoWebPlayer.Tests/Services/`)
- Gegen echtes SQLite (`ProgramScheduleTestDb`): lokale Quelle → `FilePath`; fehlende Datei → `null`; SFTP → `IsPipe` + `OpenStreamAsync`; unbekanntes Item → `null`.
- Konstruktor heute: `(db, IMediaSourceReader, LocalMediaSourceReader, logger)` — Signaturänderung betrifft diese Tests und `LiveStreamCoordinatorTests`.

### `LiveStreamCoordinatorTests` (`VideoWebPlayer.Tests/`)
- `FakeSegmenter : ILiveStreamSegmenter` (Jobs-Liste, StopCalls) — Interface-Erweiterung betrifft dieses Fake.
- Start/Drift/Slotwechsel/Sendelücke/ffmpeg-fehlt/Medium-nicht-auflösbar gegen echtes SQLite.

### `InternalMediaStreamTokenServiceTests` (`VideoWebPlayer.Tests/Services/`)
- `TryCreateProbeUri`: Wildcard→Loopback, HTTPS-only→null, Disabled→null, konfigurierte BaseUrl gewinnt, ungültige BaseUrl → Auto-Detection, TTL/Expiry, `ResolveMediaItemId`-Auflösung, `MaskStreamTokenInText`.
- Test-Fakes: `IServer`-Stub mit `IServerAddressesFeature`, `MediaProbeOptions` via `Options.Create`, Fake `TimeProvider`.

### `LiveStreamWorkerTests`, `LiveStreamControllerTests`, `LiveStreamSegmentStoreTests`, `LiveStreamE2ETests`, `MediaProbeHttpIntegrationTests`, `InternalMediaStreamControllerTests`
Bestehende Abdeckung von Worker-Ticks, m3u8-Token-Rewrite, Store-Retention, HTTP-E2E — nicht direkt betroffen, aber Signaturen der Resolver/Coordinator-Konstruktion ggf. anzupassen.

## Hilfsmethoden

- `ProgramScheduleTestDb` — echte SQLite-Test-DB mit Schema; `CreateSlotAsync`-Helfer.
- `TestWebHostEnvironment` — ContentRootPath für Store-Tests.
- `StubStreamTokenService` (in `MediaDurationProbeServiceTests`) — `IInternalMediaStreamTokenService`-Stub, wiederverwendbares Muster für Resolver-Tests.
