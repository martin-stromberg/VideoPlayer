# Bestandsaufnahme: Interfaces

## `IProgramGuideService`
Datei: `VideoWebPlayer/Services/IProgramGuideService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNowAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<DtoProgramGuideNow>` | „Jetzt/Gleich"-Auskunft — Zeitbasis des Streams (`Now.PositionSeconds` = ffmpeg-Offset, `Now.EndUtc` = Umschaltzeitpunkt, `Next` = Nachfolger/Lückenende) |
| `GetUpcomingAsync` | `DateTimeOffset utcNow`, `TimeSpan preview`, `CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Begrenzte Vorschau (für den Stream nicht benötigt) |

## `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OpenFileStream` | `MediaCollection collection`, `string fileName` | `Stream?` | Quell-unabhängiger Datei-Stream — Pipe-Eingang für ffmpeg bei Nicht-Local-Quellen |
| `FileExistsAsync` | `MediaCollection`, `string` | `Task<bool>` | Existenzprüfung |
| `ReadFileStreamAsync` / `ReadFileAsync` | `MediaCollection`, `string` | `Task<Stream?>`/`Task<string?>` | Dateiinhalte |
| `ReadRootDirectory` / `ReadDirectoryEntries` | `MediaSource`/`MediaCollection` | `IEnumerable<MediaEntry>` | Verzeichnislisten (Scan) |

## `IFfprobeLocator`
Datei: `VideoWebPlayer/Services/IFfprobeLocator.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Resolve` | — | `string?` | Pfad des ffprobe-Binaries oder `null` — identische Signatur für `IFfmpegLocator` vorgesehen |

## `IBackgroundProcessingGate`
Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `EnterOperationAsync` | `string name`, `CancellationToken` | `Task<IAsyncDisposable>` | Lease für Hintergrundoperationen — verhindert, dass ein Backup-Restore während Stream-Entscheidungen/Retention läuft |
| `PauseForRestoreAsync` | `CancellationToken` | `Task<IAsyncDisposable>` | Restore-Exklusivität |
| `IsPausedForRestore` / `ActiveOperationCount` | — | `bool`/`int` | Zustandsabfragen |

## `IProgramScheduleSignal`
Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Weckt den Plan-Worker bei Regel-/Medienänderungen — auch für den Stream-Worker relevant (verdrängter laufender Slot soll zeitnah umgeschaltet werden) |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten auf Signal (koaleszierend) |
| `BeginScan` | — | `IDisposable` | Scan-Suppression |

## `IMediaDurationProbe`
Datei: `VideoWebPlayer/Services/` (`FfprobeMediaDurationProbe.cs` enthält die Implementierung)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProbeFileAsync` / `ProbeStreamAsync` | Pfad/Stream, `CancellationToken` | `Task<TimeSpan?>` | Laufzeitermittlung — zeigt das stdin-Pump-Muster für Prozesse |

## `IAuthService` / `ApiBaseController.CheckLoggedIn`
Datei: `VideoWebPlayer/Controllers/ApiBaseController.cs`

| Methode | Zweck |
|---------|-------|
| `CheckLoggedIn` | Wirft `UnauthorizedAccessException`, wenn `CurrentUser` null — serverseitige Anmelde-Pflicht; `CheckLogedIn` ist der Legacy-Alias |
