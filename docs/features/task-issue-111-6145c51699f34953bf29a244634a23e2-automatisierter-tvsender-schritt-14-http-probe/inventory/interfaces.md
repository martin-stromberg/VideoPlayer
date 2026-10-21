# Interfaces — relevante Contracts

## `IMediaDurationProbe` (Services/IMediaDurationProbe.cs)
| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProbeFileAsync` | `string filePath`, `CancellationToken` | `Task<MediaDurationProbeResult>` | lokale Datei |
| `ProbeUriAsync` | `string uri`, `CancellationToken` | `Task<MediaDurationProbeResult>` | URI-Protokoll (seekfähig); `UriUnsupported` = Schema nicht unterstützt → Caller fällt zurück |
| `ProbeStreamAsync` | `Stream input`, `CancellationToken` | `Task<MediaDurationProbeResult>` | `pipe:0` |

`MediaDurationProbeResult`: `Success(TimeSpan)`, `TransientFailure`, `PermanentFailure`,
`UriUnsupportedFailure`; Property `Duration`, `Transient`, `UriUnsupported`.

## `IMediaSourceReader` (Services/IMediaSourceReader.cs)
| Methode | Zweck |
|---------|-------|
| `OpenFileStream(MediaCollection, string fileName)` | Stream oder null; SFTP: `SftpStreamWrapper` (CanSeek delegiert an `SftpFileStream` → seekfähig) |

## `IFfprobeLocator`
`string? Resolve()` — ffprobe-Pfad oder null.

## `IProgramScheduleSignal`
`NotifyScheduleChanged()`, `BeginScan()`, `WaitAsync(CancellationToken)` — weckt den
Sendeplan-Worker nach neu geschriebenen Laufzeiten.

## `IBackgroundProcessingGate`
`EnterOperationAsync(name, ct)` — Worker-Gate gegen parallel laufende Hintergrundarbeiten
(z. B. Backup-Restore).
