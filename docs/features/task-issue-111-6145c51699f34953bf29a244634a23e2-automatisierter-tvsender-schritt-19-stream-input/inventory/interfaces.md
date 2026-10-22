# Bestandsaufnahme — Interfaces und Records

## `ILiveStreamSegmenter`
Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` (Singleton: `FfmpegLiveStreamSegmenter`)

| Member | Typ | Zweck |
|--------|-----|-------|
| `IsRunning` | `bool` | Läuft ein Segmentierungsprozess? |
| `CurrentSlotId` | `long?` | Slot des aktuellen Jobs. |
| `ExpectedPositionSeconds` | `double?` | Erwartete Position = Jobposition + Prozesslaufzeit. |
| `StartAsync(LiveStreamJob, CancellationToken)` | `Task` | Job starten (laufenden vorher stoppen). |
| `StopAsync(CancellationToken)` | `Task` | Laufenden Job stoppen. |

Fehlt: jede Form von Exit-Diagnose (Exit-Code, stderr, Fehlerklassifikation).

## `ISegmenterProcess` (internal)
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

`HasExited`, `ExitCode`, `StandardInput`, `StandardError`, `WaitForExitAsync`, `Kill`,
`IDisposable` — Testseam für `FakeSegmenterProcess`.

## `IInternalMediaStreamTokenService`
Datei: `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs` (Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `TryCreateProbeUri` | `long mediaItemId` | `string?` | Loopback-URI mit kurzlebigem Token (Probe-TTL 600 s). |
| `ResolveMediaItemId` | `string token` | `long?` | Token → MediaItemId, `null` bei unbekannt/abgelaufen. |

Fehlt: Ausstellung mit anforderer-definierter TTL (Stream-Läufe > 600 s).
`MaskStreamTokenInText` ist `internal static` an der Implementierung, nicht am Interface.

## `LiveStreamInput` (record)
Datei: `VideoWebPlayer/Services/LiveStreamInput.cs`

`record LiveStreamInput(string? FilePath, Func<CancellationToken, Task<Stream?>>? OpenStreamAsync)`
mit `ForFile`, `ForStream`, `IsPipe => FilePath is null`. Fehlt: HTTP-URL-Eingabeart —
`IsPipe` muss mit einer dritten Art sauber differenzieren.

## `LiveStreamJob` (record)
Datei: `VideoWebPlayer/Services/LiveStreamJob.cs`

`(long SlotId, LiveStreamInput Input, double PositionSeconds, double RemainingSeconds,
int SegmentSeconds, string SegmentDirectory, string FfmpegPath)` — trägt alles, was
`BuildArguments` braucht; `RemainingSeconds` eignet sich als Basis der Stream-Token-TTL.

## `MediaDurationProbeResult` (Vorbild)
`record (TimeSpan? Duration, bool Transient) { UriUnsupported }` — `UriUnsupportedFailure`
markiert HTTP-Transportfehler/Scheme-Fehler und steuert die N9-Fallback-Kaskade in
`MediaDurationProbeService.ProbeRemoteItemAsync`.

## DI-Registrierung (`ServiceCollectionExtensions`)
`ILiveStreamSegmenter` → Singleton; `LiveStreamMediaResolver`, `LiveStreamSegmentStore`,
`LiveStreamCoordinator` → Scoped; `IInternalMediaStreamTokenService` → Singleton;
`LiveStreamWorker` → HostedService. Der Resolver kann den Token-Singleton direkt injizieren.
