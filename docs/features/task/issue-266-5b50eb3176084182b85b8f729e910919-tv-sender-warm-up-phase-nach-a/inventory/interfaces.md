# Interfaces — Bestandsaufnahme

## ILiveStreamSegmenter

Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `IsRunning` | — | `bool` | Läuft gerade ein Segmentierungsprozess? |
| `CurrentSlotId` | — | `long?` | Slot-ID des aktuellen Jobs, `null` ohne Job. |
| `ExpectedPositionSeconds` | — | `double?` | Erwartete Wiedergabeposition des laufenden Prozesses, `null` ohne Job. |
| `LastUnexpectedExit` | — | `LiveStreamExitInfo?` | Diagnose des letzten vorzeitig selbst beendeten Laufs (für den HTTP→Pipe-Fallback des Koordinators). |
| `StartAsync` | `LiveStreamJob job`, `CancellationToken` | `Task` | Startet einen Job; ein evtl. laufender wird vorher gestoppt. |
| `StopAsync` | `CancellationToken` | `Task` | Stoppt den laufenden Prozess, falls vorhanden. |

## ISegmenterProcess (internal)

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` (Zeilen 16–35)

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `HasExited` | — | `bool` | Prozess beendet? |
| `ExitCode` | — | `int` | Exit-Code, `-1` solange laufend. |
| `StandardInput` | — | `Stream` | stdin für Pipe-Eingang. |
| `StandardError` | — | `Stream` | stderr (muss gedraint werden). |
| `WaitForExitAsync` | `CancellationToken` | `Task` | Warten auf Prozessende. |
| `Kill` | — | `void` | Prozess samt Kindern töten. |

## IProgramGuideService

Datei: `VideoWebPlayer/Services/IProgramGuideService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `GetNowAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<DtoProgramGuideNow>` | „Was läuft jetzt und was kommt danach" — `Now` ist `null` in Sendelücken. |
| `GetUpcomingAsync` | `DateTimeOffset utcNow`, `TimeSpan preview`, `CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Begrenzte Programmübersicht ab `utcNow`. |

## IProgramScheduleSignal

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` (Zeilen 10–35)

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `NotifyScheduleChanged` | — | `void` | Sendeplan-Eingänge geändert → Worker aufwecken (unterdrückt während `BeginScan`). |
| `BeginScan` | — | `IDisposable` | Scan-Scope; währenddessen kein Aufwecken, am Ende einmalig. |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten bis ein Signal ansteht (koalesziert). |

## IFfmpegLocator

Datei: `VideoWebPlayer/Services/IFfmpegLocator.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `Resolve` | — | `string?` | ffmpeg-Pfad oder `null`, wenn nicht auffindbar. |

## IInternalMediaStreamTokenService

Datei: `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `TryCreateProbeUri` | `long mediaItemId` | `string?` | Kurzlebige Loopback-Probe-URI (ffprobe). |
| `TryCreateStreamUri` | `long mediaItemId`, `TimeSpan lifetime` | `string?` | Loopback-Stream-URI mit wählbarer Token-Lebensdauer (ffmpeg-HTTP-Eingang); `null` ohne nutzbare HTTP-Adresse. |
| `ResolveMediaItemId` | `string token` | `long?` | Token → Media-Item-ID, `null` bei unbekannt/abgelaufen. |

## IBackgroundProcessingGate

Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `IsPausedForRestore` | — | `bool` | Restore-Modus blockiert neue Operationen? |
| `ActiveOperationCount` | — | `int` | Anzahl aktiver Operationen. |
| `EnterOperationAsync` | `string name`, `CancellationToken` | `Task<IAsyncDisposable>` | Eintritt in eine Hintergrund-Schreiboperation (Lease). |
| `PauseForRestoreAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Pausiert neue Operationen bis aktive abgeschlossen sind. |
