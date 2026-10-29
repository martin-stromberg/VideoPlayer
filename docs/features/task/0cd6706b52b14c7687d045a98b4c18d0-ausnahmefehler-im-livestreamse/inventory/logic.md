# Logik — betroffene Klassen

## `SegmenterProcessSupport`

Datei: `VideoWebPlayer/Services/SegmenterProcessSupport.cs`

`internal static` — gemeinsame Prozess-Infrastruktur der beiden ffmpeg-Segmentierer
(`FfmpegLiveStreamSegmenter`, `LiveStreamStandbySegmenter`): Prozessstart, `-i`-Angabe,
Transcode-Argumentblock, stderr-Drain, stdin-Pumpe.

| Methode | Sichtbarkeit | Kurzbeschreibung |
| --- | --- | --- |
| `StartRealProcess(ProcessStartInfo, ILogger, string)` (Z. 47) | `internal static` | Startet echten `Process`, wrappt ihn als `SegmenterProcess`; bei Fehlschlag `LogWarning` mit Ausnahme und `null`-Rückgabe. |
| `InputSpec(LiveStreamInput)` (Z. 64) | `internal static` | `-i`-Wert: quotierter Dateipfad, quotierte HTTP-URL oder `pipe:0`. |
| `NoStdinArg(LiveStreamInput)` (Z. 75) | `internal static` | `-nostdin ` für Datei-/URL-Eingaben; leer bei Pipe. |
| `TranscodeArgs(int)` (Z. 82) | `internal static` | Gemeinsamer H.264/AAC-Transcodeblock mit keyframe-ausgerichteten Segmentgrenzen. |
| `DrainStandardErrorAsync(ISegmenterProcess)` (Z. 92) | `internal static` | Liest stderr bis EOF; hält die letzten `StderrTailCapacityChars` (8192) Zeichen als Tail; erwartete Fehler (`IOException`/`ObjectDisposedException`/`OperationCanceledException`) werden **still** geschluckt (Z. 107–109). |
| `PumpInputAsync(ISegmenterProcess, LiveStreamInput, ILogger, string)` (Z. 123–154) | `internal static` | **Kern der Anforderung.** Öffnet `input.OpenStreamAsync`, pumpt per `source.CopyToAsync(process.StandardInput, CancellationToken.None)`. Catch-Pfade siehe unten. |

Verhalten von `PumpInputAsync` im Detail:

- `source is null` → `SegmenterPumpOutcome.SourceUnavailable` (kein Log).
- Erwarteter Stop (`IOException`/`ObjectDisposedException`/`OperationCanceledException`,
  Z. 139–144): `logger.LogDebug(ex, "[{Caller}] Eingabe-Pump beendet.", caller)` — das
  Ausnahmeobjekt `ex` wird übergeben, der Stacktrace landet im Log; Rückgabe
  `SegmenterPumpOutcome.Completed`.
- Unerwarteter Fehler (Z. 145–149): `logger.LogWarning(ex, ...)` →
  `SegmenterPumpOutcome.Failed`.
- `finally` (Z. 150–153): `process.StandardInput.Close()` immer (EOF für ffmpeg);
  Fehler beim Schließen werden geschluckt.

Keine Events. Konstanten: `StderrTailCapacityChars` (Z. 36).

## `FfmpegLiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

`public sealed`, implementiert `ILiveStreamSegmenter`. Besitzt den einen kontinuierlich
laufenden ffmpeg-HLS-Prozess des TV-Senders; serialisiert Start/Stop über `_gate`
(`SemaphoreSlim`, Z. 98).

| Methode | Sichtbarkeit | Kurzbeschreibung |
| --- | --- | --- |
| `FfmpegLiveStreamSegmenter(ILogger, TimeProvider?)` (Z. 110) | `public` ctor | Produktiv-Konstruktor; `_startProcess` → `SegmenterProcessSupport.StartRealProcess`. |
| `FfmpegLiveStreamSegmenter(ILogger, Func<ProcessStartInfo, ISegmenterProcess?>, TimeProvider?)` (Z. 117) | `internal` ctor | Testbarer Konstruktor mit injizierbarem Prozess-Factory. |
| `IsRunning` / `CurrentSlotId` / `LastUnexpectedExit` / `ExpectedPositionSeconds` (Z. 128–142) | `public` | Zustandsabfragen (Implementierung von `ILiveStreamSegmenter` bzw. Zusatz). |
| `StartAsync(LiveStreamJob, CancellationToken)` (Z. 145–186) | `public` | Unter `_gate`: erst `StopCoreAsync`, dann Prozessstart mit `RedirectStandardInput = job.Input.IsPipe`. Startet zwei Fire-and-forget-Tasks: `_ = Task.Run(() => DrainStandardErrorAsync(...))` (Z. 178) und — nur bei `IsPipe` — `_ = Task.Run(() => PumpInputAsync(process, job), CancellationToken.None)` (Z. 180). **Task-Referenzen werden verworfen.** |
| `StopAsync(CancellationToken)` (Z. 189–200) | `public` | Unter `_gate` → `StopCoreAsync`. |
| `StopCoreAsync(CancellationToken)` (Z. 202–229) | `private` | Setzt `_process`/`_job` auf `null` (Markierung „geplanter Stop" für `ReportProcessExit`), `Kill()` + `WaitForExitAsync` mit `StopTimeout` (10 s, Z. 90), `Dispose()`. **Holt weder Pump- noch Drain-Task ein.** |
| `DrainStandardErrorAsync(ISegmenterProcess, LiveStreamJob, DateTimeOffset)` (Z. 243–247) | `private` | Hülle um `SegmenterProcessSupport.DrainStandardErrorAsync`; anschließend `ReportProcessExit`. |
| `ReportProcessExit(...)` (Z. 259–295) | `private` | Klassifiziert Prozessende: geplanter Stop → Debug „wurde angehalten"; planmäßiges Laufende (Toleranz `Max(10, 2*SegmentSeconds)`) → Debug „endete planmaessig"; sonst Warnung mit maskiertem stderr-Tail + `LastUnexpectedExit` (inkl. `HttpTransportFailure` via `FfmpegErrorMarkers`). |
| `ReadExitCode(ISegmenterProcess)` (Z. 297–309) | `private` | Wartet bis `ExitDetectionTimeout` (5 s) auf Exit-Code, sonst −1. |
| `PumpInputAsync(ISegmenterProcess, LiveStreamJob)` (Z. 311–317) | `private` | Hülle um den Helper mit Caller-Tag `FfmpegLiveStreamSegmenter (Slot {SlotId})` (Protokoll-Tag aus der Anforderung); bei `SourceUnavailable` Warnung. |
| `BuildArguments(LiveStreamJob)` (Z. 329–346) | `internal static` | ffmpeg-Argumente: `-re -ss … -i {InputSpec}`, Transcodeblock, rollierendes HLS-Fenster (`append_list+discont_start+program_date_time`), `-t` Restlaufzeit. |

Keine Events. Relevante Felder: `_process`, `_job`, `_startedAt`,
`_lastUnexpectedExit` (`volatile`). Konstanten: `PlaylistFileName` (`live.m3u8`),
`PlaylistWindowSegmentCount` (12), `StopTimeout` (10 s), `ExitDetectionTimeout` (5 s).

## `LiveStreamStandbySegmenter`

Datei: `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`

`public sealed`. Einmaliger, gebundener Offline-Transcode eines Standby-Clips
(`-f segment`), nutzt denselben `SegmenterProcessSupport`-Helper.

| Methode | Sichtbarkeit | Kurzbeschreibung |
| --- | --- | --- |
| `LiveStreamStandbySegmenter(...)` (Z. 72) / interner Ctor (Z. 81) | `public`/`internal` | Produktiv-/Test-Ctor (injizierbarer `_startProcess`, `_transcodeTimeout`, `_stderrDrainTimeout`). |
| `TranscodeAsync(LiveStreamInput, string, CancellationToken)` (Z. 107–251) | `internal` | Prozess starten, `stderrTask` + `pumpTask` (nur bei `IsPipe`, Z. 158) anlegen; `WaitForExitAsync` mit `TranscodeTimeout` (120 s); Kill bei Timeout/Cancellation; stderr-Drain mit `StderrDrainTimeout` (10 s) einholen; **Pump-Task zeitgebunden einholen: `pumpTask.WaitAsync(PumpDrainTimeout)` (Z. 206–207, `PumpDrainTimeout` = 10 s, Z. 53)**; Exit-Code- und Segment-Auswertung. |
| `BuildArguments(LiveStreamInput, string)` (Z. 258–266) | `internal static` | Segment-Muxer-Argumente inkl. `-t 60`-Cap. |
| `PumpInputAsync(ISegmenterProcess, LiveStreamInput)` (Z. 268–278) | `private` | Hülle um den Helper (Caller `LiveStreamStandbySegmenter`); bei `SourceUnavailable` Warnung, bei nicht-`Completed` `process.Kill()` (ffmpeg würde sonst ewig auf stdin warten). |
| `ProbeDurationAsync(string, CancellationToken)` (Z. 280–329) | `private` | ffprobe-Dauerprobe pro erzeugtem Segment mit 10-s-Timeout. |

Begleit-Record `StandbyTranscodedSegment` (Z. 16). Konstanten u. a.:
`MaxStandbyDurationSeconds` (60), `SegmentSeconds` (4), `PumpDrainTimeout` (10 s),
`StderrDrainTimeout` (10 s), `TranscodeTimeout` (120 s).

## `LiveStreamWorker`

Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs`

`public sealed : BackgroundService`. Auslösekontext: ruft pro Tick
`LiveStreamCoordinator.SynchronizeAsync` in frischem DI-Scope (Z. 109–110); stoppt den
Segmentierer, wenn das Feature-Flag aus ist (Z. 98–100). Keine eigene Pump-Logik —
voraussichtlich unverändert.

| Methode | Sichtbarkeit | Kurzbeschreibung |
| --- | --- | --- |
| `ExecuteAsync(CancellationToken)` (Z. 53–155) | `protected override` | Startaufräumen (`LiveStreamSegmentStore.ClearAsync`), `LiveStreamStandbyProvider.RefreshAsync`, dann Poll-Schleife mit `PollCap` (30 s) / `IProgramScheduleSignal`-Wakeup. |
| `LogFfmpegUnavailableThrottled(string)` (Z. 157–164) | `private` | Warnung max. alle 5 min. |

## `LiveStreamCoordinator`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`

`public sealed`. Bringt Segmentierung und Sendeplan in Einklang; nutzt
`ILiveStreamSegmenter` (`_segmenter`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
| --- | --- | --- |
| `SynchronizeAsync(DateTimeOffset, CancellationToken)` (Z. 81–87) | `public` | Sync + Retention-Sweep; Rückgabe `LiveStreamSyncResult`. |
| `SynchronizeStreamAsync(DateTimeOffset, CancellationToken)` (Z. 89–161) | `private` | ffmpeg-Pfad prüfen → `StopAsync` bei fehlendem ffmpeg/Sendelücke/nicht auflösbarem Medium; `useHttpInput`-Entscheidung aus `_segmenter.LastUnexpectedExit` (Z. 111: HTTP-Transportfehler desselben Slots → Pipe-Fallback); `IsRunning && CurrentSlotId` + Drift-Prüfung (`MaxDriftSeconds` 30, Z. 135–145); Job bauen und `_segmenter.StartAsync(job)` (Z. 158) — Sendeplatz-Wechsel läuft hierüber. |

## `LiveStreamMediaResolver`

Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`

`public sealed`. Ermittelt `LiveStreamInput` je Sendeplatz: `ForFile` bei lokaler Quelle
(Z. 119), `ForHttp` bei Remote-Quelle mit aktivem HTTP-Eingang (Z. 129), sonst
`ForStream` (Z. 132) — einzige Stelle, die produktiv eine Pipe-Eingabe erzeugt.

## `LiveStreamInput`

Datei: `VideoWebPlayer/Services/LiveStreamInput.cs`

`public sealed record LiveStreamInput(string? FilePath, Func<CancellationToken, Task<Stream?>>? OpenStreamAsync, string? HttpUrl)`.
Fabriken `ForFile` (Z. 23), `ForStream` (Z. 26), `ForHttp` (Z. 31); `IsPipe` ⇔
`OpenStreamAsync is not null` (Z. 34).

## `LiveStreamJob` / `LiveStreamExitInfo` / `StandbyTranscodedSegment`

- `LiveStreamJob` (`VideoWebPlayer/Services/LiveStreamJob.cs`, Z. 14–21): Record mit
  `SlotId`, `Input`, `PositionSeconds`, `RemainingSeconds`, `SegmentSeconds`,
  `SegmentDirectory`, `FfmpegPath`.
- `LiveStreamExitInfo` (`VideoWebPlayer/Services/LiveStreamExitInfo.cs`, Z. 18): Record
  `SlotId`, `ExitCode`, `HttpTransportFailure`, `OccurredAtUtc` — Diagnose für den
  Input-Fallback des Koordinators.
- `StandbyTranscodedSegment` (`LiveStreamStandbySegmenter.cs`, Z. 16): `FileName` + `Duration`.

## `FfmpegErrorMarkers`

Datei: `VideoWebPlayer/Services/FfmpegErrorMarkers.cs`

`internal static`; `ContainsHttpTransportError(string?)` (Z. 32) prüft stderr-Tail gegen
`HttpTransportErrorMarkers` (Z. 19–27); wird in `ReportProcessExit` nur für HTTP-Eingaben
ausgewertet.

Querverweise: `FfmpegLiveStreamSegmenter` und `LiveStreamStandbySegmenter` nutzen
`SegmenterProcessSupport.PumpInputAsync`/`DrainStandardErrorAsync`/`InputSpec`/`NoStdinArg`/
`TranscodeArgs`/`StartRealProcess`. `LiveStreamCoordinator` kennt nur `ILiveStreamSegmenter`.
`LiveStreamWorker` kennt nur `LiveStreamCoordinator` + `ILiveStreamSegmenter` (Feature-Flag-Stop).
