# Bestandsaufnahme — Logik und Abläufe

## `FfmpegLiveStreamSegmenter`
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` (Singleton, `ILiveStreamSegmenter`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync` | public | Stoppt ggf. laufenden Job (`StopCoreAsync`), baut `ProcessStartInfo` (`Arguments = BuildArguments(job)`, `RedirectStandardInput = job.Input.IsPipe`, `RedirectStandardError = true`), startet Prozess über `_startProcess`-Seam, startet `DrainStandardErrorAsync` und bei Pipe-Input `PumpInputAsync` als `Task.Run`. |
| `StopAsync` | public | Sperrt `_gate`, ruft `StopCoreAsync`. |
| `StopCoreAsync` | private | Setzt `_process`/`_job` auf `null`, killt Prozess (inkl. Prozessbaum), wartet max. 10 s, loggt „beendet". |
| `DrainStandardErrorAsync` | private | Liest `StandardError` in 8-KB-Puffer bis EOF — **wirft alle Bytes weg**. |
| `PumpInputAsync` | private | Öffnet `job.Input.OpenStreamAsync`, kopiert nach `StandardInput`; `IOException` („Die Pipe wurde beendet") nur `LogDebug` — hier schlägt der Live-Befund auf. |
| `BuildArguments` | internal static | `-hide_banner -loglevel warning -y [-nostdin] -re -ss {pos} -i {input} -map … -c:v libx264 … -f hls … -t {remaining} "{playlist}"`; `input` = quotierter Pfad oder `pipe:0`. |
| `StartRealProcess` | private static | `Process.Start` → `SegmenterProcess`, `null` bei Fehler. |

Zustand: `_process`, `_job`, `_startedAt` (für `ExpectedPositionSeconds` = `PositionSeconds + Laufzeit`).
`IsRunning` = `_process is { HasExited: false }` — ein selbstbeendetes ffmpeg ist für den
Coordinator erst beim nächsten Tick sichtbar.

## `SegmenterProcess` / `ISegmenterProcess`
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

`ISegmenterProcess`: `HasExited`, `ExitCode` (-1 laufend), `StandardInput`, `StandardError`,
`WaitForExitAsync(ct)`, `Kill()`, `IDisposable`. `SegmenterProcess` wrappt
`System.Diagnostics.Process` (`Kill(entireProcessTree: true)`). Testseam existiert
(`FakeSegmenterProcess` in den Tests).

## `LiveStreamCoordinator`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (scoped, je Worker-Tick neue Instanz)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SynchronizeAsync` | public | Pro Tick: `SynchronizeStreamAsync` + Retention-Sweep. |
| `SynchronizeStreamAsync` | private | ffmpeg-Pfad prüfen → `IProgramGuideService.GetNowAsync` → `LiveStreamMediaResolver.ResolveAsync` → Drift-Check (`MaxDriftSeconds = 30`, nur wenn `IsRunning && CurrentSlotId == SlotId`) → `LiveStreamJob` bauen + `StartAsync`. Bei `IsRunning == false` (Crash) wird ohne Drift-Check an `now.PositionSeconds` neu gestartet. |

Rein zeitbasiert: Neustart landet immer an der aktuellen Programmposition — erklärt die
beobachteten +~30 s-Sprünge je Restart-Zyklus (Tick-Latenz bis `PollCap` = 30 s).

## `LiveStreamWorker`
Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs` (`BackgroundService`)

Initialdelay 10 s, einmaliges `ClearAsync` des Segment-Verzeichnisses, danach Schleife:
Scope → `IBackgroundProcessingGate`-Lease → `LiveStreamCoordinator.SynchronizeAsync` →
Delay = min(`WakeAtUtc`-Abstand, `PollCap` 30 s) oder `IProgramScheduleSignal.WaitAsync`.
Loggt `Started`/`MediaUnavailable` als Info, `FfmpegUnavailable` gedrosselt (5 min).

## `LiveStreamMediaResolver`
Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs` (scoped)

`ResolveAsync(mediaItemId, ct)`: lädt `MediaItem` + `MediaCollection` + `MediaSource`
(AsNoTracking). `LocalDirectory` → `LocalMediaSourceReader.ResolveLocalFilePath` →
`LiveStreamInput.ForFile`. Alle anderen Quelltypen →
`LiveStreamInput.ForStream(_ => OpenFileStream(collection, fileName))`. `null` bei
fehlendem Item/Collection/Dateinamen/Pfad (Slot wirkt wie Sendelücke).

## `InternalMediaStreamTokenService`
Datei: `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs` (Singleton)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TryCreateProbeUri` | public | `null` wenn `!HttpProbeEnabled` oder keine http-Basis-URL; sonst 256-bit-Hex-Token (In-Memory `ConcurrentDictionary`, TTL `EffectiveHttpProbeTokenLifetimeSeconds` = 600 s), Rückgabe `{baseUrl}/api/internal/media-stream/{token}`. |
| `ResolveMediaItemId` | public | Token → `MediaItemId`, `null`/entfernt bei unbekannt/abgelaufen. |
| `MaskStreamTokenInText` | internal static | Regex `/api/internal/media-stream/[0-9a-fA-F]{16,}` → `…/***`. |
| `ResolveBaseUrl` | private | `HttpProbeBaseUrl` (konfiguriert) oder erste `http://`-Adresse aus `IServerAddressesFeature`, Wildcard/Loopback → `127.0.0.1`; HTTPS-only → `null`. Ergebnis gecacht, Negativ-Ergebnis nicht (Startup-Race). |
| `SweepExpiredTokens` | private | Räumt abgelaufene Token bei jeder Ausstellung. |

## `InternalMediaStreamController`
Datei: `VideoWebPlayer/Controllers/InternalMediaStreamController.cs`

`GET api/internal/media-stream/{token}` — ohne `[BearerTokenCheck]` (Token im Pfad ist die
einzige Credential); `ResolveMediaItemId` → Item + Collection + Source laden →
`IMediaSourceReader.OpenFileStream` → `File(stream, …, enableRangeProcessing: true)`.
Unbekannt/abgelaufen → 404 (kein Preisgeben, ob Token je existierte). Range-fähig: ffmpeg kann
via HTTP-Range seeken.

## `FfprobeMediaDurationProbe` (Vorbild N9/N12)
Datei: `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs`

- `HttpTransportErrorMarkers` (private): „HTTP error", „Server returned", „Connection refused",
  „Connection timed out", „No route to host", „Failed to connect" → `UriUnsupported` →
  `MediaDurationProbeService` kaskadiert auf `sftp://`/`pipe:0`.
- `ProtocolNotFoundMarker`, `PermanentErrorMarkers` („moov atom not found" u. a.) — nur für
  seekfähige Inputs ausgewertet.
- Maskierung vor jedem Log: `MaskStreamTokenInText(SftpProbeUriBuilder.MaskCredentialsInText(…))`
  bzw. `MaskCredentials`/`MaskStreamTokenInText` für die Display-URI.

## `MediaDurationProbeService` (N9-Kaskade, Vorbild)
`ProbeRemoteItemAsync`: `TryCreateProbeUri` → `ProbeUriAsync`; nur bei `UriUnsupported`
weiter zu `sftp://`-URI (SFTP) bzw. `OpenFileStream`-Pipe. Zeigt das etablierte
Fallback-Muster: Transportfehler kaskadieren, Inhaltsfehler nicht.

## `MediaProbeOptions`
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs` (Section `MediaProbe`)

`HttpProbeEnabled` (Default `true`), `HttpProbeBaseUrl`, `HttpProbeTokenLifetimeSeconds`
(600, `EffectiveHttpProbeTokenLifetimeSeconds`), dazu Ffprobe-/Ffmpeg-Pfade, Timeouts,
Retry, Parallelität. Keine Live-Stream-spezifischen Optionen.

## `ProgramSettingsService` (Live-Stream-Setup)
`GetLiveStreamSegmentLengthAsync` (Default 6 s), `GetLiveStreamSegmentDirectoryAsync`,
`GetLiveStreamSegmentRetentionAsync` (30 min), `UpdateLiveStreamSettingsAsync` — Setup-Spalten,
Admin-UI in `TVSenderAdmin`.

## Ablauf Restart-Schleife (Live-Befund rekonstruiert)

1. Tick: laufender Slot 5 → `ResolveAsync` → `ForStream` (SFTP) → `StartAsync` an Position P.
2. `PumpInputAsync` pumpft SFTP-Stream nach stdin; ffmpeg liest `pipe:0`.
3. ffmpeg beendet sich nach ~15–17 s selbst (stderr unbekannt — verworfen).
4. Pipe bricht → `PumpInputAsync` `IOException` → `LogDebug` (im Log kaum sichtbar).
5. Nächster Tick (≤ 30 s): `IsRunning == false` → `StartAsync` an `PositionSeconds ≈ P + 30 s`.
6. Wiederholung → Position läuft dem Programm in ~30 s-Sprüngen hinterher, je Zyklus gehen
   ~30–45 s Sendeinhalt verloren; Playlist-Fenster zeigt Lücken → „momentan nicht verfügbar".
