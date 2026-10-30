# Bestandsaufnahme: Interfaces — Live-Stream-Eingangskette

## `ILiveStreamSegmenter`
Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` — implementiert von `FfmpegLiveStreamSegmenter` (Singleton)

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|------------------------|-----------|--------------|-------|
| `IsRunning` | — | `bool` | Läuft gerade ein Segmentierungsprozess? |
| `CurrentSlotId` | — | `long?` | Sendeplatz des laufenden Jobs. |
| `ExpectedPositionSeconds` | — | `double?` | Erwartete Wiedergabeposition des laufenden Prozesses (Drift-Erkennung). |
| `LastUnexpectedExit` | — | `LiveStreamExitInfo?` | Diagnose des letzten vorzeitigen Exits; vom `LiveStreamCoordinator` für den HTTP→Pipe-Fallback gelesen. |
| `StartAsync` | `LiveStreamJob job, CancellationToken` | `Task` | Job starten; laufender Job wird zuerst gestoppt. |
| `StopAsync` | `CancellationToken` | `Task` | Laufenden Prozess stoppen (idempotent). |

## `IMediaSourceReader`
Datei: `VideoWebPlayer/Services/IMediaSourceReader.cs` — implementiert von `MediaSourceReaderDispatcher` (delegiert an `SftpMediaSourceReader`/`LocalMediaSourceReader`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ReadRootDirectory` | `MediaSource` | `IEnumerable<MediaEntry>` | Root-Collection einer Quelle. |
| `ReadDirectoryEntries` | `MediaCollection` | `IEnumerable<MediaEntry>` | Direkte Untereinträge. |
| `FileExistsAsync` | `MediaCollection, string fileName` | `Task<bool>` | Existenzprüfung. |
| `ReadFileAsync` | `MediaCollection, string fileName` | `Task<string?>` | Dateiinhalt als Text. |
| `ReadFileStreamAsync` | `MediaCollection, string fileName` | `Task<Stream?>` | Datei komplett als `MemoryStream` (SFTP lädt vollständig herunter). |
| `OpenFileStream` | `MediaCollection, string fileName` | `Stream?` | **Stream auf die Datei — die vom Pipe-Pump genutzte Quelle**: SFTP = `SftpStreamWrapper` über `SftpClient.OpenRead` (live vom Server, kein Fortschritts-/Vollständigkeitsfeedback), lokal = `FileStream`. |

## `IInternalMediaStreamTokenService`
Datei: `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs` — implementiert von `InternalMediaStreamTokenService` (Singleton)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `TryCreateProbeUri` | `long mediaItemId` | `string?` | Kurzlebige Loopback-URI für ffprobe; `null` wenn HTTP-Probe deaktiviert oder keine http-Adresse auffindbar. |
| `TryCreateStreamUri` | `long mediaItemId, TimeSpan lifetime` | `string?` | Loopback-URI mit langlebigem Token für den Segmenter (`api/internal/media-stream/{token}`); **`null` ohne auflösbare http-Basis-URL → stiller `pipe:0`-Fallback** (`LiveStreamMediaResolver.cs:127-129`). Nicht von `HttpProbeEnabled` abhängig. |
| `ResolveMediaItemId` | `string token` | `long?` | Token → MediaItem; `null` bei unbekannt/abgelaufen. |

## `ISegmenterProcess`
Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs:15-34` — `internal`, implementiert von `SegmenterProcess`; Test-Doubles in `FfmpegLiveStreamSegmenterTests`, `SegmenterProcessSupportTests`, `LiveStreamStandbySegmenterTests`

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|------------------------|-----------|--------------|-------|
| `HasExited` | — | `bool` | Prozess beendet? |
| `ExitCode` | — | `int` | Exit-Code (-1 solange laufend). |
| `StandardInput` | — | `Stream` | stdin für Pipe-Eingang. |
| `StandardError` | — | `Stream` | stderr (muss gedrainded werden). |
| `WaitForExitAsync` | `CancellationToken` | `Task` | Auf Prozessende warten. |
| `Kill` | — | `void` | Prozessbaum killen. |

## `IProgramGuideService`
Datei: `VideoWebPlayer/Services/IProgramGuideService.cs` — implementiert von `ProgramGuideService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNowAsync` | `DateTimeOffset utcNow, CancellationToken` | `Task<DtoProgramGuideNow>` | Laufender (`Now`) + nächster Sendeplatz; `Now` liefert `SlotId`, `MediaItemId`, `PositionSeconds`, `RemainingSeconds`, `EndUtc` — die Datenbasis des Coordinators. |
| `GetUpcomingAsync` | `DateTimeOffset, TimeSpan, CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Programmvorschau (für den Stream-Pfad nicht relevant). |

## `IFfmpegLocator` / `IFfprobeLocator`
Datei: `VideoWebPlayer/Services/IFfmpegLocator.cs` (ffprobe analog)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Resolve` | — | `string?` | Werkzeugpfad: konfigurierter Pfad → `tools/ffmpeg/` neben der App → App-Verzeichnis → `PATH`; `null` = nicht auffindbar → Coordinator meldet `FfmpegUnavailable`. |
