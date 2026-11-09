# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- `FfmpegErrorMarkers` angelegt (`internal static`, `HttpTransportErrorMarkers` +
  `ContainsHttpTransportError`); `FfprobeMediaDurationProbe` delegiert — Markertexte unverändert.
- `LiveStreamInput`: drittes Feld `HttpUrl` (Default `null`), `ForHttp`, `IsPipe` →
  `OpenStreamAsync is not null`; `BuildArguments` wählt Pfad → HttpUrl → `pipe:0`,
  `-nostdin`/Stdin-Redirect am `IsPipe`-Flag.
- `IInternalMediaStreamTokenService.TryCreateStreamUri(long, TimeSpan)` +
  Implementierung (gemeinsamer privater `TryCreateUri`; `TryCreateProbeUri` delegiert mit
  Probe-TTL und bleibt an `HttpProbeEnabled` gekoppelt; Stream-Variante nicht an der Option,
  Untergrenze 60 s).
- stderr-Tail (8 KB) in `DrainStandardErrorAsync`; `ReportProcessExit` mit
  Vorzeitigkeits-Toleranz (`max(10 s, 2×SegmentSeconds)`), maskiertem Warning-Log
  (Exit-Code + Tail + HTTP-Transport-Flag); `LastUnexpectedExit` (`LiveStreamExitInfo`)
  gesetzt/in `StartAsync` zurückgesetzt; maskiertes Debug-Log der ffmpeg-Argumente.
- `LiveStreamOptions` (`LiveStream:UseHttpInput`, Default `true`), DI-Bindung,
  `appsettings.json`-Eintrag.
- `LiveStreamMediaResolver`: Token-Dienst + Options injiziert, `ResolveAsync`-Signatur
  `(mediaItemId, runDuration, useHttpInput, ct)`, HTTP-Primärpfad mit
  `runDuration + 15 min`-TTL (Default 45 min), Pipe-Fallback.
- `LiveStreamCoordinator`: `useHttpInput` aus `LastUnexpectedExit` (nur Transportfehler,
  nur selber Slot), `runDuration` aus `RemainingSeconds`, Fallback-`LogInformation`,
  Kommentar zur positionsbasierten Restart-Semantik (+30 s-Analyse).
- Tests: 21 neue/angepasste Tests (8 Segmenter, 6 Resolver, 4 Coordinator, 4 Token-Dienst
  — davon ersetzt `ResolveAsync_RemoteSource_ReturnsHttpUrl` den alten SFTP-Pipe-Test);
  Fakes/Stubs erweitert (`FakeSegmenter` ×2, `StubStreamTokenService` ×2,
  `NullStreamTokenService`, `ControlledPipeStream`).

## Offene Aufgaben

Keine.

## Hinweise

- Restart-Analyse (N15-Punkt 3): Die ~30 s-Sprünge sind positionsbasiert korrekt — der
  Coordinator startet am nächsten Tick (Poll-Cap 30 s) an `now.PositionSeconds`, also auf
  der Live-Position; es existiert immer nur ein Prozess (`_gate`), keine Doppelbelegung.
  Dokumentiert als Kommentar in `LiveStreamCoordinator` und in `docs/help/tv-sender.md`.
