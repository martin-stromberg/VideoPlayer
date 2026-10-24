# Bestandsaufnahme: Interfaces

## `IInternalMediaStreamTokenService`

Datei: `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs`

Implementierung: `InternalMediaStreamTokenService` (Singleton). Konsumenten:
`InternalMediaStreamController` (Auflösung), `MediaDurationProbeService` (Probe-Tokens,
optional), `LiveStreamMediaResolver` (Stream-Tokens).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `TryCreateProbeUri` | `long mediaItemId` | `string?` | Stellt ein Probe-Token aus und liefert die absolute Loopback-Probe-URI (`/api/internal/media-stream/{token}`), oder `null` wenn `MediaProbe:HttpProbeEnabled` aus ist oder keine HTTP-Adresse ermittelbar. TTL = `EffectiveHttpProbeTokenLifetimeSeconds` (Default 600 s). |
| `TryCreateStreamUri` | `long mediaItemId`, `TimeSpan lifetime` | `string?` | Stellt ein Stream-Token mit anforderer-gewählter Lebensdauer aus (geclamppt auf ≥ 60 s) und liefert die Loopback-Stream-URI — für Langläufer wie den Live-Stream-Segmentierer; **nicht** an `HttpProbeEnabled` gekoppelt. `null` ohne HTTP-Adresse. |
| `ResolveMediaItemId` | `string token` | `long?` | Löst ein Token zur `MediaItemId` auf; `null` bei unbekannt/abgelaufen/leer. Einzige Berechtigungsprüfung des Endpunkts. |

## `ILiveStreamSegmenter` (relevant für Variante-A-Konsequenzen)

Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`

Implementierung: `FfmpegLiveStreamSegmenter` (Singleton). Konsument:
`LiveStreamCoordinator`/`LiveStreamWorker`.

| Methode/Eigenschaft | Typ | Zweck |
|---------------------|-----|-------|
| `StartAsync(LiveStreamJob, CancellationToken)` | Methode | Startet die ffmpeg-Segmentierung eines Jobs (u. a. mit `Input.HttpUrl` als `-i`-Argument). |
| `StopAsync(CancellationToken)` | Methode | Regulärer Stopp-Pfad (`process.Kill()`) — so wird ein laufender ffmpeg-Read beim Flag-Wechsel heute beendet (`LiveStreamWorker` Z. 79). |
| `IsRunning` / `CurrentSlotId` | Eigenschaft | Prozesszustand für den Coordinator-Tick. |
| `LastUnexpectedExit` | `LiveStreamExitInfo?` | Diagnose des letzten unerwarteten Exits inkl. `HttpTransportFailure` — steuert den HTTP→Pipe-Fallback (`LiveStreamCoordinator` Z. 111). |
| `ExpectedPositionSeconds` | `double?` | Erwartete Programmposition für die Drift-Erkennung. |

## `IMediaSourceReader` (nur die vom Endpunkt genutzte Methode)

Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OpenFileStream` | `MediaCollection collection`, `string fileName` | `Stream?` | Öffnet die Datei über den zur Quelle passenden Reader (lokal/SFTP) — derselbe Lesepfad wie die Wiedergabe; vom `InternalMediaStreamController` Z. 73 genutzt. |

## `IMediaDurationProbe` (nur die Token-relevante Methode)

Datei: `VideoWebPlayer/Services/IMediaDurationProbe.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProbeUriAsync` | `string uri`, `CancellationToken` | `Task<MediaDurationProbeResult>` | ffprobe-Aufruf gegen eine URI — hier die tokenisierte Loopback-URL des Endpunkts. HTTP-Transportfehler auf `http(s)`-Eingängen werden als `UriUnsupported` gewertet (`MediaDurationProbeResult.UriUnsupportedFailure`) und lösen beim Aufruf den `sftp://`/`pipe:0`-Fallback aus. |
