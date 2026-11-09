# Bestandsaufnahme: N15 — Segmenter-Restart-Schleife (stderr-Logging + seekfähiger HTTP-Input)

Analyse des Live-Stream-Segmentierungspfads des automatisierten TV-Senders bezogen auf
Nachbesserung N15 (Live-Befund 2026-10-22: ffmpeg-Restart alle ~14–17 s, `pipe:0`-Pump bricht,
stderr landet nicht im Log).

## Zusammenfassung

- `FfmpegLiveStreamSegmenter` startet einen ffmpeg-Prozess pro laufendem Sendeplatz
  (`-re -ss -t -f hls`). stderr wird kontinuierlich gedrained (`DrainStandardErrorAsync`),
  aber **vollständig verworfen** — die Prio-1-Lücke aus N15 ist bestätigt: ein selbstbeendendes
  ffmpeg hinterlässt keine Diagnose im Log.
- `LiveStreamInput` kennt genau zwei Eingabearten: `ForFile` (lokaler Pfad, `-nostdin`) und
  `ForStream` (Pump nach stdin, `pipe:0`). `LiveStreamMediaResolver` liefert für
  `LocalDirectory` den physischen Pfad, für alle entfernten Quellen den Stream-Pump — es gibt
  **keinen HTTP-Input**.
- Der interne Range-Endpunkt aus N9 existiert bereits vollständig:
  `InternalMediaStreamController` (`api/internal/media-stream/{token}`,
  `enableRangeProcessing: true`), Token-Ausstellung über
  `IInternalMediaStreamTokenService.TryCreateProbeUri` (Singleton, In-Memory-Token,
  TTL `MediaProbeOptions.EffectiveHttpProbeTokenLifetimeSeconds` = 600 s), Maskierung über
  `InternalMediaStreamTokenService.MaskStreamTokenInText` (`/api/internal/media-stream/***`).
- **TTL-Falle:** die Probe-TTL (600 s) deckt lange Segmentierungsläufe (Sendeplätze bis zu
  Stunden) nicht ab — ein Stream-Token braucht eine an der Restlaufzeit bemessene TTL.
- `LiveStreamCoordinator` ist scoped (je Worker-Tick neu) und zustandslos; Fehlerzustand eines
  abgestürzten Laufs kann nur im Singleton `FfmpegLiveStreamSegmenter` gehalten werden.
- Restart-Semantik: bei `IsRunning == false` startet der Coordinator an `now.PositionSeconds`
  neu → die beobachteten ~30 s-Sprünge sind positionsbasiert korrekt (Tick-Latenz, Poll-Cap
  30 s), keine Doppelbelegung; sie überspringen aber je Zyklus ~30–45 s Inhalt.
- HTTP-Transportfehler-Klassifikation existiert als Muster in `FfprobeMediaDurationProbe`
  (`HttpTransportErrorMarkers` → `MediaDurationProbeResult.UriUnsupported`).
- `LiveStream:UseHttpInput`-Option existiert nicht; Stream-relevante Optionen stehen in
  `MediaProbeOptions` (Section `MediaProbe`) bzw. `Setup`-Spalten via `ProgramSettingsService`.

Test-Ausgangszustand: siehe [Tests](inventory/tests.md) — volle Suite zuletzt in Schritt 18 bei
identischem Code-Stand (2124 grün + 1 Alt-Skip); nur Doku-Commits danach.

## Details

- [Logik und Abläufe](inventory/logic.md)
- [Interfaces und Records](inventory/interfaces.md)
- [Tests](inventory/tests.md)
