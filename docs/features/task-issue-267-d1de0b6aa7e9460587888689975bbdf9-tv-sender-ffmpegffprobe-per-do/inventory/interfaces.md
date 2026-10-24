# Bestandsaufnahme: Interfaces

## `IFfprobeLocator`

**Datei:** `VideoWebPlayer/Services/IFfprobeLocator.cs` — DI: `AddSingleton<IFfprobeLocator, FfprobeLocator>()` (`ServiceCollectionExtensions.cs:237`)

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `Resolve()` | — | `string?` | Aufgelöster ffprobe-Pfad oder `null`, wenn kein Binary gefunden |

Aufrufer: `MediaDurationProbeService.ProbePendingAsync`, `FfprobeMediaDurationProbe.ProbeAsync`; Tests: `PresentLocator`-Stub (`VideoWebPlayer.Tests/Helpers/MediaProbeTestDoubles.cs:12-15`).

## `IFfmpegLocator`

**Datei:** `VideoWebPlayer/Services/IFfmpegLocator.cs` — DI: `AddSingleton<IFfmpegLocator, FfmpegLocator>()` (`ServiceCollectionExtensions.cs:238`)

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `Resolve()` | — | `string?` | Aufgelöster ffmpeg-Pfad oder `null`, wenn kein Binary gefunden |

Aufrufer: `LiveStreamCoordinator.SynchronizeStreamAsync` (einzige Stelle).

## `IMediaDurationProbe`

**Datei:** `VideoWebPlayer/Services/IMediaDurationProbe.cs` — DI: `AddSingleton<IMediaDurationProbe, FfprobeMediaDurationProbe>()` (`ServiceCollectionExtensions.cs:243`)

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `ProbeFileAsync` | `string filePath`, `CancellationToken` | `Task<MediaDurationProbeResult>` | Lokale Datei per Dateipfad proben |
| `ProbeUriAsync` | `string uri`, `CancellationToken` | `Task<MediaDurationProbeResult>` | Seekfähige Remote-URI (sftp://, http://-Loopback); `UriUnsupported` signalisiert Fallback |
| `ProbeStreamAsync` | `Stream input`, `CancellationToken` | `Task<MediaDurationProbeResult>` | Gestreamter Probe via `pipe:0` |

Test-Double: `FixedDurationProbe` (`MediaProbeTestDoubles.cs:18-32`), weitere private Stubs in `MediaDurationProbeServiceTests.cs` (~Z. 640).

## `ILiveStreamSegmenter`

**Datei:** `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` — DI: `AddSingleton<ILiveStreamSegmenter, FfmpegLiveStreamSegmenter>()` (`ServiceCollectionExtensions.cs:239`)

| Member | Typ | Zweck |
|---|---|---|
| `IsRunning` | `bool` | Läuft ein Segmentierungsprozess |
| `CurrentSlotId` | `long?` | Sendeplatz des laufenden Jobs |
| `ExpectedPositionSeconds` | `double?` | Erwartete Wiedergabeposition (Drift-Prüfung) |
| `LastUnexpectedExit` | `LiveStreamExitInfo?` | Diagnose des letzten unerwarteten Exits (HTTP-Fallback-Entscheidung) |
| `StartAsync(LiveStreamJob, CancellationToken)` | `Task` | Job starten (vorheriger Lauf wird gestoppt) |
| `StopAsync(CancellationToken)` | `Task` | Laufenden Prozess stoppen |

Test-Double: Fake-Segmenter in `LiveStreamCoordinatorTests.cs` / `LiveStreamWorkerTests.cs`.

## `ISegmenterProcess` (internal)

**Datei:** `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs:16-35` — Prozess-Abstraktion für Tests: `HasExited`, `ExitCode`, `StandardInput`, `StandardError`, `WaitForExitAsync`, `Kill`. Implementiert von `SegmenterProcess`; Fakes in `FfmpegLiveStreamSegmenterTests.cs`.

## Es existiert **kein** `IFfmpegToolDownloadService`

Ein Download-/Installations-Contract für ffmpeg/ffprobe fehlt komplett — weder Interface noch Implementierung, kein Snapshot-Record für Download-Fortschritt, keine DI-Registrierung.

## Verwandte, nicht direkt betroffene Interfaces (Kontext)

- `IProgramScheduleSignal` (`ProgramScheduleSignal.cs`): `NotifyScheduleChanged()`, `WaitAsync(ct)` — weckt `LiveStreamWorker` und `ProgramScheduleWorker`.
- `IInternalMediaStreamTokenService` (`IInternalMediaStreamTokenService.cs`): `TryCreateProbeUri(itemId)` u. a. — loopback-URIs für Probes/Segmentierung.
- `IMediaSourceReader` (`IMediaSourceReader.cs`): `OpenFileStream` u. a. — Fallback-Eingang für Remote-Probes.
- `IBackgroundProcessingGate` (`Services/Backups/IBackgroundProcessingGate.cs`): `EnterOperationAsync(name, ct)` — Lease für Hintergrundläufe (LiveStreamWorker, MediaDurationProbeWorker laufen dahinter).
