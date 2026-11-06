# Tasks: Segmenter stderr-Logging + seekfähiger HTTP-Input (N15, Schritt 19)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `FfmpegErrorMarkers` (internal static) anlegen; `FfprobeMediaDurationProbe` darauf umstellen | Offen | — |
| 2 | Logik | `LiveStreamInput.HttpUrl` + `ForHttp` + `IsPipe` auf `OpenStreamAsync is not null`; `BuildArguments`-URL-Zweig | Offen | `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` |
| 3 | Logik | `IInternalMediaStreamTokenService.TryCreateStreamUri(long, TimeSpan)` + gemeinsamer Aussteller; Probe-Pfad unverändert | Offen | `TryCreateStreamUri_*` (3 Tests) |
| 4 | Logik | stderr-Tail + vorzeitige-Exit-Klassifikation + `LogWarning` (maskiert) in `DrainStandardErrorAsync` | Offen | `UnexpectedExit_*`-Tests |
| 5 | Logik | `LiveStreamExitInfo` + `ILiveStreamSegmenter.LastUnexpectedExit` (Set/Reset) | Offen | `UnexpectedExit_SetsLastUnexpectedExit_*`, `StartAsync_ClearsLastUnexpectedExit` |
| 6 | Logik | Debug-Log der maskierten ffmpeg-Args in `StartAsync` | Offen | Teil der Segmenter-Tests |
| 7 | Konfiguration | `LiveStreamOptions` (`LiveStream:UseHttpInput`) + DI-Bindung + `appsettings.json` | Offen | `ResolveAsync_OptionDisabled_FallsBackToPipe` |
| 8 | Logik | `LiveStreamMediaResolver`: Token-Dienst + Options injizieren, `ResolveAsync`-Signatur (runDuration, useHttpInput), HTTP-Primärpfad | Offen | `ResolveAsync_*`-Tests (5) |
| 9 | Logik | `LiveStreamCoordinator`: Fallback aus `LastUnexpectedExit`, `runDuration`-Übergabe, Restart-Semantik-Kommentar | Offen | `SynchronizeAsync_*`-Tests (2) |
| 10 | Tests | `FfmpegLiveStreamSegmenterTests`: stderr-Fake erweitern + 7 neue Tests | Offen | — |
| 11 | Tests | `LiveStreamMediaResolverTests`: Token-Stub + Options + 5 neue Tests | Offen | — |
| 12 | Tests | `LiveStreamCoordinatorTests`: `LastUnexpectedExit` im Fake + 2 neue Tests | Offen | — |
| 13 | Tests | `InternalMediaStreamTokenServiceTests`: 3 neue `TryCreateStreamUri`-Tests | Offen | — |
| 14 | Tests | `MediaDurationProbeServiceTests.StubStreamTokenService` um `TryCreateStreamUri` erweitern | Offen | Kompiliert + Suite |
| 15 | Doku | `docs/help/tv-sender.md` (HTTP-Input, Option, stderr-Diagnose), `docs/RELEASE_NOTES.md` | Offen | — |
| 16 | Verify | Build Debug+Release 0 Fehler, volle Suite grün | Offen | test-results.md |
